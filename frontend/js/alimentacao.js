// Ronu — Alimentação (gosto, evitar, horários, orçamento e nomes regionais)
//
// Como no canvas aprovado, tudo é salvo na hora, sem botão Salvar:
// - Gosto / evitar: cada chip é uma PreferenciaAlimentar (POST faz upsert pelo
//   nome, sem diferenciar maiúsculas; DELETE remove). Mandar para a outra
//   lista um alimento que já está numa só troca o tipo.
// - Horários (acordar e dormir) e orçamento vivem no perfil (PUT /perfil grava
//   todos os campos juntos). Os horários salvam ao sair do campo ou pouco
//   depois de mudar, nunca no meio da digitação; o orçamento, no clique.

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

  const ESPERA_HORARIO_MS = 800;

  let preferencias = [];
  let perfil = null;
  // Para os avisos de treino fora da janela acordado: nome, início e duração de cada modalidade.
  let modalidades = [];
  let mudou = false;

  // Conferência das preferências contra a base nutricional (fase 2b-3): o que
  // cada uma casa, o que não casou e se a lista que sobra atende aos mínimos.
  // Vem pronta da API (GET /preferencias-alimentares/conferencia): a tela não
  // repete a regra. Sem conferência (erro), nenhum sinal: nunca acusar sem certeza.
  let conferidas = new Map();
  let listaMinimos = null;
  let painelAberto = null;
  // Chips já mostrados: só o chip novo tem a animação de entrada (abrir o painel ou receber a
  // conferência redesenha a lista, e os outros não podem "pular" de novo).
  const chipsVistos = new Set();

  // Rótulos dos grupos de troca da base (GrupoDeTroca), para agrupar o que um "evitar" tira.
  const GRUPOS = {
    CarnesEPeixes: 'Carnes, aves e peixes', Ovos: 'Ovos', Leguminosas: 'Leguminosas', LeiteEIogurte: 'Leite e iogurte',
    Queijos: 'Queijos', Carboidratos: 'Pães, cereais e tubérculos', Frutas: 'Frutas', Hortalicas: 'Hortaliças',
    OleaginosasESementes: 'Castanhas e sementes', OleosEGorduras: 'Óleos e gorduras', Adocantes: 'Açúcares e mel',
    CafesEChas: 'Cafés e chás', SucosERefrigerantes: 'Sucos e refrigerantes', BebidasAlcoolicas: 'Bebidas alcoólicas'
  };
  const AGRUPAR_ACIMA_DE = 8;

  // Sinal do chip que não casou: círculo vazado cortado na diagonal ("não encontrado"), no traço do
  // botão de remover, para não depender só da cor.
  const SINAL_SEM_CASAMENTO = '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" aria-hidden="true"><circle cx="7" cy="7" r="5.25"/><path d="M3.3 10.7l7.4-7.4"/></svg>';

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
      chips: secao.querySelector('.al-chips'),
      conferencia: secao.querySelector('.al-conferencia')
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
    if (!chipsVistos.has(preferencia.id)) {
      item.classList.add('al-chip-novo');
      chipsVistos.add(preferencia.id);
    }
    const remover = criar('button', 'chip-remover');
    remover.type = 'button';
    remover.setAttribute('aria-label', `Remover ${preferencia.alimento}`);
    remover.innerHTML = '<svg viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" aria-hidden="true"><path d="M3 3l6 6M9 3l-6 6"/></svg>';
    remover.addEventListener('click', () => removerPreferencia(preferencia, item));

    const conferida = conferidas.get(preferencia.id);
    if (conferida && !conferida.casou) {
      // Não casou com a base: o sinal e um texto escondido para leitor de tela; a frase fica abaixo da lista.
      item.classList.add('al-chip-sem-casamento');
      const sinal = criar('span', 'al-chip-sinal');
      sinal.innerHTML = SINAL_SEM_CASAMENTO;
      item.append(sinal, preferencia.alimento, criar('span', 'oculto-visual', ' (não está entre os alimentos do Ronu)'), remover);
    } else if (conferida && preferencia.tipo === 'evitar') {
      // "Evitar" que casou: o chip vira o botão que abre o que ele tira do cardápio.
      const total = conferida.alimentos.length;
      const abrir = criar('button', 'al-chip-abrir');
      abrir.type = 'button';
      abrir.setAttribute('aria-expanded', String(painelAberto === preferencia.id));
      abrir.setAttribute('aria-controls', 'al-painel');
      abrir.setAttribute('aria-label', `${preferencia.alimento}, tira ${total} ${total === 1 ? 'alimento' : 'alimentos'} do cardápio`);
      abrir.append(criar('span', null, preferencia.alimento), criar('span', 'al-chip-tira num', `tira ${total}`));
      abrir.addEventListener('click', () => {
        painelAberto = painelAberto === preferencia.id ? null : preferencia.id;
        renderizarListas();
        secoes.evitar.chips.querySelector(`[data-id="${preferencia.id}"] .al-chip-abrir`)?.focus();
      });
      item.append(abrir, remover);
    } else {
      item.append(preferencia.alimento, remover);
    }
    return item;
  }

  function renderizarListas() {
    Object.entries(secoes).forEach(([tipo, s]) => {
      s.chips.replaceChildren(...preferencias.filter((p) => p.tipo === tipo).map(criarChip));
      renderizarConferencia(tipo);
    });
    renderizarPainel();
    renderizarListaPobre();
    renderizarSugestoes();
  }

  // Abaixo de cada lista: o que não casou (com o nome) e o que foi lido pela tolerância a 1 letra.
  function renderizarConferencia(tipo) {
    const daLista = preferencias.filter((p) => p.tipo === tipo).map((p) => [p, conferidas.get(p.id)]).filter(([, c]) => c);
    const linhas = [];
    const semCasamento = daLista.filter(([, c]) => !c.casou).map(([p]) => `“${p.alimento}”`);
    if (semCasamento.length) {
      const nomes = semCasamento.length === 1 ? semCasamento[0] : `${semCasamento.slice(0, -1).join(', ')} e ${semCasamento[semCasamento.length - 1]}`;
      const um = semCasamento.length === 1;
      const linha = criar('p', 'al-conferencia-linha');
      const sinal = criar('span', 'al-chip-sinal');
      sinal.innerHTML = SINAL_SEM_CASAMENTO;
      if (tipo === 'evitar') {
        // "Evitar" é segurança: no acento, e com o caminho para corrigir.
        linha.dataset.tipo = 'evitar';
        linha.append(sinal, `Não achamos ${nomes} entre os alimentos do Ronu. Se for outro nome de um alimento, escreva de outro jeito.`);
      } else {
        linha.append(sinal, `${nomes} ${um ? 'não está' : 'não estão'} entre os alimentos do Ronu: ${um ? 'não vai' : 'não vão'} aparecer no cardápio.`);
      }
      linhas.push(linha);
    }
    daLista.filter(([, c]) => c.casou && c.lidoComo.length).forEach(([p, c]) => {
      linhas.push(criar('p', 'al-conferencia-linha', `“${p.alimento}” foi lido como ${c.lidoComo.join(' e ')}.`));
    });
    secoes[tipo].conferencia.replaceChildren(...linhas);
  }

  // O que o "evitar" aberto tira do cardápio. Acima de 8 itens, agrupado pelo grupo de troca.
  function renderizarPainel() {
    const painel = $('al-painel');
    const preferencia = preferencias.find((p) => p.id === painelAberto && p.tipo === 'evitar');
    const conferida = preferencia && conferidas.get(preferencia.id);
    if (!conferida || !conferida.casou) {
      painelAberto = null;
      painel.hidden = true;
      painel.replaceChildren();
      return;
    }

    const total = conferida.alimentos.length;
    const cabeca = criar('p', 'al-painel-cabeca');
    cabeca.append(criar('strong', null, `“${preferencia.alimento}”`), ` tira ${total} ${total === 1 ? 'alimento' : 'alimentos'} do cardápio`);
    if (conferida.lidoComo.length) cabeca.append(criar('span', 'al-painel-lido', ` · lido como ${conferida.lidoComo.join(' e ')}`));

    let corpo;
    if (total > AGRUPAR_ACIMA_DE) {
      corpo = criar('dl', 'al-painel-grupos');
      const porGrupo = new Map();
      conferida.alimentos.forEach((a) => porGrupo.set(a.grupo, [...(porGrupo.get(a.grupo) || []), a.nome]));
      porGrupo.forEach((nomes, grupo) => {
        corpo.append(criar('dt', null, GRUPOS[grupo] || grupo), criar('dd', null, nomes.join(', ')));
      });
    } else {
      corpo = criar('p', 'al-painel-lista', `${conferida.alimentos.map((a) => a.nome).join(', ')}.`);
    }
    painel.replaceChildren(cabeca, corpo);
    painel.hidden = false;
  }

  // Lista pobre, antes de gerar: nota abaixo do "evitar"; papel vazio, no tom de erro (a dieta não sai).
  function renderizarListaPobre() {
    const el = $('al-lista-pobre');
    if (!listaMinimos || listaMinimos.atende) {
      el.hidden = true;
      el.replaceChildren();
      return;
    }
    const texto = ronuTextoListaPobre({
      papeis: listaMinimos.abaixoDoMinimo,
      itensPorPapel: listaMinimos.itensPorPapel,
      gruposDeProteina: listaMinimos.gruposDeProteina,
      vazios: listaMinimos.vazios
    });
    const vazio = listaMinimos.vazios.length > 0;
    el.replaceChildren(criar('p', vazio ? 'aviso aviso-erro' : 'nota nota-forte', texto));
    el.hidden = false;
  }

  async function conferir() {
    try {
      const resposta = await ronuFetchAutenticado('/preferencias-alimentares/conferencia');
      if (!resposta.ok) throw new Error();
      const dados = await resposta.json();
      conferidas = new Map(dados.preferencias.map((c) => [c.id, c]));
      listaMinimos = dados.lista;
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      conferidas = new Map();
      listaMinimos = null;
    }
    renderizarListas();
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

  const adicionar = ronuComGravacao(async (tipo, nomeBruto, origem) => {
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
      conferir();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') avisar(tipo, erro.message, true);
    } finally {
      s.botao.disabled = false;
      s.botao.removeAttribute('aria-busy');
      // A sugestão clicada some da lista; o foco vai para o campo da seção.
      if (origem && origem.isConnected) origem.disabled = false;
      if (origem && !origem.isConnected) s.campo.focus();
    }
  });

  Object.entries(secoes).forEach(([tipo, s]) => {
    s.form.addEventListener('submit', (evento) => {
      evento.preventDefault();
      adicionar(tipo, s.campo.value);
    });
    s.campo.addEventListener('input', () => avisar(tipo, ''));
  });

  const removerPreferencia = ronuComGravacao(async (preferencia, item) => {
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
      conferir();
    } catch (erro) {
      delete item.dataset.saindo;
      if (erro.message !== 'Sessão expirada.') avisar(tipo, erro.message, true);
    }
  });

  // ---------- Perfil: horários e orçamento ----------
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
      // Sem rotinaDiaria: a API ignora o campo desde os horários estruturados.
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

  // Sem os dados do perfil o PUT sempre falharia: horários e orçamento já
  // abrem travados, com o caminho para completar.
  function mostrarPerfilIncompleto() {
    const erro = $('al-perfil-erro');
    erro.textContent = 'Complete seus dados pessoais em ';
    const link = criar('a', null, 'Perfil');
    link.href = 'perfil.html';
    erro.append(link, ' para salvar seus horários e seu orçamento.');
    erro.hidden = false;
    Object.values(camposHorario).forEach((el) => { el.disabled = true; });
    botoesOrcamento.forEach((b) => { b.disabled = true; });
  }

  // ----- Horários -----
  // Ausente no PUT mantém o horário salvo, e vazio apaga (RegrasHorario, na API): vai só o que mudou. O campo
  // de hora só muda o valor com a hora completa; a espera cobre quem ainda vai mexer nos minutos.

  const camposHorario = { horarioAcordar: $('al-acordo'), horarioDormir: $('al-durmo') };
  const elHorariosStatus = $('al-horarios-status');
  let horarioPendente = null;
  let horarioSalvando = false;
  let horarioDeNovo = false;

  // Avisos que não bloqueiam (js/base/horarios.js), montados só com textContent.
  function renderizarAvisos() {
    const lista = RonuHorarios.avisos(camposHorario.horarioAcordar.value, camposHorario.horarioDormir.value, modalidades);
    $('al-horarios-aviso').replaceChildren(...lista.map((texto) => criar('p', 'nota', texto)));
  }

  function horariosAlterados() {
    const alteracoes = {};
    Object.entries(camposHorario).forEach(([campo, el]) => {
      if (el.value !== (perfil[campo] ?? '')) alteracoes[campo] = el.value;
    });
    return alteracoes;
  }

  const salvarHorarios = ronuComGravacao(async () => {
    clearTimeout(horarioPendente);
    if (!perfil || perfilIncompleto()) return;
    const alteracoes = horariosAlterados();
    if (Object.keys(alteracoes).length === 0) return;
    if (horarioSalvando) { horarioDeNovo = true; return; }

    horarioSalvando = true;
    status(elHorariosStatus, 'Salvando...');
    try {
      await salvarNoPerfil(alteracoes);
      status(elHorariosStatus, 'Salvo', 'ok');
      marcarMudanca();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') status(elHorariosStatus, erro.message, 'erro');
    } finally {
      horarioSalvando = false;
      if (horarioDeNovo) {
        horarioDeNovo = false;
        salvarHorarios();
      }
    }
  });

  Object.values(camposHorario).forEach((el) => {
    el.addEventListener('change', () => {
      renderizarAvisos();
      status(elHorariosStatus, '');
      clearTimeout(horarioPendente);
      horarioPendente = setTimeout(salvarHorarios, ESPERA_HORARIO_MS);
    });
    el.addEventListener('blur', salvarHorarios);
  });

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

  const escolherOrcamento = ronuComGravacao(async (botao) => {
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
  });

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

  // ----- Sua rotina antiga -----
  // O texto livre de antes dos horários estruturados não é mais usado (LGPD base, etapa 3). O cartão aparece
  // sempre que a API devolver texto, sem lembrar no navegador que já foi visto: some quando a pessoa apaga ou
  // quando a limpeza dos 60 dias apaga. O texto é da pessoa, então entra só por textContent.

  const elRotina = $('al-rotina');
  const elRotinaErro = $('al-rotina-erro');
  const elRotinaStatus = $('al-rotina-status');
  const botaoApagarRotina = $('al-rotina-apagar');

  function renderizarRotina() {
    const texto = perfil && typeof perfil.rotinaDiaria === 'string' ? perfil.rotinaDiaria.trim() : '';
    $('al-rotina-texto').textContent = texto;
    elRotina.hidden = !texto;
  }

  const apagarRotina = ronuComGravacao(async () => {
    elRotinaErro.textContent = '';
    status(elRotinaStatus, '');
    ronuDefinirCarregando(botaoApagarRotina, true, 'Apagando...');
    try {
      let resposta;
      try {
        resposta = await ronuFetchAutenticado('/perfil/rotina', { method: 'DELETE' });
      } catch (erro) {
        if (erro.message === 'Sessão expirada.') throw erro;
        throw new Error('Não foi possível apagar. Tente novamente.');
      }
      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível apagar. Tente novamente.');
      }
      perfil.rotinaDiaria = null;
      renderizarRotina();
      // O botão focado sumiu com o cartão: o foco vai para a confirmação, que fica no lugar dele.
      status(elRotinaStatus, 'Rotina apagada', 'ok');
      elRotinaStatus.focus();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        elRotinaErro.textContent = erro.message;
        elRotinaErro.dataset.tipo = 'erro';
      }
    } finally {
      ronuDefinirCarregando(botaoApagarRotina, false);
    }
  });

  botaoApagarRotina.addEventListener('click', apagarRotina);

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

  // ---------- Gerar dieta ----------

  $('al-gerar').addEventListener('click', () => {
    ronuGerarDietaEIrAoPainel($('al-gerar'), $('al-gerar-erro'), {
      // Horário ainda na espera de salvar (sem mudança, salvarHorarios não grava).
      descarregar: salvarHorarios,
      travar: [document.querySelector('.al-coluna'), document.querySelector('.al-orcamento')]
    });
  });

  // ---------- Carga ----------

  async function carregarPreferencias() {
    try {
      const resposta = await ronuFetchAutenticado('/preferencias-alimentares');
      if (!resposta.ok) throw new Error();
      preferencias = await resposta.json();
      renderizarListas();
      await conferir();
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
      camposHorario.horarioAcordar.value = perfil.horarioAcordar ?? '';
      camposHorario.horarioDormir.value = perfil.horarioDormir ?? '';
      renderizarAvisos();
      renderizarOrcamento(perfil.orcamentoSemanal);
      renderizarNomes();
      renderizarRotina();
      if (perfilIncompleto()) mostrarPerfilIncompleto();
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      const elErro = $('al-perfil-erro');
      ronuMostrarErroFormulario(elErro, 'Não foi possível carregar seus horários e seu orçamento. Recarregue a página.');
      Object.values(camposHorario).forEach((el) => { el.disabled = true; });
      botoesOrcamento.forEach((b) => { b.disabled = true; });
      renderizarOrcamento(null);
      renderizarNomes();
    }
  }

  // Só para os avisos de treino fora da janela: sem as modalidades, os avisos de treino não aparecem.
  async function carregarModalidades() {
    try {
      const resposta = await ronuFetchAutenticado('/usuarios/modalidades');
      if (!resposta.ok) return;
      modalidades = (await resposta.json()).map((m) => ({
        nome: m.modalidade.nome, horarioInicio: m.horarioInicio, duracaoMediaHoras: m.duracaoMediaHoras
      }));
    } catch (erro) {
      // Sem aviso de treino, nada mais muda.
    }
  }

  renderizarSugestoes();
  // Seções independentes: uma falha numa não impede a outra de carregar.
  await Promise.all([carregarPreferencias(), carregarModalidades().then(carregarPerfil)]);
});
