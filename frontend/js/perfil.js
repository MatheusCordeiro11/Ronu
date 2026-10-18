// Ronu — Perfil (objetivo, dados do corpo, gasto em repouso, região, aparência e conta)
//
// Como nas outras telas do redesign, tudo é salvo na hora, sem botão Salvar,
// com "Salvando..." / "Salvo" à vista:
// - Objetivo: trocar pede confirmação e o peso de hoje. O POST /objetivos
//   grava a pesagem de hoje com o objetivo novo (upsert do dia), que é onde o
//   objetivo mora; o ajuste pelo progresso recomeça a contar a partir dela.
// - Sexo salva no clique; altura e nascimento, pouco depois de parar de
//   digitar (ou ao sair do campo), só quando o valor é válido.
// - Região: PUT /perfil/estado (ronuSalvarEstado, o mesmo do estado.html).
// O gasto em repouso vem do POST /metas/simulacao com o que está nos campos
// (~300 ms depois da última mudança), então mexer num campo já mostra a conta.

document.addEventListener('DOMContentLoaded', async () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const $ = (id) => document.getElementById(id);

  const NOMES_OBJETIVO = { 'perder peso': 'Perder peso', 'manter peso': 'Manter peso', 'ganhar peso': 'Ganhar peso' };
  const ESTADOS = {
    AC: 'Acre', AL: 'Alagoas', AP: 'Amapá', AM: 'Amazonas', BA: 'Bahia', CE: 'Ceará', DF: 'Distrito Federal',
    ES: 'Espírito Santo', GO: 'Goiás', MA: 'Maranhão', MT: 'Mato Grosso', MS: 'Mato Grosso do Sul', MG: 'Minas Gerais',
    PA: 'Pará', PB: 'Paraíba', PR: 'Paraná', PE: 'Pernambuco', PI: 'Piauí', RJ: 'Rio de Janeiro', RN: 'Rio Grande do Norte',
    RS: 'Rio Grande do Sul', RO: 'Rondônia', RR: 'Roraima', SC: 'Santa Catarina', SP: 'São Paulo', SE: 'Sergipe', TO: 'Tocantins'
  };
  // Espelha a regra 9 do prompt e o FusoHorarioEstado, como a prévia do
  // estado.js. Se uma regra mudar, mudar aqui também.
  const NORTE_NORDESTE = ['AC', 'AM', 'AP', 'PA', 'RO', 'RR', 'TO', 'AL', 'BA', 'CE', 'MA', 'PB', 'PE', 'PI', 'RN', 'SE'];
  const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

  const ESPERA_SALVAR_MS = 800;
  const ESPERA_SIMULAR_MS = 300;
  // Escala das barras do gasto em repouso (a maior parcela fica perto daqui).
  const ESCALA_PARTES = 1200;

  const elLoading = $('perfil-loading');
  const elLoadError = $('perfil-load-error');
  const elContent = $('perfil-content');

  let perfil = null;
  let objetivoAtual = null;   // GET /objetivos/atual
  let tendencia = [];
  let simulacao = null;

  // ---------- Utilitários ----------

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto != null) el.textContent = texto;
    return el;
  }

  function formatarNumero(valor) {
    return Math.round(valor).toLocaleString('pt-BR');
  }

  function formatarKg(valor) {
    return Number(valor).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  }

  function dataIso(d) {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  function status(el, texto, tipo) {
    el.textContent = texto;
    el.dataset.tipo = tipo || '';
  }

  function marcarMudanca() {
    $('pf-quando').dataset.mudou = '';
    $('pf-quando-titulo').textContent = 'Salvo';
    $('pf-quando-texto').textContent = 'Pronto. A próxima dieta gerada já usa os dados novos.';
    $('pf-gerar').hidden = false;
  }

  function mostrarSomente(elementoVisivel) {
    [elLoading, elLoadError, elContent].forEach((el) => {
      el.hidden = el !== elementoVisivel;
    });
  }

  // ---------- Cabeçalho ----------

  function renderizarIdentidade() {
    const nome = perfil.nome || usuario.nome || '';
    $('pf-nome').textContent = nome || 'Seu perfil';
    $('pf-iniciais').textContent = nome ? ronuIniciais(nome) : '';
    $('pf-email').textContent = perfil.email || '';
    $('pf-google-chip').hidden = !perfil.temGoogle;
  }

  // ---------- Radiogroups simples (sexo e objetivo) ----------
  // Clique escolhe; setas andam e escolhem (padrão de radiogroup).

  function marcarRadio(grupo, valor) {
    const botoes = Array.from(grupo.querySelectorAll('[role="radio"]'));
    botoes.forEach((b) => {
      const marcado = b.dataset.valor === valor;
      b.setAttribute('aria-checked', String(marcado));
      b.tabIndex = marcado || (!valor && b === botoes[0]) ? 0 : -1;
    });
  }

  function ligarRadio(grupo, aoEscolher) {
    const botoes = Array.from(grupo.querySelectorAll('[role="radio"]'));
    botoes.forEach((b) => b.addEventListener('click', () => aoEscolher(b.dataset.valor, b)));
    grupo.addEventListener('keydown', (evento) => {
      const passo = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[evento.key];
      if (!passo) return;
      evento.preventDefault();
      const atual = Math.max(0, botoes.indexOf(document.activeElement));
      const proximo = botoes[(atual + passo + botoes.length) % botoes.length];
      proximo.focus();
      aoEscolher(proximo.dataset.valor, proximo);
    });
  }

  // ---------- Objetivo ----------

  const elObjetivos = $('pf-objetivos');
  const elConfirmar = $('pf-confirmar');
  const elPesoHoje = $('pf-peso-hoje');
  const btnTrocar = $('pf-confirmar-trocar');
  const btnManter = $('pf-confirmar-manter');
  const elObjetivoStatus = $('pf-objetivo-status');
  let objetivoPendente = null;

  // "Desde 2 set": a pesagem mais antiga da sequência atual com o mesmo
  // objetivo (a mesma volta que a meta adaptativa dá até a última troca).
  function renderizarDesde() {
    const objetivo = objetivoAtual && objetivoAtual.objetivo;
    const ordenada = [...tendencia].sort((a, b) => new Date(a.data) - new Date(b.data));
    let inicio = null;
    for (let i = ordenada.length - 1; i >= 0 && ordenada[i].objetivo === objetivo; i--) inicio = ordenada[i];
    if (!inicio) { $('pf-objetivo-desde').textContent = ''; return; }
    const d = new Date(inicio.data);
    $('pf-objetivo-desde').textContent = `Desde ${d.getDate()} ${MESES[d.getMonth()]}${d.getFullYear() !== new Date().getFullYear() ? ` ${d.getFullYear()}` : ''}`;
  }

  function renderizarObjetivo() {
    marcarRadio(elObjetivos, objetivoPendente || (objetivoAtual && objetivoAtual.objetivo));
    renderizarDesde();
  }

  function abrirConfirmacao(valor) {
    objetivoPendente = valor;
    renderizarObjetivo();
    const atual = NOMES_OBJETIVO[objetivoAtual.objetivo].toLowerCase();
    btnTrocar.querySelector('.btn-label').textContent = `Trocar para ${NOMES_OBJETIVO[valor].toLowerCase()}`;
    btnManter.textContent = `Manter ${atual}`;
    // Já pesou hoje: o peso de hoje vem preenchido (o POST corrige o registro
    // de hoje); senão fica vazio, com a última pesagem de referência.
    elPesoHoje.value = objetivoAtual.registradoHoje ? String(objetivoAtual.peso) : '';
    elPesoHoje.placeholder = formatarKg(objetivoAtual.peso);
    ronuLimparCampoInvalido($('pf-campo-peso'), $('pf-peso-erro'));
    ronuOcultarErroFormulario($('pf-objetivo-erro'));
    status(elObjetivoStatus, '');
    elConfirmar.hidden = false;
  }

  function fecharConfirmacao(focar) {
    objetivoPendente = null;
    elConfirmar.hidden = true;
    renderizarObjetivo();
    if (focar) elObjetivos.querySelector('[aria-checked="true"]').focus();
  }

  ligarRadio(elObjetivos, (valor) => {
    if (!objetivoAtual) return;
    if (valor === objetivoAtual.objetivo) fecharConfirmacao(false);
    else abrirConfirmacao(valor);
  });

  btnManter.addEventListener('click', () => fecharConfirmacao(true));
  elPesoHoje.addEventListener('input', () => ronuLimparCampoInvalido($('pf-campo-peso'), $('pf-peso-erro')));

  elConfirmar.addEventListener('submit', async (evento) => {
    evento.preventDefault();
    const peso = parseFloat(elPesoHoje.value);
    if (!(peso >= 20 && peso <= 300)) {
      ronuMarcarCampoInvalido($('pf-campo-peso'), $('pf-peso-erro'), 'Informe um peso entre 20 e 300 kg.');
      elPesoHoje.focus();
      return;
    }

    const novo = objetivoPendente;
    ronuOcultarErroFormulario($('pf-objetivo-erro'));
    ronuDefinirCarregando(btnTrocar, true, 'Salvando...');
    try {
      // Aderência vai nula: ela descreve o período do objetivo antigo, e a
      // pergunta volta na próxima pesagem (mesma regra do Progresso).
      const resposta = await ronuFetchAutenticado('/objetivos', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ peso, objetivo: novo, aderencia: null })
      });
      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível trocar o objetivo. Tente novamente.');
      }
      objetivoAtual = await resposta.json();
      await carregarTendencia();
      ronuDefinirCarregando(btnTrocar, false);
      fecharConfirmacao(true);
      renderizarPeso();
      status(elObjetivoStatus, `Objetivo trocado para ${NOMES_OBJETIVO[novo].toLowerCase()}.`, 'ok');
      marcarMudanca();
      agendarSimulacao();
    } catch (erro) {
      ronuDefinirCarregando(btnTrocar, false);
      if (erro.message !== 'Sessão expirada.') ronuMostrarErroFormulario($('pf-objetivo-erro'), erro.message);
    }
  });

  // ---------- Seus dados ----------

  const elSexo = $('pf-sexo');
  const elAltura = $('pf-altura');
  const elNascimento = $('pf-nascimento');
  const elDadosStatus = $('pf-dados-status');
  let sexoEscolhido = null;
  let salvarPendente = null;
  let salvando = false;
  let salvarDeNovo = false;

  // Faixa de idade da API (RegrasDadosCorporais): 18 a 100 anos.
  const hoje = new Date();
  const nascimentoMax = dataIso(new Date(hoje.getFullYear() - 18, hoje.getMonth(), hoje.getDate()));
  const nascimentoMin = dataIso(new Date(hoje.getFullYear() - 100, hoje.getMonth(), hoje.getDate()));
  elNascimento.max = nascimentoMax;
  elNascimento.min = nascimentoMin;

  // Devolve { altura, sexo, dataNascimento } ou null, marcando o campo errado.
  // Só marca erro quando "mostrarErros" (ao salvar), não a cada tecla.
  function dadosDosCampos(mostrarErros) {
    const altura = Number(elAltura.value);
    let valido = true;
    if (!elAltura.value || !Number.isInteger(altura) || altura < 100 || altura > 250) {
      if (mostrarErros) ronuMarcarCampoInvalido($('pf-campo-altura'), $('pf-altura-erro'), 'Informe uma altura entre 100 e 250 cm.');
      valido = false;
    }
    const nascimento = elNascimento.value;
    let erroNascimento = null;
    if (!nascimento) erroNascimento = 'Informe sua data de nascimento.';
    else if (nascimento > nascimentoMax) erroNascimento = 'O Ronu é para maiores de 18 anos.';
    else if (nascimento < nascimentoMin) erroNascimento = 'Informe uma data de nascimento de até 100 anos atrás.';
    if (erroNascimento) {
      if (mostrarErros) ronuMarcarCampoInvalido($('pf-campo-nascimento'), $('pf-nascimento-erro'), erroNascimento);
      valido = false;
    }
    if (!sexoEscolhido) valido = false;
    return valido ? { altura, sexo: sexoEscolhido, dataNascimento: nascimento } : null;
  }

  function igualAoSalvo(dados) {
    return Number(perfil.altura) === dados.altura && perfil.sexo === dados.sexo && perfil.dataNascimento === dados.dataNascimento;
  }

  // PUT /perfil grava todos os campos juntos: rotina e orçamento (editados
  // na Alimentação) vêm de um GET feito na hora, para não apagar o que outra
  // aba salvou (solução definitiva em docs/lacunas-conhecidas.md).
  async function salvarDados() {
    clearTimeout(salvarPendente);
    const dados = dadosDosCampos(true);
    if (!dados || igualAoSalvo(dados)) return;
    if (salvando) { salvarDeNovo = true; return; }

    salvando = true;
    status(elDadosStatus, 'Salvando...');
    try {
      const respostaAtual = await ronuFetchAutenticado('/perfil');
      if (!respostaAtual.ok) throw new Error('Não foi possível salvar. Tente novamente.');
      const atual = await respostaAtual.json();
      const resposta = await ronuFetchAutenticado('/perfil', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...dados,
          rotinaDiaria: atual.rotinaDiaria ?? null,
          orcamentoSemanal: atual.orcamentoSemanal ?? null
        })
      });
      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível salvar. Tente novamente.');
      }
      perfil = await resposta.json();
      status(elDadosStatus, 'Salvo', 'ok');
      marcarMudanca();
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      status(elDadosStatus, erro.message === 'Failed to fetch' ? 'Não foi possível salvar. Tente novamente.' : erro.message, 'erro');
    } finally {
      salvando = false;
      if (salvarDeNovo) {
        salvarDeNovo = false;
        salvarDados();
      }
    }
  }

  function aoMudarDados() {
    status(elDadosStatus, '');
    agendarSimulacao();
    clearTimeout(salvarPendente);
    salvarPendente = setTimeout(salvarDados, ESPERA_SALVAR_MS);
  }

  ligarRadio(elSexo, (valor) => {
    if (valor === sexoEscolhido) return;
    sexoEscolhido = valor;
    marcarRadio(elSexo, valor);
    agendarSimulacao();
    salvarDados();
  });

  [[elAltura, 'pf-campo-altura', 'pf-altura-erro'], [elNascimento, 'pf-campo-nascimento', 'pf-nascimento-erro']].forEach(([campo, bloco, erro]) => {
    campo.addEventListener('input', () => {
      ronuLimparCampoInvalido($(bloco), $(erro));
      aoMudarDados();
    });
    campo.addEventListener('blur', salvarDados);
  });

  function renderizarDados() {
    sexoEscolhido = perfil.sexo;
    marcarRadio(elSexo, perfil.sexo);
    elAltura.value = perfil.altura != null ? String(Math.round(perfil.altura)) : '';
    elNascimento.value = perfil.dataNascimento ?? '';
  }

  function renderizarPeso() {
    const el = $('pf-peso');
    el.replaceChildren();
    const peso = simulacao ? simulacao.entradas.peso : objetivoAtual ? objetivoAtual.peso : null;
    if (peso == null) return;
    el.append('Peso: ', criar('strong', 'num', `${formatarKg(peso)} kg`),
      simulacao && simulacao.entradas.pesoOrigem === 'tendencia' ? ', a tendência das suas pesagens' : ', da sua última pesagem');
  }

  // ---------- Gasto em repouso (simulação) ----------

  let simulacaoPendente = null;
  let pedidoSimulacao = 0;

  function agendarSimulacao() {
    clearTimeout(simulacaoPendente);
    simulacaoPendente = setTimeout(simular, ESPERA_SIMULAR_MS);
  }

  // Manda só os campos válidos; o que faltar a API completa com o salvo.
  async function simular() {
    const numero = ++pedidoSimulacao;
    const corpo = {};
    const altura = Number(elAltura.value);
    if (Number.isInteger(altura) && altura >= 100 && altura <= 250) corpo.altura = altura;
    if (sexoEscolhido) corpo.sexo = sexoEscolhido;
    const nascimento = elNascimento.value;
    if (nascimento && nascimento <= nascimentoMax && nascimento >= nascimentoMin) corpo.dataNascimento = nascimento;
    try {
      const resposta = await ronuFetchAutenticado('/metas/simulacao', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(corpo)
      });
      if (!resposta.ok) throw new Error();
      const dados = await resposta.json();
      if (numero !== pedidoSimulacao) return;
      simulacao = dados;
    } catch (erro) {
      if (erro.message === 'Sessão expirada.' || numero !== pedidoSimulacao) return;
      simulacao = null;
    }
    renderizarRepouso();
    renderizarPeso();
  }

  function renderizarRepouso() {
    const elPartes = $('pf-partes');
    if (!simulacao) {
      $('pf-repouso-total').textContent = '—';
      elPartes.replaceChildren();
      return;
    }
    const g = simulacao.gastoRepouso;
    const e = simulacao.entradas;
    $('pf-repouso-total').textContent = formatarNumero(g.total);
    const partes = [
      [`Peso · 10 × ${formatarKg(e.peso)} kg`, g.peso],
      [`Altura · 6,25 × ${formatarNumero(e.altura)} cm`, g.altura],
      [`Idade · 5 × ${e.idade} anos`, g.idade],
      [`Sexo · ${e.sexo === 'Feminino' ? 'feminino' : 'masculino'}`, g.sexo]
    ];
    elPartes.replaceChildren(...partes.map(([rotulo, valor], i) => {
      const linha = criar('div', 'pf-parte');
      const texto = criar('div', 'pf-parte-texto');
      texto.append(criar('span', null, rotulo), criar('span', 'num', `${valor < 0 ? '−' : '+'} ${formatarNumero(Math.abs(valor))}`));
      const trilho = criar('div', 'trilho');
      trilho.setAttribute('aria-hidden', 'true');
      const barra = criar('span', `preenche${valor < 0 ? ' preenche-negativo' : ''}`);
      barra.dataset.anima = 'barra-h';
      barra.style.width = `${Math.min(100, (Math.abs(valor) / ESCALA_PARTES) * 100).toFixed(1)}%`;
      barra.style.setProperty('--atraso', `${i * 120}ms`);
      trilho.appendChild(barra);
      linha.append(texto, trilho);
      return linha;
    }));
  }

  // ---------- Região ----------

  const elRegiaoForm = $('pf-regiao-form');
  const elRegiaoSelect = $('pf-regiao-select');
  const btnRegiaoAlterar = $('pf-regiao-alterar');

  elRegiaoSelect.replaceChildren(
    criar('option', null, 'Escolha o estado'),
    ...Object.entries(ESTADOS).sort((a, b) => a[1].localeCompare(b[1], 'pt-BR')).map(([uf, nome]) => {
      const opcao = criar('option', null, nome);
      opcao.value = uf;
      return opcao;
    })
  );
  elRegiaoSelect.options[0].value = '';
  elRegiaoSelect.options[0].disabled = true;

  function renderizarRegiao() {
    const uf = perfil.estado;
    const nome = ESTADOS[uf];
    $('pf-regiao-nome').textContent = nome || 'Não informado';
    btnRegiaoAlterar.textContent = nome ? 'Alterar' : 'Informar';
    if (!nome) {
      $('pf-regiao-detalhe').textContent = 'Sem estado, o fuso é o de Brasília e o cardápio usa os nomes mais comuns.';
      return;
    }
    const fuso = uf === 'AC' ? 'Fuso do Acre (UTC−5)'
      : ['AM', 'RO', 'RR', 'MT', 'MS'].includes(uf) ? 'Fuso do Amazonas (UTC−4)' : 'Fuso de Brasília (UTC−3)';
    const mandioca = uf === 'RJ' ? 'aipim' : NORTE_NORDESTE.includes(uf) ? 'macaxeira' : 'mandioca';
    const feijao = ['PR', 'RS', 'SC'].includes(uf) ? 'feijão preto' : 'feijão';
    $('pf-regiao-detalhe').textContent = `${fuso} · ${mandioca}, ${feijao}`;
  }

  function abrirRegiao(abrir) {
    elRegiaoForm.hidden = !abrir;
    btnRegiaoAlterar.setAttribute('aria-expanded', String(abrir));
    ronuOcultarErroFormulario($('pf-regiao-erro'));
    if (abrir) {
      elRegiaoSelect.value = perfil.estado || '';
      elRegiaoSelect.focus();
    }
  }

  btnRegiaoAlterar.addEventListener('click', () => abrirRegiao(elRegiaoForm.hidden));
  $('pf-regiao-cancelar').addEventListener('click', () => {
    abrirRegiao(false);
    btnRegiaoAlterar.focus();
  });

  elRegiaoForm.addEventListener('submit', async (evento) => {
    evento.preventDefault();
    const uf = elRegiaoSelect.value;
    if (!uf) {
      ronuMostrarErroFormulario($('pf-regiao-erro'), 'Escolha o seu estado.');
      return;
    }
    if (uf === perfil.estado) {
      abrirRegiao(false);
      btnRegiaoAlterar.focus();
      return;
    }
    const btn = $('pf-regiao-salvar');
    ronuDefinirCarregando(btn, true, 'Salvando...');
    try {
      await ronuSalvarEstado(uf);
      perfil = { ...perfil, estado: uf };
      renderizarRegiao();
      abrirRegiao(false);
      btnRegiaoAlterar.focus();
      marcarMudanca();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') ronuMostrarErroFormulario($('pf-regiao-erro'), erro.message);
    } finally {
      ronuDefinirCarregando(btn, false);
    }
  });

  // ---------- Conta ----------

  function itemConta(titulo, texto, acao) {
    const li = criar('li');
    const corpo = acao ? criar('button', 'pf-conta-item') : criar('div', 'pf-conta-item pf-conta-info');
    if (acao) {
      corpo.type = 'button';
      corpo.addEventListener('click', acao);
    }
    const textos = criar('span', 'pf-conta-textos');
    textos.append(criar('span', 'pf-conta-item-titulo', titulo), criar('span', 'apoio', texto));
    corpo.appendChild(textos);
    if (acao) {
      const seta = criar('span', 'pf-conta-seta', '›');
      seta.setAttribute('aria-hidden', 'true');
      corpo.appendChild(seta);
    }
    li.appendChild(corpo);
    return li;
  }

  function renderizarConta() {
    const itens = [];
    if (perfil.temSenha) {
      // O Esqueci a senha manda o link por email; o email da conta já vai
      // preenchido (a mesma chave que o login usa).
      itens.push(itemConta('Alterar senha', 'Enviamos um link para o seu email. Ao trocar, você sai de todos os aparelhos.', () => {
        sessionStorage.setItem(RONU_EMAIL_REDEFINICAO_KEY, perfil.email || '');
        window.location.href = 'esqueci-senha.html';
      }));
    } else {
      itens.push(itemConta('Senha', 'Você entra pelo Google; esta conta não tem senha.'));
    }
    // Só mostra a situação: ligar ou desligar o Google não existe na API.
    itens.push(itemConta('Login com Google', perfil.temGoogle ? `Conectado como ${perfil.email}` : 'Não conectado. Você entra com email e senha.'));
    itens.push(itemConta('Sair', 'Encerra a sessão neste aparelho', () => {
      ronuLimparSessao();
      window.location.href = 'login.html';
    }));
    $('pf-conta-lista').replaceChildren(...itens);
  }

  // ---------- Carga ----------

  async function carregarTendencia() {
    try {
      const resposta = await ronuFetchAutenticado('/objetivos/tendencia');
      tendencia = resposta.ok ? await resposta.json() : [];
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') throw erro;
      tendencia = [];
    }
  }

  async function iniciar() {
    mostrarSomente(elLoading);
    try {
      const [respostaPerfil, respostaObjetivo] = await Promise.all([
        ronuFetchAutenticado('/perfil'),
        ronuFetchAutenticado('/objetivos/atual'),
        carregarTendencia()
      ]);
      if (!respostaPerfil.ok) throw new Error('Falha ao carregar.');
      perfil = await respostaPerfil.json();
      objetivoAtual = respostaObjetivo.ok ? await respostaObjetivo.json() : null;

      renderizarIdentidade();
      renderizarObjetivo();
      $('pf-objetivos').querySelectorAll('[role="radio"]').forEach((b) => { b.disabled = !objetivoAtual; });
      renderizarDados();
      renderizarRegiao();
      renderizarConta();
      renderizarPeso();
      mostrarSomente(elContent);
      // O sol/lua e as opções de tema foram desenhados agora: acerta as marcas.
      if (window.RonuTema) window.RonuTema.atualizar();
      await simular();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') mostrarSomente(elLoadError);
    }
  }

  $('btn-tentar-novamente').addEventListener('click', iniciar);

  await iniciar();
});
