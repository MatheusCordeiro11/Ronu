// Ronu — Progresso de peso
// Consome GET /objetivos/tendencia (peso bruto de cada registro + peso de
// tendência suavizado ao lado) e renderiza um carimbo de resumo, um gráfico
// SVG (desenhado à mão, sem lib externa) e o histórico completo. A única
// escrita é o formulário inline de registrar peso (POST /objetivos), com a
// pergunta opcional de aderência à dieta; editar/remover registros antigos
// continua fora daqui.

const SVG_NS = 'http://www.w3.org/2000/svg';

document.addEventListener('DOMContentLoaded', async () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const elLoading = document.getElementById('progresso-loading');
  const elError = document.getElementById('progresso-error');
  const elEmpty = document.getElementById('progresso-empty');
  const elContent = document.getElementById('progresso-content');

  const elStampData = document.getElementById('peso-stamp-data');
  const elStampTendencia = document.getElementById('peso-stamp-tendencia');
  const elStampBruto = document.getElementById('peso-stamp-bruto');
  const elStampVariacao = document.getElementById('peso-stamp-variacao');

  const elGrafico = document.getElementById('peso-grafico');
  const elHistoricoList = document.getElementById('peso-historico-list');

  const btnLogout = document.getElementById('logout-btn');

  const btnToggleRegistrar = document.getElementById('peso-registrar-toggle');
  const formRegistrar = document.getElementById('peso-registrar-form');
  const inputPesoRegistrar = document.getElementById('peso-registrar-valor');
  const elRegistrarError = document.getElementById('peso-registrar-error');
  const elRegistrarSucesso = document.getElementById('peso-registrar-sucesso');
  const btnSalvarPeso = document.getElementById('btn-registrar-peso');
  const btnCancelarRegistrar = document.getElementById('btn-cancelar-registrar-peso');

  const fieldsetAderencia = document.getElementById('aderencia-fieldset');
  const elAderenciaData = document.getElementById('aderencia-data');
  const btnLimparAderencia = document.getElementById('aderencia-limpar');
  const elObjetivoAlterado = document.getElementById('aderencia-objetivo-alterado');

  let pontos = [];
  let objetivoAtual = null;

  // Vêm do GET /objetivos/atual e são atualizados com a resposta do POST
  // (mesmo formato). registradoHoje é calculado no back-end com a mesma
  // regra de "hoje" do upsert — o front nunca compara datas para isso.
  let aderenciaSalva = null;
  let registradoHoje = false;
  let temDieta = false;

  // A linha de tendência é "desenhada" só na primeira vez que o gráfico
  // aparece na página. Resize e registro de peso redesenham estático — uma
  // linha se redesenhando a cada ajuste de janela seria só ruído.
  let animarEntrada = true;

  btnLogout.addEventListener('click', () => {
    ronuLimparSessao();
    window.location.href = 'login.html';
  });

  // ---------- Formatação ----------

  function formatarPeso(valor) {
    return `${valor.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} kg`;
  }

  function formatarVariacao(valor) {
    if (valor === null) return '—';
    const sinal = valor > 0 ? '+' : '';
    return `${sinal}${formatarPeso(valor)}`;
  }

  function formatarDataCurta(isoString) {
    return new Date(isoString).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
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

  // ---------- Estados de topo (mutuamente exclusivos) ----------

  function mostrarSomente(elementoVisivel) {
    [elLoading, elError, elEmpty, elContent].forEach((el) => {
      el.hidden = el !== elementoVisivel;
    });
  }

  // ---------- Render: carimbo de resumo ----------

  function renderizarStamp() {
    const ultimo = pontos[pontos.length - 1];
    const primeiro = pontos[0];
    const variacao = pontos.length > 1 ? ultimo.pesoBruto - primeiro.pesoBruto : null;

    elStampData.textContent = `Última pesagem em ${formatarDataCurta(ultimo.data)}`;
    elStampTendencia.textContent = formatarPeso(ultimo.pesoTendencia);
    elStampBruto.textContent = formatarPeso(ultimo.pesoBruto);
    elStampVariacao.textContent = formatarVariacao(variacao);
  }

  // ---------- Render: histórico ----------

  function renderizarHistorico() {
    elHistoricoList.innerHTML = '';

    [...pontos].reverse().forEach((ponto) => {
      const item = document.createElement('li');
      item.className = 'peso-historico-item';

      const data = document.createElement('span');
      data.className = 'peso-historico-data';
      data.textContent = formatarDataCurta(ponto.data);

      const valores = document.createElement('dl');
      valores.className = 'peso-historico-valores num';
      valores.innerHTML = `
        <div><dt>Bruto</dt><dd>${formatarPeso(ponto.pesoBruto)}</dd></div>
        <div><dt>Tendência</dt><dd>${formatarPeso(ponto.pesoTendencia)}</dd></div>
      `;

      item.append(data, valores);
      elHistoricoList.appendChild(item);
    });
  }

  // ---------- Render: gráfico SVG ----------
  // O SVG não usa um viewBox fixo em "unidades de design": a cada render (e a
  // cada resize) medimos a caixa real do elemento em px e montamos o viewBox
  // nessa mesma escala, então 1 unidade = 1px de verdade em qualquer largura
  // de tela — evita tanto distorcer o desenho quanto encolher o texto dos
  // eixos junto com o gráfico num container estreito (mobile).
  const PAD_ESQUERDA = 52;
  const PAD_DIREITA = 12;
  const PAD_TOPO = 16;
  const PAD_BASE = 28;
  // Precisa bater com o "+ 4px" do keyframe peso-linha-desenho (progresso.css).
  const ENTRADA_FOLGA = 4;

  function criarElementoSvg(tag, atributos) {
    const el = document.createElementNS(SVG_NS, tag);
    Object.entries(atributos).forEach(([chave, valor]) => el.setAttribute(chave, valor));
    return el;
  }

  function renderizarGrafico() {
    const animar = animarEntrada;
    animarEntrada = false;
    if (animar) aguardarGraficoNaTela();

    const caixa = elGrafico.getBoundingClientRect();
    const largura = Math.max(caixa.width, 1);
    const altura = Math.max(caixa.height, 1);
    elGrafico.setAttribute('viewBox', `0 0 ${largura} ${altura}`);

    // Preserva o <title> de acessibilidade e limpa o resto antes de redesenhar.
    elGrafico.querySelectorAll(':scope > :not(title)').forEach((el) => el.remove());

    const larguraPlot = Math.max(largura - PAD_ESQUERDA - PAD_DIREITA, 1);
    const alturaPlot = Math.max(altura - PAD_TOPO - PAD_BASE, 1);

    const datas = pontos.map((p) => new Date(p.data).getTime());
    const pesos = pontos.flatMap((p) => [p.pesoBruto, p.pesoTendencia]);
    const dataMin = Math.min(...datas);
    const dataMax = Math.max(...datas);
    const pesoMinBruto = Math.min(...pesos);
    const pesoMaxBruto = Math.max(...pesos);
    const margemPeso = Math.max((pesoMaxBruto - pesoMinBruto) * 0.15, 0.5);
    const pesoMin = pesoMinBruto - margemPeso;
    const pesoMax = pesoMaxBruto + margemPeso;

    function x(dataMs) {
      if (dataMax === dataMin) return PAD_ESQUERDA + larguraPlot / 2;
      return PAD_ESQUERDA + ((dataMs - dataMin) / (dataMax - dataMin)) * larguraPlot;
    }

    function y(peso) {
      return PAD_TOPO + alturaPlot - ((peso - pesoMin) / (pesoMax - pesoMin)) * alturaPlot;
    }

    // Linhas de grade + rótulos do eixo Y (mínimo / meio / máximo observados).
    [pesoMaxBruto, (pesoMaxBruto + pesoMinBruto) / 2, pesoMinBruto].forEach((valor) => {
      const posY = y(valor);
      elGrafico.appendChild(criarElementoSvg('line', {
        class: 'peso-grid-linha',
        x1: PAD_ESQUERDA, x2: largura - PAD_DIREITA, y1: posY, y2: posY
      }));

      const label = criarElementoSvg('text', {
        class: 'peso-eixo-label',
        x: 0, y: posY + 4, 'text-anchor': 'start'
      });
      label.textContent = valor.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
      elGrafico.appendChild(label);
    });

    // Rótulos do eixo X (primeira e última data — o intervalo real entre
    // pesagens já fica visível na posição horizontal dos próprios pontos).
    [pontos[0], pontos[pontos.length - 1]].forEach((ponto, indice) => {
      const label = criarElementoSvg('text', {
        class: 'peso-eixo-label',
        x: x(new Date(ponto.data).getTime()),
        y: altura - 6,
        'text-anchor': indice === 0 ? 'start' : 'end'
      });
      label.textContent = formatarDataCurta(ponto.data);
      elGrafico.appendChild(label);
    });

    // Momento (fração da duração da entrada) em que a ponta da linha passa
    // por cada pesagem — 0 para todas quando não há linha (ponto único).
    let fracoesEntrada = pontos.map(() => 0);

    // Linha de tendência (sólida, cor de faixa) por cima dos pontos brutos
    // esmaecidos — a mesma leitura de MacroFactor/Libra citada no brief.
    if (pontos.length > 1) {
      const vertices = pontos.map((p) => [x(new Date(p.data).getTime()), y(p.pesoTendencia)]);
      const linha = criarElementoSvg('polyline', {
        class: 'peso-linha-tendencia',
        points: vertices.map(([vx, vy]) => `${vx},${vy}`).join(' ')
      });
      elGrafico.appendChild(linha);

      if (animar) {
        // getTotalLength só mede depois que a linha está no SVG.
        const comprimento = linha.getTotalLength();
        linha.style.setProperty('--linha-comprimento', `${comprimento}px`);
        linha.classList.add('peso-entrada');

        // A entrada usa ease-out quadrático exato (desenhado = 1 − (1 − t)²)
        // e começa ENTRADA_FOLGA px antes do início da linha (ver CSS), então
        // dá para inverter a conta e achar o t em que a ponta chega a cada
        // vértice: t = 1 − √(L·(1 − p) / (L + folga)).
        let acumulado = 0;
        fracoesEntrada = vertices.map(([vx, vy], indice) => {
          if (indice > 0) {
            const [ax, ay] = vertices[indice - 1];
            acumulado += Math.hypot(vx - ax, vy - ay);
          }
          const p = comprimento > 0 ? Math.min(acumulado / comprimento, 1) : 0;
          return 1 - Math.sqrt((comprimento * (1 - p)) / (comprimento + ENTRADA_FOLGA));
        });
      }
    }

    const raioPonto = pontos.length > 20 ? 2.5 : 3.5;
    pontos.forEach((p, indice) => {
      elGrafico.appendChild(marcarEntrada(criarElementoSvg('circle', {
        class: 'peso-ponto-bruto',
        cx: x(new Date(p.data).getTime()),
        cy: y(p.pesoBruto),
        r: raioPonto
      }), fracoesEntrada[indice]));
    });

    // Único ponto: sem linha pra desenhar, mas o próprio peso de tendência
    // (igual ao bruto no primeiro registro) ainda merece uma marca sólida.
    if (pontos.length === 1) {
      elGrafico.appendChild(marcarEntrada(criarElementoSvg('circle', {
        class: 'peso-ponto-bruto',
        cx: x(new Date(pontos[0].data).getTime()),
        cy: y(pontos[0].pesoTendencia),
        r: 3.5,
        style: 'fill: var(--accent); opacity: 1;'
      }), 0));
    }

    function marcarEntrada(ponto, fracao) {
      if (!animar) return ponto;
      ponto.style.setProperty('--entrada-fracao', fracao.toFixed(4));
      ponto.classList.add('peso-entrada');
      return ponto;
    }
  }

  // O gráfico costuma estar abaixo da dobra (no mobile, sempre): a entrada
  // fica pausada no estado "invisível" (data-entrada="pendente", ver CSS) e
  // só toca quando ~40% dele entra na tela. Se já estiver visível ao
  // carregar, o observer dispara na hora. Um redesenho antes disso (resize)
  // sai estático e sem .peso-entrada, então liberar a pausa depois não faz
  // nada — continua valendo "uma vez por visita".
  function aguardarGraficoNaTela() {
    if (!('IntersectionObserver' in window)) return;

    elGrafico.dataset.entrada = 'pendente';
    const observador = new IntersectionObserver((entradas) => {
      if (!entradas.some((entrada) => entrada.isIntersecting)) return;
      delete elGrafico.dataset.entrada;
      observador.disconnect();
    }, { threshold: 0.4 });
    observador.observe(elGrafico);
  }

  let redimensionamentoPendente = null;
  window.addEventListener('resize', () => {
    if (elContent.hidden || pontos.length === 0) return;
    clearTimeout(redimensionamentoPendente);
    redimensionamentoPendente = setTimeout(renderizarGrafico, 150);
  });

  // ---------- Orquestração ----------

  function renderizarTudo() {
    if (pontos.length === 0) {
      mostrarSomente(elEmpty);
      return;
    }

    renderizarStamp();
    renderizarHistorico();
    mostrarSomente(elContent);
    // O SVG só tem largura real depois de entrar no layout (elContent deixa
    // de estar hidden acima) — por isso o gráfico é desenhado depois disso.
    renderizarGrafico();
  }

  async function carregarTendencia() {
    const resposta = await ronuFetchAutenticado('/objetivos/tendencia');
    if (!resposta.ok) throw new Error('Não foi possível carregar seu progresso de peso.');
    pontos = await resposta.json();
    renderizarTudo();
    // Formulário aberto antes da tendência chegar: a data da pesagem
    // anterior só existe agora.
    if (!formRegistrar.hidden) atualizarAderencia();
  }

  // Busca o objetivo atual só pra pré-selecionar o radio do formulário de
  // registrar peso. Falha aqui não derruba a tela — o formulário só abre
  // sem nada pré-selecionado. Guarda a própria promise (em vez de só
  // aguardá-la aqui) porque ela corre em paralelo com carregarTendencia():
  // se o usuário abrir o formulário antes dela terminar, abrirFormularioRegistrar
  // precisa ter como esperar por ela também, e não só confiar que já rodou.
  let objetivoAtualPromise = null;

  function carregarObjetivoAtual() {
    objetivoAtualPromise = (async () => {
      try {
        const resposta = await ronuFetchAutenticado('/objetivos/atual');
        if (resposta.ok) {
          guardarObjetivoSalvo(await resposta.json());
        }
      } catch (erro) {
        // Sessão expirada já foi tratada (redirecionamento) dentro de
        // ronuFetchAutenticado — nada a fazer aqui além de seguir sem objetivo.
      }
    })();
    return objetivoAtualPromise;
  }

  async function iniciar() {
    mostrarSomente(elLoading);

    try {
      await Promise.all([carregarTendencia(), carregarObjetivoAtual()]);
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        ronuMostrarErroFormulario(elError, erro.message);
        mostrarSomente(elError);
      }
    } finally {
      // Começou sem pesagem (ou com erro)? O primeiro gráfico, depois de
      // registrar peso, já não é mais "a entrada da página".
      animarEntrada = false;
    }
  }

  // ---------- Registrar peso ----------
  // Tela é só de leitura, exceto por esta ação: lançar uma nova pesagem sem
  // sair da tela. POST /objetivos faz upsert por data no back-end (se já
  // existe um registro de hoje, atualiza em vez de duplicar), então não há
  // necessidade de escolher entre criar/editar aqui.

  async function abrirFormularioRegistrar() {
    ronuOcultarErroFormulario(elRegistrarError);
    elRegistrarSucesso.hidden = true;

    btnToggleRegistrar.hidden = true;
    btnToggleRegistrar.setAttribute('aria-expanded', 'true');
    formRegistrar.hidden = false;
    inputPesoRegistrar.focus();

    // Se o usuário abriu o formulário rápido demais (antes da carga inicial
    // terminar de buscar o objetivo atual), espera essa mesma requisição em
    // vez de deixar o radio sem nada pré-selecionado.
    if (objetivoAtualPromise) await objetivoAtualPromise;

    if (objetivoAtual) {
      const radioAtual = formRegistrar.querySelector(
        `input[name="peso-registrar-objetivo"][value="${objetivoAtual}"]`
      );
      if (radioAtual) radioAtual.checked = true;
    }

    // O POST substitui o registro de hoje inteiro, inclusive a aderência: se
    // já há resposta salva hoje, ela volta marcada para não ser apagada sem
    // querer ao corrigir só o peso.
    if (registradoHoje && aderenciaSalva) {
      const radioAderencia = fieldsetAderencia.querySelector(
        `input[name="peso-registrar-aderencia"][value="${aderenciaSalva}"]`
      );
      if (radioAderencia) radioAderencia.checked = true;
    }

    atualizarAderencia();
  }

  function fecharFormularioRegistrar() {
    formRegistrar.hidden = true;
    btnToggleRegistrar.hidden = false;
    btnToggleRegistrar.setAttribute('aria-expanded', 'false');
    formRegistrar.reset();
    fieldsetAderencia.hidden = true;
    elObjetivoAlterado.hidden = true;
    ronuOcultarErroFormulario(elRegistrarError);
  }

  btnToggleRegistrar.addEventListener('click', abrirFormularioRegistrar);
  btnCancelarRegistrar.addEventListener('click', fecharFormularioRegistrar);

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
  }

  formRegistrar.querySelectorAll('input[name="peso-registrar-objetivo"]').forEach((radio) => {
    radio.addEventListener('change', atualizarAderencia);
  });

  // O botão some assim que nada fica marcado (CSS) — o foco vai para a
  // primeira opção em vez de se perder junto com ele.
  btnLimparAderencia.addEventListener('click', () => {
    const opcoes = fieldsetAderencia.querySelectorAll('input[name="peso-registrar-aderencia"]');
    opcoes.forEach((radio) => { radio.checked = false; });
    opcoes[0].focus();
  });

  // Pergunta escondida = sem resposta: envia null, o que também limpa uma
  // resposta salva hoje (ex.: objetivo trocado no mesmo envio).
  function aderenciaParaEnviar() {
    if (fieldsetAderencia.hidden) return null;
    const marcada = fieldsetAderencia.querySelector('input[name="peso-registrar-aderencia"]:checked');
    return marcada ? marcada.value : null;
  }

  formRegistrar.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    if (!inputPesoRegistrar.reportValidity()) return;

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
          peso: parseFloat(inputPesoRegistrar.value),
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

      fecharFormularioRegistrar();
      elRegistrarSucesso.textContent = 'Peso registrado. A tendência se ajusta aos poucos, para refletir a direção real, não picos de um dia.';
      elRegistrarSucesso.hidden = false;

      await carregarTendencia();
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
