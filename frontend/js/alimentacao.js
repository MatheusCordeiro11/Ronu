// Ronu — Alimentação (gosto, evitar, rotina, orçamento e nomes regionais)
//
// Como no canvas aprovado, tudo é salvo na hora, sem botão Salvar:
// - Gosto / evitar: cada chip é uma PreferenciaAlimentar (POST faz upsert pelo
//   nome, sem diferenciar maiúsculas; DELETE remove). Mandar para a outra
//   lista um alimento que já está numa só troca o tipo.
// - Rotina e orçamento vivem no perfil (PUT /perfil grava todos os campos
//   juntos). A rotina salva pouco depois de parar de digitar e ao sair do
//   campo; o orçamento, no clique.

document.addEventListener('DOMContentLoaded', async () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const $ = (id) => document.getElementById(id);

  // Mesma lista de sugestões do passo Alimentação do onboarding.
  const SUGESTOES = ['frango', 'arroz', 'feijão', 'ovo', 'banana', 'aveia', 'cuscuz', 'tapioca', 'batata-doce',
    'carne moída', 'peixe', 'iogurte', 'leite', 'queijo', 'pão francês', 'camarão', 'mandioca', 'abacate'];
  const MAX_SUGESTOES = 8;

  // Enfeite da faixa do topo (não é dado: só pratos comuns no Brasil).
  const PRATOS = ['arroz e feijão', 'cuscuz com ovo', 'tapioca', 'frango grelhado', 'batata-doce', 'pão de queijo',
    'açaí', 'baião de dois', 'mandioca cozida', 'banana com aveia', 'peixe assado', 'omelete'];

  const NOMES_LISTA = { preferido: 'Gosto de comer', evitar: 'Prefiro evitar' };

  // As três faixas da regra 12 do prompt (GeradorDietaGemini). O anel mostra
  // a faixa: um terço, dois terços ou cheio.
  const ORCAMENTOS = {
    economico: { nome: 'Econômico', dica: 'Prioriza ovo, frango, peixes populares e feijão, sem cortes caros.', fracao: 1 / 3 },
    moderado: { nome: 'Moderado', dica: 'Bom senso de custo-benefício na escolha.', fracao: 2 / 3 },
    sem_restricao: { nome: 'Sem restrição', dica: 'O custo não entra na escolha dos alimentos.', fracao: 1 }
  };

  const ESTADOS = {
    AC: 'Acre', AL: 'Alagoas', AP: 'Amapá', AM: 'Amazonas', BA: 'Bahia', CE: 'Ceará', DF: 'Distrito Federal',
    ES: 'Espírito Santo', GO: 'Goiás', MA: 'Maranhão', MT: 'Mato Grosso', MS: 'Mato Grosso do Sul', MG: 'Minas Gerais',
    PA: 'Pará', PB: 'Paraíba', PR: 'Paraná', PE: 'Pernambuco', PI: 'Piauí', RJ: 'Rio de Janeiro', RN: 'Rio Grande do Norte',
    RS: 'Rio Grande do Sul', RO: 'Rondônia', RR: 'Roraima', SC: 'Santa Catarina', SP: 'São Paulo', SE: 'Sergipe', TO: 'Tocantins'
  };
  // Espelha a regra 9 do prompt (GeradorDietaGemini.MontarPrompt), como a
  // prévia do estado.js. Se uma mudar, mudar a outra também.
  const NORTE_NORDESTE = ['AC', 'AM', 'AP', 'PA', 'RO', 'RR', 'TO', 'AL', 'BA', 'CE', 'MA', 'PB', 'PE', 'PI', 'RN', 'SE'];

  const ROTINA_LIMITE = 500;
  const ESPERA_ROTINA_MS = 1000;

  let preferencias = [];
  let perfil = null;
  let mudou = false;

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto != null) el.textContent = texto;
    return el;
  }

  function normalizar(texto) {
    return texto.trim().toLowerCase();
  }

  function marcarMudanca() {
    mudou = true;
    $('al-quando').dataset.mudou = '';
    $('al-quando-titulo').textContent = 'Salvo';
    $('al-quando-texto').textContent = 'Suas mudanças foram salvas. Elas entram na próxima dieta que você gerar.';
    $('al-gerar').hidden = false;
  }

  // ---------- Faixa dos pratos ----------

  $('al-pratos').replaceChildren(...[...PRATOS, ...PRATOS].map((prato) => criar('span', null, `${prato}  ·`)));

  // ---------- Gosto / evitar ----------

  const secoes = {};
  document.querySelectorAll('.al-lista').forEach((secao) => {
    const tipo = secao.dataset.tipo;
    secoes[tipo] = {
      secao,
      form: secao.querySelector('form'),
      campo: secao.querySelector('input'),
      botao: secao.querySelector('.al-adicionar-botao'),
      aviso: secao.querySelector('.al-aviso'),
      chips: secao.querySelector('.al-chips')
    };
  });

  function avisar(tipo, texto, erro) {
    const aviso = secoes[tipo].aviso;
    aviso.textContent = texto;
    aviso.dataset.tipo = erro ? 'erro' : '';
  }

  function criarChip(preferencia) {
    const item = criar('li', 'chip chip-com-remover al-chip');
    item.dataset.id = preferencia.id;
    const remover = criar('button', 'chip-remover');
    remover.type = 'button';
    remover.setAttribute('aria-label', `Remover ${preferencia.alimento}`);
    remover.innerHTML = '<svg viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" aria-hidden="true"><path d="M3 3l6 6M9 3l-6 6"/></svg>';
    remover.addEventListener('click', () => removerPreferencia(preferencia, item));
    item.append(preferencia.alimento, remover);
    return item;
  }

  function renderizarListas() {
    Object.entries(secoes).forEach(([tipo, s]) => {
      s.chips.replaceChildren(...preferencias.filter((p) => p.tipo === tipo).map(criarChip));
    });
    renderizarSugestoes();
  }

  function renderizarSugestoes() {
    const usados = new Set(preferencias.map((p) => normalizar(p.alimento)));
    const lista = SUGESTOES.filter((nome) => !usados.has(nome)).slice(0, MAX_SUGESTOES);
    $('al-sugestoes').replaceChildren(...lista.map((nome) => {
      const botao = criar('button', 'chip chip-sugestao al-sugestao', `+ ${nome}`);
      botao.type = 'button';
      botao.setAttribute('aria-label', `Adicionar ${nome} aos que você gosta`);
      botao.addEventListener('click', () => adicionar('preferido', nome, botao));
      return botao;
    }));
  }

  async function adicionar(tipo, nomeBruto, origem) {
    const s = secoes[tipo];
    const nome = normalizar(nomeBruto);
    if (!nome) {
      s.campo.focus();
      return;
    }

    const existente = preferencias.find((p) => normalizar(p.alimento) === nome);
    if (existente && existente.tipo === tipo) {
      avisar(tipo, `“${nome}” já está nesta lista.`);
      s.campo.value = '';
      return;
    }

    avisar(tipo, '');
    // Sem ronuDefinirCarregando: ele troca o texto do .btn-label, e este tem
    // dois rótulos (desktop "Adicionar", celular "+") que se perderiam.
    if (s.botao.getAttribute('aria-busy') === 'true') return;
    s.botao.disabled = true;
    s.botao.setAttribute('aria-busy', 'true');
    if (origem) origem.disabled = true;

    try {
      const resposta = await ronuFetchAutenticado('/preferencias-alimentares', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ alimento: nome, tipo })
      });
      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível adicionar. Tente novamente.');
      }
      const salva = await resposta.json();
      // Upsert: o mesmo id volta quando o alimento já estava na outra lista.
      preferencias = [...preferencias.filter((p) => p.id !== salva.id), salva];
      renderizarListas();
      if (existente) {
        avisar(tipo, `“${nome}” saiu de “${NOMES_LISTA[existente.tipo]}” e veio para cá.`);
      }
      s.campo.value = '';
      marcarMudanca();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') avisar(tipo, erro.message, true);
    } finally {
      s.botao.disabled = false;
      s.botao.removeAttribute('aria-busy');
      // A sugestão clicada some da lista; o foco vai para o campo da seção.
      if (origem && origem.isConnected) origem.disabled = false;
      if (origem && !origem.isConnected) s.campo.focus();
    }
  }

  Object.entries(secoes).forEach(([tipo, s]) => {
    s.form.addEventListener('submit', (evento) => {
      evento.preventDefault();
      adicionar(tipo, s.campo.value);
    });
    s.campo.addEventListener('input', () => avisar(tipo, ''));
  });

  async function removerPreferencia(preferencia, item) {
    const tipo = preferencia.tipo;
    item.dataset.saindo = '';
    avisar(tipo, '');
    try {
      const resposta = await ronuFetchAutenticado(`/preferencias-alimentares/${preferencia.id}`, { method: 'DELETE' });
      if (!resposta.ok && resposta.status !== 404) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível remover. Tente novamente.');
      }
      preferencias = preferencias.filter((p) => p.id !== preferencia.id);
      renderizarListas();
      // O chip some com o botão focado: o foco vai para o campo da lista.
      secoes[tipo].campo.focus();
      marcarMudanca();
    } catch (erro) {
      delete item.dataset.saindo;
      if (erro.message !== 'Sessão expirada.') avisar(tipo, erro.message, true);
    }
  }

  // ---------- Perfil: rotina e orçamento ----------
  // PUT /perfil grava todos os campos juntos (altura, sexo e data de
  // nascimento são obrigatórios). Para não apagar o que outra aba salvou, o
  // perfil é lido de novo logo antes de cada PUT, e só o campo da seção muda
  // (solução definitiva, endpoints por seção, em docs/lacunas-conhecidas.md).

  function perfilIncompleto() {
    return perfil.altura == null || perfil.sexo == null || perfil.dataNascimento == null;
  }

  async function buscarPerfilAtual() {
    let resposta;
    try {
      resposta = await ronuFetchAutenticado('/perfil');
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') throw erro;
      throw new Error('Não foi possível salvar. Tente novamente.');
    }
    if (!resposta.ok) throw new Error('Não foi possível salvar. Tente novamente.');
    return resposta.json();
  }

  async function salvarNoPerfil(alteracoes) {
    const atual = await buscarPerfilAtual();
    const corpo = {
      altura: atual.altura,
      sexo: atual.sexo,
      dataNascimento: atual.dataNascimento,
      rotinaDiaria: atual.rotinaDiaria ?? null,
      orcamentoSemanal: atual.orcamentoSemanal ?? null,
      ...alteracoes
    };
    const resposta = await ronuFetchAutenticado('/perfil', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(corpo)
    });
    if (!resposta.ok) {
      const dados = await resposta.json().catch(() => null);
      throw new Error(dados?.mensagem || 'Não foi possível salvar. Tente novamente.');
    }
    // O PUT devolve o perfil como ficou gravado.
    perfil = await resposta.json();
  }

  function status(el, texto, tipo) {
    el.textContent = texto;
    el.dataset.tipo = tipo || '';
  }

  // Sem os dados do perfil o PUT sempre falharia: rotina e orçamento já
  // abrem travados, com o caminho para completar.
  function mostrarPerfilIncompleto() {
    const erro = $('al-perfil-erro');
    erro.textContent = 'Complete seus dados pessoais em ';
    const link = criar('a', null, 'Perfil');
    link.href = 'perfil.html';
    erro.append(link, ' para salvar sua rotina e seu orçamento.');
    erro.hidden = false;
    $('al-rotina-texto').disabled = true;
    botoesOrcamento.forEach((b) => { b.disabled = true; });
  }

  // ----- Rotina -----

  const elRotina = $('al-rotina-texto');
  const elRotinaStatus = $('al-rotina-status');
  let rotinaPendente = null;
  let rotinaSalvando = false;
  let rotinaDeNovo = false;

  function atualizarContador() {
    $('al-rotina-contador').textContent = `${elRotina.value.length}/${ROTINA_LIMITE}`;
  }

  // Vazio (ou só espaços) vira null: "sem rotina" tem um valor só no banco.
  function rotinaDoCampo() {
    return elRotina.value.trim() || null;
  }

  async function salvarRotina() {
    clearTimeout(rotinaPendente);
    if (!perfil || perfilIncompleto()) return;
    if (rotinaDoCampo() === (perfil.rotinaDiaria ?? null)) return;
    if (rotinaSalvando) { rotinaDeNovo = true; return; }

    rotinaSalvando = true;
    status(elRotinaStatus, 'Salvando...');
    try {
      await salvarNoPerfil({ rotinaDiaria: rotinaDoCampo() });
      status(elRotinaStatus, 'Salvo', 'ok');
      marcarMudanca();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') status(elRotinaStatus, erro.message, 'erro');
    } finally {
      rotinaSalvando = false;
      if (rotinaDeNovo) {
        rotinaDeNovo = false;
        salvarRotina();
      }
    }
  }

  elRotina.addEventListener('input', () => {
    atualizarContador();
    status(elRotinaStatus, '');
    clearTimeout(rotinaPendente);
    rotinaPendente = setTimeout(salvarRotina, ESPERA_ROTINA_MS);
  });
  elRotina.addEventListener('blur', salvarRotina);

  // ----- Orçamento -----

  const botoesOrcamento = Array.from($('al-faixas').querySelectorAll('[role="radio"]'));
  const elOrcamentoStatus = $('al-orcamento-status');

  function renderizarOrcamento(valor) {
    const faixa = ORCAMENTOS[valor];
    $('al-orcamento-nome').textContent = faixa ? faixa.nome : 'Não informado';
    $('al-orcamento-dica').textContent = faixa ? faixa.dica : 'Sem escolha, o Ronu usa bom senso de custo-benefício.';
    $('al-anel').style.setProperty('--anel-fim', String(1 - (faixa ? faixa.fracao : 0)));
    botoesOrcamento.forEach((b) => {
      const marcado = b.dataset.valor === valor;
      b.setAttribute('aria-checked', String(marcado));
      // Sem nada marcado, o primeiro recebe o Tab (padrão de radiogroup).
      b.tabIndex = marcado || (!faixa && b === botoesOrcamento[0]) ? 0 : -1;
    });
    // Redesenha o anel com a faixa nova.
    const grupo = document.querySelector('.al-orcamento-topo');
    if (window.RonuMovimento && grupo.classList.contains('em-cena')) window.RonuMovimento.redesenhar(grupo);
  }

  async function escolherOrcamento(botao) {
    const valor = botao.dataset.valor;
    if (!perfil || perfilIncompleto() || valor === perfil.orcamentoSemanal) return;
    const anterior = perfil.orcamentoSemanal;
    renderizarOrcamento(valor);
    status(elOrcamentoStatus, 'Salvando...');
    try {
      await salvarNoPerfil({ orcamentoSemanal: valor });
      status(elOrcamentoStatus, 'Salvo', 'ok');
      marcarMudanca();
    } catch (erro) {
      renderizarOrcamento(anterior);
      if (erro.message !== 'Sessão expirada.') status(elOrcamentoStatus, erro.message, 'erro');
    }
  }

  botoesOrcamento.forEach((botao) => botao.addEventListener('click', () => escolherOrcamento(botao)));
  $('al-faixas').addEventListener('keydown', (evento) => {
    const passo = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[evento.key];
    if (!passo) return;
    evento.preventDefault();
    const atual = Math.max(0, botoesOrcamento.indexOf(document.activeElement));
    const proximo = botoesOrcamento[(atual + passo + botoesOrcamento.length) % botoesOrcamento.length];
    proximo.focus();
    escolherOrcamento(proximo);
  });

  // ----- Nomes regionais -----

  function renderizarNomes() {
    const uf = perfil && perfil.estado;
    const mandioca = uf === 'RJ' ? 'aipim' : NORTE_NORDESTE.includes(uf) ? 'macaxeira' : 'mandioca';
    const feijao = ['PR', 'RS', 'SC'].includes(uf) ? 'feijão preto' : 'feijão';
    const texto = $('al-nomes-texto');
    texto.replaceChildren();
    if (uf && ESTADOS[uf]) {
      texto.append('Você está em ', criar('strong', null, ESTADOS[uf]), ', então o cardápio fala:');
    } else {
      texto.textContent = 'Sem estado informado, o cardápio usa os nomes mais comuns:';
    }
    $('al-nomes-chips').replaceChildren(...[mandioca, feijao].map((nome) => criar('li', 'chip chip-pequeno', nome)));
    // No celular a frase já traz os nomes (canvas AlimentacaoMobile).
    texto.append(criar('span', 'al-so-celular', ` ${mandioca}, ${feijao}.`));
  }

  // ---------- Carga ----------

  async function carregarPreferencias() {
    try {
      const resposta = await ronuFetchAutenticado('/preferencias-alimentares');
      if (!resposta.ok) throw new Error();
      preferencias = await resposta.json();
      renderizarListas();
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      Object.keys(secoes).forEach((tipo) => avisar(tipo, 'Não foi possível carregar suas preferências. Recarregue a página.', true));
    }
  }

  async function carregarPerfil() {
    try {
      const resposta = await ronuFetchAutenticado('/perfil');
      if (!resposta.ok) throw new Error();
      perfil = await resposta.json();
      elRotina.value = perfil.rotinaDiaria ?? '';
      atualizarContador();
      renderizarOrcamento(perfil.orcamentoSemanal);
      renderizarNomes();
      if (perfilIncompleto()) mostrarPerfilIncompleto();
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      const elErro = $('al-perfil-erro');
      ronuMostrarErroFormulario(elErro, 'Não foi possível carregar sua rotina e seu orçamento. Recarregue a página.');
      elRotina.disabled = true;
      botoesOrcamento.forEach((b) => { b.disabled = true; });
      renderizarOrcamento(null);
      renderizarNomes();
    }
  }

  renderizarSugestoes();
  // Seções independentes: uma falha numa não impede a outra de carregar.
  await Promise.all([carregarPreferencias(), carregarPerfil()]);
});
