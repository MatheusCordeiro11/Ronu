// Ronu — Progresso de peso
// Consome GET /objetivos/tendencia (peso bruto de cada registro + peso de
// tendência suavizado ao lado), GET /objetivos/atual (objetivo, registro de
// hoje e o resumo da janela do ajuste) e GET /dietas/atual (ritmo real e
// esperado de quando a dieta atual foi gerada). Renderiza o gráfico (SVG
// desenhado à mão, sem lib externa), os cards de ajuste e ritmo e as
// pesagens recentes. A única escrita é registrar peso (POST /objetivos), com
// a pergunta opcional de aderência à dieta; editar/remover registros antigos
// continua fora daqui.

const SVG_NS = 'http://www.w3.org/2000/svg';

document.addEventListener('DOMContentLoaded', async () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const $ = (id) => document.getElementById(id);

  const elLoading = $('progresso-loading');
  const elError = $('progresso-error');
  const elEmpty = $('progresso-empty');
  const elContent = $('progresso-content');

  const elGrafico = $('peso-grafico');
  const elDesenho = $('pg-grafico-desenho');

  const btnToggleRegistrar = $('peso-registrar-toggle');
  const elRegistrar = $('pg-registrar');
  const formRegistrar = $('peso-registrar-form');
  const inputPesoRegistrar = $('peso-registrar-valor');
  const elPesoErro = $('peso-erro');
  const elRegistrarError = $('peso-registrar-error');
  const elRegistrarSucesso = $('peso-registrar-sucesso');
  const btnSalvarPeso = $('btn-registrar-peso');
  const btnCancelarRegistrar = $('btn-cancelar-registrar-peso');

  const fieldsetAderencia = $('aderencia-fieldset');
  const elAderenciaData = $('aderencia-data');
  const btnLimparAderencia = $('aderencia-limpar');
  const elObjetivoAlterado = $('aderencia-objetivo-alterado');

  // Abaixo de 720 px o formulário fica sempre aberto (canvas ProgressoMobile).
  const celular = window.matchMedia('(max-width: 720px)');

  const LINHAS_RECENTES = 8;
  const NOMES_ADERENCIA = {
    seguiu: 'Segui a dieta',
    comeu_mais: 'Comi um pouco mais',
    comeu_menos: 'Comi um pouco menos',
    nao_seguiu: 'Não deu pra seguir'
  };

  let pontos = [];
  let objetivoAtual = null;
  let progressoAjuste = null;
  let dietaAtual = null;
  let semanasPeriodo = 4;
  let verTodas = false;

  // Vêm do GET /objetivos/atual e são atualizados com a resposta do POST
  // (mesmo formato). registradoHoje é calculado no back-end com a mesma
  // regra de "hoje" do upsert — o front nunca compara datas para isso.
  let aderenciaSalva = null;
  let registradoHoje = false;
  let temDieta = false;

  // ---------- Formatação ----------

  function formatarKg(valor) {
    return Number(valor).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  }

  function formatarVariacao(valor) {
    if (Math.abs(valor) < 0.05) return '± 0,0';
    return `${valor > 0 ? '+' : '−'}${formatarKg(Math.abs(valor))}`;
  }

  // "17 out", como nas telas aprovadas (o toLocaleDateString dá "17 de out.").
  const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
  function formatarDataCurta(data) {
    const d = new Date(data);
    return `${d.getDate()} ${MESES[d.getMonth()]}`;
  }

  // Para o meio de uma frase ("Desde a pesagem de 28 de setembro, ..."): o
  // mês abreviado termina em ponto ("28 de set."), que fica ruim antes da
  // vírgula. Ano só quando não é o atual.
  function formatarDataPorExtenso(isoString) {
    const data = new Date(isoString);
    const opcoes = { day: 'numeric', month: 'long' };
    if (data.getFullYear() !== new Date().getFullYear()) opcoes.year = 'numeric';
    return data.toLocaleDateString('pt-BR', opcoes);
  }

  // Ritmo em g ou kg por semana, com sinal: "−200 g/sem", "+1,2 kg/sem".
  function formatarRitmo(kgSemana) {
    if (kgSemana == null) return '—';
    const absoluto = Math.abs(kgSemana);
    const sinal = absoluto < 0.005 ? '' : kgSemana < 0 ? '−' : '+';
    const valor = absoluto >= 1
      ? `${absoluto.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} kg`
      : `${Math.round(absoluto * 100) * 10} g`;
    return `${sinal}${valor}/sem`;
  }

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto != null) el.textContent = texto;
    return el;
  }

  function svg(tag, atributos) {
    const el = document.createElementNS(SVG_NS, tag);
    Object.entries(atributos).forEach(([chave, valor]) => el.setAttribute(chave, valor));
    return el;
  }

  // ---------- Estados de topo (mutuamente exclusivos) ----------

  function mostrarSomente(elementoVisivel) {
    [elLoading, elError, elEmpty, elContent].forEach((el) => {
      el.hidden = el !== elementoVisivel;
    });
    btnToggleRegistrar.hidden = elementoVisivel !== elContent;
  }

  // ---------- Período ----------

  // Pontos das últimas N semanas, contadas da última pesagem (não de hoje:
  // quem ficou um tempo sem pesar ainda vê a própria curva).
  function pontosDoPeriodo() {
    const ultimo = new Date(pontos[pontos.length - 1].data).getTime();
    const inicio = ultimo - semanasPeriodo * 7 * 86400000;
    return pontos.filter((p) => new Date(p.data).getTime() >= inicio);
  }

  const elPeriodos = $('pg-periodos');
  const botoesPeriodo = Array.from(elPeriodos.querySelectorAll('button'));

  function selecionarPeriodo(botao, focar) {
    semanasPeriodo = Number(botao.dataset.semanas);
    botoesPeriodo.forEach((b) => {
      const marcado = b === botao;
      b.setAttribute('aria-selected', String(marcado));
      b.tabIndex = marcado ? 0 : -1;
    });
    if (focar) botao.focus();
    renderizarNumeros();
    renderizarGrafico();
  }

  botoesPeriodo.forEach((botao) => botao.addEventListener('click', () => selecionarPeriodo(botao, false)));
  elPeriodos.addEventListener('keydown', (evento) => {
    const passo = { ArrowRight: 1, ArrowLeft: -1 }[evento.key];
    if (!passo) return;
    evento.preventDefault();
    const atual = botoesPeriodo.findIndex((b) => b.getAttribute('aria-selected') === 'true');
    selecionarPeriodo(botoesPeriodo[(atual + passo + botoesPeriodo.length) % botoesPeriodo.length], true);
  });

  // ---------- Render: números do topo do gráfico ----------

  function renderizarNumeros() {
    const periodo = pontosDoPeriodo();
    const ultimo = pontos[pontos.length - 1];
    $('pg-tendencia').textContent = formatarKg(ultimo.pesoTendencia);
    $('pg-variacao').textContent = periodo.length > 1
      ? `${formatarVariacao(ultimo.pesoTendencia - periodo[0].pesoTendencia)} kg`
      : '—';
    $('pg-periodo-rotulo').textContent = celular.matches ? `${semanasPeriodo} semanas` : 'No período';
    $('pg-ultima').textContent = `${formatarKg(ultimo.pesoBruto)} kg · ${formatarDataCurta(ultimo.data)}`;
  }

  // ---------- Render: gráfico SVG ----------
  // O viewBox é montado na escala real da caixa (1 unidade = 1 px) a cada
  // render e a cada resize: assim nem o desenho distorce nem a linha afina
  // num contêiner estreito (celular).
  const PAD_TOPO = 16;
  const PAD_BASE = 16;
  const PAD_DIREITA = 44; // espaço dos valores do eixo Y, à direita

  function renderizarGrafico() {
    const periodo = pontosDoPeriodo();
    const caixa = elGrafico.getBoundingClientRect();
    const largura = Math.max(caixa.width, 1);
    const altura = Math.max(caixa.height, 1);
    elGrafico.setAttribute('viewBox', `0 0 ${largura} ${altura}`);
    elGrafico.querySelectorAll(':scope > :not(title)').forEach((el) => el.remove());

    const larguraPlot = Math.max(largura - PAD_DIREITA, 1);
    const alturaPlot = Math.max(altura - PAD_TOPO - PAD_BASE, 1);

    const tempos = periodo.map((p) => new Date(p.data).getTime());
    const pesos = periodo.flatMap((p) => [Number(p.pesoBruto), Number(p.pesoTendencia)]);
    const tMin = Math.min(...tempos);
    const tMax = Math.max(...tempos);
    const minObservado = Math.min(...pesos);
    const maxObservado = Math.max(...pesos);
    const folga = Math.max((maxObservado - minObservado) * 0.15, 0.3);
    const pMin = minObservado - folga;
    const pMax = maxObservado + folga;

    const x = (t) => (tMax === tMin ? larguraPlot / 2 : ((t - tMin) / (tMax - tMin)) * larguraPlot);
    const y = (p) => PAD_TOPO + ((pMax - p) / (pMax - pMin)) * alturaPlot;

    // Três linhas de grade, com os valores no eixo Y (HTML ao lado, para o
    // texto não esticar com o SVG).
    const marcas = [maxObservado, (maxObservado + minObservado) / 2, minObservado];
    marcas.forEach((valor) => {
      elGrafico.appendChild(svg('line', { class: 'pg-grade-linha', x1: 0, x2: larguraPlot, y1: y(valor), y2: y(valor) }));
    });
    const eixoY = $('pg-eixo-y');
    eixoY.replaceChildren(...marcas.map((valor) => {
      const rotulo = criar('span', null, formatarKg(valor));
      rotulo.style.top = `${y(valor)}px`;
      return rotulo;
    }));

    const vertices = periodo.map((p, i) => [x(tempos[i]), y(Number(p.pesoTendencia))]);
    if (periodo.length > 1) {
      const caminho = vertices.map(([vx, vy], i) => `${i ? 'L' : 'M'}${vx.toFixed(1)} ${vy.toFixed(1)}`).join(' ');
      const area = svg('path', { class: 'pg-area', d: `${caminho} L${larguraPlot} ${altura} L0 ${altura} Z`, 'data-anima': 'surge' });
      area.style.setProperty('--atraso', '500ms');
      elGrafico.appendChild(area);
    }

    const raio = periodo.length > 20 ? 2.5 : 3.5;
    const grupoPontos = svg('g', { class: 'pg-pontos', 'data-anima': 'pontos' });
    periodo.forEach((p, i) => {
      grupoPontos.appendChild(svg('circle', { cx: x(tempos[i]).toFixed(1), cy: y(Number(p.pesoBruto)).toFixed(1), r: raio }));
    });
    elGrafico.appendChild(grupoPontos);

    if (periodo.length > 1) {
      elGrafico.appendChild(svg('path', {
        class: 'pg-linha',
        d: vertices.map(([vx, vy], i) => `${i ? 'L' : 'M'}${vx.toFixed(1)} ${vy.toFixed(1)}`).join(' '),
        pathLength: 1,
        'data-anima': 'linha'
      }));
    }

    // A tendência de hoje, em destaque no fim da linha.
    const [ux, uy] = vertices[vertices.length - 1];
    elGrafico.appendChild(svg('circle', { class: 'pg-atual', cx: ux.toFixed(1), cy: uy.toFixed(1), r: 6 }));

    // Datas embaixo: 5 no desktop, 3 no celular, espalhadas no período.
    const quantas = Math.min(celular.matches ? 3 : 5, periodo.length);
    const eixoX = $('pg-eixo-x');
    eixoX.replaceChildren(...Array.from({ length: quantas }, (_, i) => {
      const fracao = quantas === 1 ? 0 : i / (quantas - 1);
      return criar('span', null, formatarDataCurta(tMin + fracao * (tMax - tMin)));
    }));
    eixoX.style.paddingRight = `${PAD_DIREITA}px`;

    const inicio = periodo[0].pesoTendencia;
    const fim = periodo[periodo.length - 1].pesoTendencia;
    const direcao = periodo.length < 2 ? 'com uma pesagem só'
      : Math.abs(fim - inicio) < 0.05 ? 'estável'
      : fim < inicio ? 'caindo' : 'subindo';
    $('peso-grafico-titulo').textContent = periodo.length < 2
      ? `Peso de tendência de ${formatarKg(fim)} kg, com uma pesagem no período`
      : `Peso de tendência ${direcao}, de ${formatarKg(inicio)} para ${formatarKg(fim)} kg nas últimas ${semanasPeriodo} semanas`;
  }

  let redimensionamentoPendente = null;
  window.addEventListener('resize', () => {
    if (elContent.hidden || pontos.length === 0) return;
    clearTimeout(redimensionamentoPendente);
    redimensionamentoPendente = setTimeout(() => {
      renderizarNumeros();
      renderizarGrafico();
    }, 150);
  });

  // ---------- Render: ajuste pelo progresso ----------

  function renderizarAjuste() {
    const card = $('pg-ajuste');
    const r = progressoAjuste;
    card.hidden = !r;
    if (!r) return;

    $('pg-ajuste-estado').textContent = r.suficiente ? 'Ativo' : 'Ainda não';
    $('pg-ajuste-regra').textContent =
      `Pede pelo menos ${r.minimoPesagens} pesagens cobrindo ${r.minimoDias / 7} semanas, nas últimas ${r.janelaDias / 7} e com o mesmo objetivo.`;

    const falta = $('pg-ajuste-falta');
    if (!temDieta) {
      falta.textContent = 'As pesagens começam a contar depois da sua primeira dieta.';
      falta.hidden = false;
    } else if (!r.suficiente) {
      const pesagens = Math.max(0, r.minimoPesagens - r.pesagens);
      const dias = Math.max(0, r.minimoDias - r.diasCobertos);
      const partes = [];
      if (pesagens) partes.push(`${pesagens} ${pesagens === 1 ? 'pesagem' : 'pesagens'}`);
      if (dias) partes.push(`${dias} ${dias === 1 ? 'dia' : 'dias'}`);
      falta.textContent = `${partes.length > 1 ? 'Faltam' : pesagens === 1 || dias === 1 ? 'Falta' : 'Faltam'} ${partes.join(' e ')}. Até lá, a sua meta segue a conta padrão.`;
      falta.hidden = partes.length === 0;
    } else {
      falta.hidden = true;
    }

    $('pg-ajuste-pesagens').textContent = r.suficiente
      ? `${r.pesagens} · mínimo ${r.minimoPesagens}`
      : `${r.pesagens} de ${r.minimoPesagens}`;
    $('pg-ajuste-dias').textContent = r.suficiente
      ? `${r.diasCobertos} · mínimo ${r.minimoDias}`
      : `${r.diasCobertos} de ${r.minimoDias}`;
    $('pg-ajuste-pesagens-barra').style.width = `${Math.min(100, (r.pesagens / r.minimoPesagens) * 100)}%`;
    $('pg-ajuste-dias-barra').style.width = `${Math.min(100, (r.diasCobertos / r.minimoDias) * 100)}%`;
  }

  // ---------- Render: ritmo da dieta atual ----------

  // O ajuste é um registro de como a dieta atual foi feita, então o ritmo é
  // o de quando ela foi gerada (o mesmo que o "Saiba mais" do dashboard usa).
  function textoRitmo(ajuste) {
    if (!ajuste || ajuste.motivo === 'HistoricoInsuficiente') {
      return 'Com poucas pesagens o ritmo ainda oscila. Ele só passa a mexer na sua meta quando o ajuste ativar.';
    }
    const percentual = `${(Math.abs(ajuste.percentual) * 100).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
    if (ajuste.motivo === 'PesoAcimaDoEsperado') return `Por isso a sua dieta atual saiu com a meta ${percentual} menor.`;
    if (ajuste.motivo === 'PesoAbaixoDoEsperado') return `Por isso a sua dieta atual saiu com a meta ${percentual} maior.`;
    return 'Dentro do esperado: a meta da sua dieta atual não mudou.';
  }

  function renderizarRitmo() {
    const card = $('pg-ritmo');
    card.hidden = !dietaAtual;
    const ajuste = dietaAtual ? dietaAtual.dieta.ajusteAdaptativo : null;
    $('pg-ritmo-real').textContent = formatarRitmo(ajuste ? ajuste.ritmoRealKgSemana : null);
    $('pg-ritmo-esperado').textContent = formatarRitmo(ajuste ? ajuste.ritmoEsperadoKgSemana : null);
    const texto = textoRitmo(ajuste);
    $('pg-ritmo-texto').textContent = texto;
    // No celular o texto vai no card do ajuste (canvas ProgressoMobile).
    $('pg-ajuste-ritmo').textContent = texto;
    $('pg-ajuste-ritmo').hidden = !dietaAtual;
  }

  // ---------- Render: pesagens recentes ----------

  function criarResposta(aderencia) {
    if (!aderencia) return criar('span', 'pg-sem-resposta', 'Sem resposta');
    return criar('span', `chip chip-pequeno pg-resposta${aderencia === 'nao_seguiu' ? ' pg-resposta-fora' : ''}`, NOMES_ADERENCIA[aderencia] || aderencia);
  }

  function renderizarPesagens() {
    const recentes = [...pontos].reverse();
    const visiveis = verTodas ? recentes : recentes.slice(0, LINHAS_RECENTES);

    $('pg-tabela-corpo').replaceChildren(...visiveis.map((p) => {
      const linha = criar('tr');
      const resposta = criar('td');
      resposta.appendChild(criarResposta(p.aderencia));
      linha.append(
        criar('td', null, formatarDataCurta(p.data)),
        criar('td', null, `${formatarKg(p.pesoBruto)} kg`),
        criar('td', 'texto-2', `${formatarKg(p.pesoTendencia)} kg`),
        resposta
      );
      return linha;
    }));

    $('pg-lista').replaceChildren(...visiveis.map((p) => {
      const item = criar('li', 'num');
      const textos = criar('div', 'pg-lista-textos');
      textos.append(
        criar('span', 'pg-lista-peso', `${formatarKg(p.pesoBruto)} kg`),
        criar('span', 'apoio', `${formatarDataCurta(p.data)} · tendência ${formatarKg(p.pesoTendencia)}`)
      );
      item.append(textos, criarResposta(p.aderencia));
      return item;
    }));

    const botao = $('pg-ver-todas');
    botao.hidden = recentes.length <= LINHAS_RECENTES;
    botao.textContent = verTodas ? 'Mostrar só as recentes' : `Ver todas as ${recentes.length} pesagens`;
  }

  $('pg-ver-todas').addEventListener('click', () => {
    verTodas = !verTodas;
    renderizarPesagens();
  });

  // ---------- Orquestração ----------

  function renderizarTudo() {
    if (pontos.length === 0) {
      mostrarSomente(elEmpty);
      return;
    }

    mostrarSomente(elContent);
    renderizarNumeros();
    renderizarAjuste();
    renderizarRitmo();
    renderizarPesagens();
    // O SVG só tem largura real depois de entrar no layout (elContent deixa
    // de estar hidden acima) — por isso o gráfico é desenhado depois disso.
    renderizarGrafico();
  }

  async function buscar(caminho) {
    const resposta = await ronuFetchAutenticado(caminho);
    return resposta;
  }

  async function carregarTendencia() {
    const resposta = await buscar('/objetivos/tendencia');
    if (!resposta.ok) throw new Error('Não foi possível carregar seu progresso de peso.');
    pontos = (await resposta.json()).map((p) => ({ ...p, pesoBruto: Number(p.pesoBruto), pesoTendencia: Number(p.pesoTendencia) }));
  }

  // Objetivo atual: pré-seleciona o objetivo do formulário e traz o resumo
  // da janela do ajuste. Falha aqui não derruba a tela — o formulário só abre
  // sem nada pré-selecionado e o card do ajuste some.
  async function carregarObjetivoAtual() {
    try {
      const resposta = await buscar('/objetivos/atual');
      if (resposta.ok) guardarObjetivoSalvo(await resposta.json());
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') throw erro;
    }
  }

  // Dieta atual (404 = ainda sem dieta): só para o card de ritmo.
  async function carregarDietaAtual() {
    try {
      const resposta = await buscar('/dietas/atual');
      dietaAtual = resposta.ok ? await resposta.json() : null;
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') throw erro;
      dietaAtual = null;
    }
  }

  async function iniciar() {
    mostrarSomente(elLoading);

    try {
      await Promise.all([carregarTendencia(), carregarObjetivoAtual(), carregarDietaAtual()]);
      renderizarTudo();
      prepararFormulario();
      // Vindo do "Registrar peso de hoje" do dashboard: já abre o formulário.
      if (window.location.hash === '#registrar' && !elContent.hidden) abrirFormularioRegistrar();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') mostrarSomente(elError);
    }
  }

  $('btn-tentar-novamente').addEventListener('click', iniciar);

  // ---------- Registrar peso ----------
  // POST /objetivos faz upsert por data no back-end (se já existe um
  // registro de hoje, atualiza em vez de duplicar), então não há
  // necessidade de escolher entre criar/editar aqui.

  function marcarObjetivo(valor) {
    const radio = formRegistrar.querySelector(`input[name="peso-registrar-objetivo"][value="${valor}"]`);
    if (radio) radio.checked = true;
  }

  // Estado inicial do formulário, a cada carga e depois de salvar: objetivo
  // salvo marcado e, se já há registro hoje, o peso e a resposta dele (o POST
  // substitui o registro de hoje inteiro — sem isso, corrigir só o peso
  // apagaria a resposta sem querer).
  function prepararFormulario() {
    formRegistrar.reset();
    ronuLimparCampoInvalido($('campo-peso'), elPesoErro);
    ronuOcultarErroFormulario(elRegistrarError);
    if (objetivoAtual) marcarObjetivo(objetivoAtual);

    const ultimo = pontos[pontos.length - 1];
    if (ultimo) inputPesoRegistrar.placeholder = formatarKg(ultimo.pesoBruto);
    if (registradoHoje && ultimo) inputPesoRegistrar.value = String(ultimo.pesoBruto);

    if (registradoHoje && aderenciaSalva) {
      const radioAderencia = fieldsetAderencia.querySelector(`input[name="peso-registrar-aderencia"][value="${aderenciaSalva}"]`);
      if (radioAderencia) radioAderencia.checked = true;
    }
    atualizarAderencia();
  }

  function abrirFormularioRegistrar() {
    elRegistrarSucesso.hidden = true;
    elRegistrar.classList.add('pg-registrar-aberto');
    btnToggleRegistrar.setAttribute('aria-expanded', 'true');
    btnToggleRegistrar.textContent = 'Fechar registro';
    btnToggleRegistrar.classList.replace('botao-primario', 'botao-fantasma');
    inputPesoRegistrar.focus();
  }

  function fecharFormularioRegistrar() {
    elRegistrar.classList.remove('pg-registrar-aberto');
    btnToggleRegistrar.setAttribute('aria-expanded', 'false');
    btnToggleRegistrar.textContent = 'Registrar peso';
    btnToggleRegistrar.classList.replace('botao-fantasma', 'botao-primario');
    prepararFormulario();
  }

  btnToggleRegistrar.addEventListener('click', () => {
    if (elRegistrar.classList.contains('pg-registrar-aberto')) {
      fecharFormularioRegistrar();
      btnToggleRegistrar.focus();
    } else {
      abrirFormularioRegistrar();
    }
  });
  btnCancelarRegistrar.addEventListener('click', () => {
    fecharFormularioRegistrar();
    btnToggleRegistrar.focus();
  });

  // ---------- Aderência à dieta ----------
  // A resposta vale para o intervalo desde a pesagem anterior (cada registro
  // é um ponto no cálculo da meta adaptativa). A pergunta só aparece quando
  // faz sentido: com dieta gerada (sem dieta não há o que seguir), com uma
  // pesagem anterior para servir de referência e com o mesmo objetivo já
  // salvo — trocar o objetivo começa um período novo no cálculo.

  function guardarObjetivoSalvo(objetivo) {
    objetivoAtual = objetivo.objetivo;
    aderenciaSalva = objetivo.aderencia;
    registradoHoje = objetivo.registradoHoje === true;
    temDieta = objetivo.temDieta === true;
    progressoAjuste = objetivo.progressoAjuste || null;
  }

  // Se o registro de hoje já existe, o último ponto é ele mesmo — a
  // referência passa a ser o penúltimo.
  function dataPesagemAnterior() {
    const indice = registradoHoje ? pontos.length - 2 : pontos.length - 1;
    return indice >= 0 ? pontos[indice].data : null;
  }

  function atualizarAderencia() {
    const dataAnterior = dataPesagemAnterior();
    const disponivel = temDieta && dataAnterior !== null;

    const objetivoMarcado = formRegistrar.querySelector('input[name="peso-registrar-objetivo"]:checked');
    const objetivoAlterado = disponivel && Boolean(objetivoMarcado) && objetivoMarcado.value !== objetivoAtual;

    if (disponivel) elAderenciaData.textContent = formatarDataPorExtenso(dataAnterior);

    // Só esconde (não desmarca): voltando ao objetivo salvo, a resposta
    // continua lá.
    fieldsetAderencia.hidden = !disponivel || objetivoAlterado;
    elObjetivoAlterado.hidden = !objetivoAlterado;
    btnLimparAderencia.hidden = !fieldsetAderencia.querySelector('input[name="peso-registrar-aderencia"]:checked');
  }

  formRegistrar.querySelectorAll('input[name="peso-registrar-objetivo"], input[name="peso-registrar-aderencia"]').forEach((radio) => {
    radio.addEventListener('change', atualizarAderencia);
  });

  // O botão some assim que nada fica marcado — o foco vai para a primeira
  // opção em vez de se perder junto com ele.
  btnLimparAderencia.addEventListener('click', () => {
    const opcoes = fieldsetAderencia.querySelectorAll('input[name="peso-registrar-aderencia"]');
    opcoes.forEach((radio) => { radio.checked = false; });
    opcoes[0].focus();
    atualizarAderencia();
  });

  // Pergunta escondida = sem resposta: envia null, o que também limpa uma
  // resposta salva hoje (ex.: objetivo trocado no mesmo envio).
  function aderenciaParaEnviar() {
    if (fieldsetAderencia.hidden) return null;
    const marcada = fieldsetAderencia.querySelector('input[name="peso-registrar-aderencia"]:checked');
    return marcada ? marcada.value : null;
  }

  inputPesoRegistrar.addEventListener('input', () => ronuLimparCampoInvalido($('campo-peso'), elPesoErro));

  formRegistrar.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    const peso = parseFloat(inputPesoRegistrar.value);
    if (!(peso >= 20 && peso <= 300)) {
      ronuMarcarCampoInvalido($('campo-peso'), elPesoErro, 'Informe um peso entre 20 e 300 kg.');
      inputPesoRegistrar.focus();
      return;
    }

    const objetivoSelecionado = formRegistrar.querySelector('input[name="peso-registrar-objetivo"]:checked');
    if (!objetivoSelecionado) {
      ronuMostrarErroFormulario(elRegistrarError, 'Selecione um objetivo.');
      return;
    }

    ronuOcultarErroFormulario(elRegistrarError);
    ronuDefinirCarregando(btnSalvarPeso, true, 'Salvando...');

    try {
      const resposta = await ronuFetchAutenticado('/objetivos', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          peso,
          objetivo: objetivoSelecionado.value,
          aderencia: aderenciaParaEnviar()
        })
      });

      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível registrar seu peso. Tente novamente.');
      }

      // Mesmo formato do GET /objetivos/atual.
      guardarObjetivoSalvo(await resposta.json());
      await carregarTendencia();

      fecharFormularioRegistrar();
      renderizarTudo();
      elRegistrarSucesso.textContent = 'Peso registrado. A tendência se ajusta aos poucos, para refletir a direção real, não picos de um dia.';
      elRegistrarSucesso.hidden = false;
      elRegistrarSucesso.scrollIntoView({ block: 'nearest' });
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        ronuMostrarErroFormulario(elRegistrarError, erro.message);
      }
    } finally {
      ronuDefinirCarregando(btnSalvarPeso, false);
    }
  });

  await iniciar();
});
