// Ronu — Aba Casa (casa.html), plano família, etapa 7
// Fluxo: guarda de sessão -> GET /casa. 404: a casa vazia, com os termos e o
// "Criar uma casa" (S3). 409 consentimento_casa: o texto novo da casa, com a
// caixa de aceite e a saída. 200: a semana da
// casa (GET /casa/semana), a própria dieta (GET /dietas/atual, para a porção de
// cada item) e a lista da casa (GET /casa/lista-compras, só a contagem) em
// paralelo, e a tela do dono ou do membro.
//
// Privacidade (decisões do plano): nada aqui mostra a porção, a meta ou a
// restrição de outra pessoa; a API nem manda. A porção de cada item vem da
// dieta de quem vê. Os pedidos não têm autor, e o "Pedido enviado" do membro
// fica só neste aparelho (RonuCasa), porque guardar no servidor quem pediu
// seria guardar o autor.

document.addEventListener('DOMContentLoaded', () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const $ = (id) => document.getElementById(id);
  const MAXIMO_MEMBROS = 6;
  const LIMITE_URL_WHATSAPP = 2000;
  const ORCAMENTOS = [['economico', 'Econômico'], ['moderado', 'Moderado'], ['sem_restricao', 'Sem restrição']];
  const NOMES_DIA_CURTO = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];
  const ICONE_FECHAR = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>';
  const ICONE_AVISO = '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" aria-hidden="true"><circle cx="8" cy="8" r="6.5"/><path d="M8 5v3.5M8 11h.01"/></svg>';

  const elCarregando = $('cs-carregando');
  const elFalha = $('cs-falha');
  const elSemCasa = $('cs-sem-casa');
  const elSemAceite = $('cs-sem-aceite');
  const elConteudo = $('cs-conteudo');
  const elErro = $('cs-erro');
  const elAnuncio = $('cs-anuncio');
  const elDialogo = $('cs-dialogo');
  const caixaAceite = $('cs-aceite');

  let casa = null;       // CasaResponse
  let semana = null;     // SemanaCasaResponse, ou null sem semana
  let dieta = null;      // a DietaResponse de quem vê (GET /dietas/atual), ou null
  let lista = null;      // ListaCasaResponse, ou null
  let diaSelecionado = hojeNaSemana();
  let conviteGerado = null; // { link, expiraEm }: o link só vem uma vez, na resposta do POST

  // ---------- Utilidades ----------

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto != null) el.textContent = texto;
    return el;
  }

  function botao(classe, texto, aoClicar) {
    const b = criar('button', classe);
    b.type = 'button';
    const rotulo = criar('span', 'btn-label', texto);
    b.append(rotulo);
    if (aoClicar) b.addEventListener('click', aoClicar);
    return b;
  }

  function icone(svg) {
    const span = criar('span', 'cs-icone');
    span.innerHTML = svg;
    return span;
  }

  // Segunda = 0 ... domingo = 6, na hora local.
  function hojeNaSemana() {
    return (new Date().getDay() + 6) % 7;
  }

  function diaMes(iso) {
    return new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
  }

  function minuscula(texto) {
    return texto.charAt(0).toLowerCase() + texto.slice(1);
  }

  // Limpa antes de escrever, para o leitor de tela anunciar um texto repetido.
  function anunciar(texto) {
    elAnuncio.textContent = '';
    setTimeout(() => { elAnuncio.textContent = texto; }, 60);
  }

  function mostrarErro(texto, { focar = true } = {}) {
    elErro.textContent = texto;
    elErro.hidden = false;
    if (focar) elErro.focus();
  }

  function esconderErro() {
    elErro.hidden = true;
    elErro.textContent = '';
  }

  // Resposta com o corpo já lido. Rede fora vira status 0; sessão expirada
  // (o auth.js já mandou ao login) vira null, e ninguém mexe mais na tela.
  async function pedir(caminho, opcoes) {
    try {
      const resposta = await ronuFetchAutenticado(caminho, opcoes);
      const corpo = resposta.status === 204 ? null : await resposta.json().catch(() => null);
      return { status: resposta.status, ok: resposta.ok, corpo };
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return null;
      return { status: 0, ok: false, corpo: null };
    }
  }

  const comCorpo = (metodo, corpo) => ({
    method: metodo,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(corpo)
  });

  function mensagem(r, padrao) {
    if (r.status === 0) return 'Não foi possível falar com o Ronu agora. Verifique sua conexão e tente de novo.';
    return r.corpo?.mensagem || padrao;
  }

  // Sem o aceite vigente, qualquer rota da casa responde 409 consentimento_casa: a tela recarrega e mostra a saída.
  function semAceite(r) {
    return r.status === 409 && r.corpo?.motivo === 'consentimento_casa';
  }

  function carregando(b, sim, texto) {
    ronuDefinirCarregando(b, sim, texto);
    if (sim) b.setAttribute('aria-busy', 'true');
    else b.removeAttribute('aria-busy');
  }

  function mostrarSomente(visivel) {
    [elCarregando, elFalha, elSemCasa, elSemAceite, elConteudo].forEach((el) => { el.hidden = el !== visivel; });
    const naCasa = visivel === elConteudo;
    $('cs-acao-dono').hidden = !(naCasa && casa?.souDono);
    $('cs-acao-membro').hidden = !(naCasa && casa && !casa.souDono);
    if (!naCasa) $('cs-semana').hidden = true;
  }

  // ---------- Carga ----------

  async function carregar({ silencioso = false } = {}) {
    if (!silencioso) mostrarSomente(elCarregando);
    const r = await pedir('/casa');
    if (!r) return;

    if (r.status === 404) {
      casa = null;
      RonuCabecalho.pedidos(0);
      mostrarSemCasa();
      return;
    }
    if (semAceite(r)) {
      casa = null;
      RonuCabecalho.pedidos(0);
      mostrarSemAceite();
      return;
    }
    if (!r.ok) {
      mostrarSomente(elFalha);
      return;
    }

    casa = r.corpo;
    RonuCabecalho.pedidos(casa.souDono ? casa.pedidosAbertos : 0);

    const [rSemana, rDieta, rLista] = await Promise.all([
      pedir('/casa/semana'), pedir('/dietas/atual'), pedir('/casa/lista-compras')
    ]);
    if (!rSemana || !rDieta || !rLista) return;
    if (semAceite(rSemana)) {
      carregar();
      return;
    }
    if (!rSemana.ok && rSemana.status !== 404) {
      mostrarSomente(elFalha);
      return;
    }
    semana = rSemana.ok ? rSemana.corpo : null;
    // A dieta e a lista só enfeitam (porção e contagem): sem elas, a semana aparece do mesmo jeito.
    dieta = rDieta.ok ? rDieta.corpo : null;
    lista = rLista.ok ? rLista.corpo : null;

    renderizarCasa();
    mostrarSomente(elConteudo);
  }

  // Depois de uma mudança na semana (troca, semana nova): a própria dieta e a lista mudam junto.
  async function recarregarDietaELista() {
    const [rDieta, rLista] = await Promise.all([pedir('/dietas/atual'), pedir('/casa/lista-compras')]);
    if (rDieta?.ok) dieta = rDieta.corpo;
    if (rLista?.ok) lista = rLista.corpo;
  }

  // ---------- Casa vazia (S3) ----------

  let aceiteCarregado = false;

  function mostrarSemCasa() {
    $('cs-frase').textContent = 'Almoço e jantar da mesma panela, cada um com a porção da própria meta. Até 6 adultos, cada um com a própria conta.';
    mostrarSomente(elSemCasa);
    if (!aceiteCarregado) {
      aceiteCarregado = true;
      RonuAceite.iniciar(caixaAceite);
      RonuAceite.carregar(caixaAceite, { logado: true });
    }
  }

  $('cs-criar').addEventListener('click', async () => {
    const b = $('cs-criar');
    if (b.dataset.loading === 'true') return;
    esconderErro();
    const versao = RonuAceite.versaoMarcada(caixaAceite, { mensagem: 'Para criar a casa, marque que leu o que é compartilhado.' });
    if (versao == null) return;

    carregando(b, true, 'Criando a casa...');
    const r = await pedir('/casa', comCorpo('POST', { versaoConsentimento: versao }));
    if (!r) return;
    carregando(b, false);

    if (r.ok || (r.status === 409 && r.corpo?.motivo === 'ja_em_casa')) {
      await carregar();
      anunciar('Casa criada. Convide quem mora com você.');
      $('cs-convidar-botao')?.focus();
      return;
    }
    if (semAceite(r)) {
      RonuAceite.textoMudou(caixaAceite, r.corpo?.mensagem);
      return;
    }
    mostrarErro(mensagem(r, 'Não foi possível criar a casa agora. Tente de novo.'));
  });

  // ---------- Texto novo da casa (versão nova do consentimento "casa") ----------
  // Quem já está na casa e ainda não aceitou o texto vigente: a mesma caixa de aceite, com o texto que vem da API
  // (GET /consentimentos/casa), e o aceite pelo POST /consentimentos, que só vale para quem está numa casa. Sair
  // continua possível sem aceitar.

  const caixaAceiteNovo = $('cs-aceite-novo');
  let aceiteNovoCarregado = false;

  function mostrarSemAceite() {
    $('cs-frase').textContent = 'Almoço e jantar da mesma panela, cada um com a porção da própria meta.';
    mostrarSomente(elSemAceite);
    if (!aceiteNovoCarregado) {
      aceiteNovoCarregado = true;
      RonuAceite.iniciar(caixaAceiteNovo);
    }
    RonuAceite.carregar(caixaAceiteNovo, { logado: true });
  }

  $('cs-sem-aceite-aceitar').addEventListener('click', async () => {
    const b = $('cs-sem-aceite-aceitar');
    if (b.dataset.loading === 'true') return;
    esconderErro();
    const versao = RonuAceite.versaoMarcada(caixaAceiteNovo, { mensagem: 'Para abrir a casa, marque que leu o texto novo.' });
    if (versao == null) return;

    carregando(b, true, 'Abrindo a casa...');
    const r = await pedir('/consentimentos', comCorpo('POST', { tipo: 'casa', versao }));
    if (!r) return;
    carregando(b, false);

    if (r.ok) {
      await carregar();
      anunciar('Autorização aceita. A casa está aberta de novo.');
      return;
    }
    // O texto mudou de novo enquanto a tela estava aberta: a caixa recarrega e pede outra leitura.
    if (r.status === 409) {
      RonuAceite.textoMudou(caixaAceiteNovo, r.corpo?.mensagem);
      return;
    }
    // 400: a pessoa saiu da casa por outra aba. A tela volta a mostrar o que ela tem agora.
    if (r.status === 400) {
      await carregar();
      return;
    }
    mostrarErro(mensagem(r, 'Não foi possível aceitar agora. Tente de novo.'));
  });

  // ---------- Render da casa ----------

  function eu() {
    return casa.membros.find((m) => m.souEu);
  }

  function dono() {
    return casa.membros.find((m) => m.dono);
  }

  function renderizarCasa() {
    esconderErro();
    renderizarTopo();
    renderizarTransferencia();
    renderizarPedidos();
    renderizarAvisoLista();
    renderizarSemana();
    renderizarRestricoes();
    renderizarLista();
    renderizarOrcamento();
    renderizarMembros();
    renderizarConvidar();
    renderizarSaidas();
  }

  function renderizarTopo() {
    const n = casa.membros.length;
    $('cs-frase').textContent = `Almoço e jantar da mesma panela para ${n} ${n === 1 ? 'pessoa' : 'pessoas'}. Você vê só a sua porção.`;
    const rotulo = $('cs-semana');
    rotulo.hidden = !semana;
    if (semana) rotulo.textContent = `Semana de ${diaMes(semana.geradaEm)}`;

    const pedirSemana = $('cs-pedir-semana');
    const pedido = RonuCasa.jaPediuSemana(semana ? semana.semanaId : null);
    pedirSemana.disabled = pedido;
    pedirSemana.querySelector('.btn-label').textContent = pedido ? 'Pedido enviado' : 'Pedir nova semana';
  }

  // ---------- Transferência pendente para mim ----------

  function renderizarTransferencia() {
    const faixa = $('cs-transferencia');
    const minha = !casa.souDono && eu()?.transferenciaPendente;
    faixa.hidden = !minha;
    if (!minha) return;
    $('cs-transferencia-texto').replaceChildren(
      criar('strong', null, dono()?.primeiroNome || 'Quem cuida da casa'),
      ' quer passar a casa para você. Quem cuida da casa gera a semana, troca alimentos e convida.'
    );
  }

  $('cs-transferencia-aceitar').addEventListener('click', async () => {
    const b = $('cs-transferencia-aceitar');
    carregando(b, true, 'Aceitando...');
    const r = await pedir('/casa/transferencia/aceitar', { method: 'POST' });
    if (!r) return;
    carregando(b, false);
    if (r.ok) {
      await carregar({ silencioso: true });
      anunciar('Agora você cuida da casa.');
      $('cs-gerar').focus();
      return;
    }
    // 409: a oferta foi cancelada no meio. A tela volta a mostrar a casa como está.
    await carregar({ silencioso: true });
    mostrarErro(mensagem(r, 'Não foi possível aceitar agora. Tente de novo.'));
  });

  $('cs-transferencia-recusar').addEventListener('click', async () => {
    const b = $('cs-transferencia-recusar');
    carregando(b, true, 'Recusando...');
    const r = await pedir('/casa/transferencia', { method: 'DELETE' });
    if (!r) return;
    carregando(b, false);
    if (!r.ok) {
      mostrarErro(mensagem(r, 'Não foi possível recusar agora. Tente de novo.'));
      return;
    }
    await carregar({ silencioso: true });
    anunciar('Você recusou. A casa continua com quem cuida dela.');
    $('cs-pedir-semana').focus();
  });

  // ---------- Pedidos e avisos (dono) ----------

  function renderizarPedidos() {
    const secao = $('cs-pedidos');
    const pedidos = casa.souDono ? (casa.pedidos || []) : [];
    secao.hidden = pedidos.length === 0;
    $('cs-pedidos-contador').textContent = String(pedidos.length);
    $('cs-pedidos-titulo').textContent = pedidos.length === 1 ? 'Pedido da casa' : 'Pedidos da casa';
    // O número da pílula é visual; o leitor de tela ouve a contagem junto do título.
    $('cs-pedidos-titulo').setAttribute('aria-label', `${pedidos.length} ${pedidos.length === 1 ? 'pedido' : 'pedidos'} da casa`);
    $('cs-pedidos-lista').replaceChildren(...pedidos.map(criarPedido));
  }

  function criarPedido(pedido) {
    const li = criar('li', `cs-pedido${pedido.tipo === 'aviso_entrada' ? ' cs-pedido-aviso' : ''}`);
    li.dataset.pedido = String(pedido.id);
    const texto = criar('p', 'cs-pedido-texto');
    if (pedido.tipo === 'aviso_entrada') texto.append(icone(ICONE_AVISO));
    texto.append(criar('span', null, pedido.texto));

    const acoes = criar('div', 'cs-pedido-acoes');
    if (pedido.tipo === 'nova_semana') {
      acoes.append(botao('botao botao-primario botao-pequeno', 'Gerar nova semana', (e) => gerarSemana(e.currentTarget)));
    } else if (pedido.tipo === 'troca' && semana && pedido.semanaId === semana.semanaId) {
      acoes.append(botao('botao botao-primario botao-pequeno', 'Ver substitutos', (e) => verSubstitutos(pedido, e.currentTarget)));
    }
    const dispensar = botao('botao botao-fantasma botao-pequeno', 'Dispensar', () => dispensarPedido(pedido, dispensar));
    acoes.append(dispensar);
    li.append(texto, acoes);
    return li;
  }

  async function dispensarPedido(pedido, b) {
    carregando(b, true, 'Dispensando...');
    const r = await pedir(`/casa/pedidos/${pedido.id}`, { method: 'DELETE' });
    if (!r) return;
    carregando(b, false);
    // 404: já tinha saído (outra aba, semana nova). Some do mesmo jeito.
    if (!r.ok && r.status !== 404) {
      mostrarErro(mensagem(r, 'Não foi possível dispensar agora. Tente de novo.'));
      return;
    }
    const lista = casa.pedidos || [];
    const indice = lista.findIndex((p) => p.id === pedido.id);
    casa.pedidos = lista.filter((p) => p.id !== pedido.id);
    casa.pedidosAbertos = casa.pedidos.length;
    RonuCabecalho.pedidos(casa.pedidosAbertos);
    renderizarPedidos();
    renderizarAvisoLista();
    anunciar('Pedido dispensado.');
    // O foco vai para o pedido seguinte (ou o anterior); sem nenhum, para o título das refeições.
    const botoes = document.querySelectorAll('.cs-pedido .botao');
    const proximo = document.querySelectorAll('.cs-pedido')[Math.min(indice, casa.pedidos.length - 1)];
    if (proximo) proximo.querySelector('.botao')?.focus();
    else if (!botoes.length) focarTituloRefeicoes();
  }

  function focarTituloRefeicoes() {
    const titulo = $('cs-refeicoes-titulo');
    titulo.tabIndex = -1;
    titulo.focus();
  }

  // "Ver substitutos": vai ao dia do pedido e abre a troca daquele item.
  function verSubstitutos(pedido, origem) {
    const chave = pedido.chavesCasa[0];
    const { dia } = RonuCasa.lerChave(chave);
    if (dia < 0 || !semana) return;
    selecionarDia(dia, false);
    const refeicao = semana.dias[dia][chave.endsWith('-almoco') ? 'almoco' : 'jantar'];
    abrirTroca(dia, refeicao, pedido.item, origem);
  }

  // ---------- Lista pobre e refeição individual (dono) ----------

  const PAPEIS = {
    Proteina: ['fonte de proteína', 'fontes de proteína'],
    Carboidrato: ['opção de carboidrato', 'opções de carboidrato'],
    Fruta: ['fruta', 'frutas'],
    Hortalica: ['hortaliça', 'hortaliças']
  };

  function juntar(lista) {
    return lista.length <= 1 ? (lista[0] || '') : `${lista.slice(0, -1).join(', ')} e ${lista[lista.length - 1]}`;
  }

  function renderizarAvisoLista() {
    const aviso = $('cs-aviso-lista');
    const partes = [];
    const pobre = casa.souDono && semana?.avisoListaPobre;
    if (pobre) {
      const itens = pobre.papeis.map((p) => {
        const n = pobre.itensPorPapel[p];
        const [um, varios] = PAPEIS[p] || [p, p];
        return `${n} ${n === 1 ? um : varios}`;
      });
      partes.push(`Com o Não como e as alergias de todos juntos, sobraram só ${juntar(itens)} para o almoço e o jantar. A semana pode ficar repetitiva.`);
    }
    // O aviso de entrada (nos pedidos) já diz isso quando foi alguém que entrou: aqui só quando ele não está aberto
    // (alguém marcou depois um "não como" ou uma alergia, ou o aviso foi dispensado).
    const avisoAberto = (casa.pedidos || []).some((p) => p.tipo === 'aviso_entrada');
    if (casa.souDono && semana?.refeicaoIndividual && !avisoAberto) {
      partes.push('Uma refeição da casa ficou individual para alguém que não come um item dela. Uma semana nova resolve.');
    }
    aviso.hidden = partes.length === 0;
    $('cs-aviso-lista-texto').textContent = partes.join(' ');
  }

  // ---------- Refeições da casa ----------

  // A refeição da casa na dieta de quem vê (a porção dela), pela chave estável.
  function minhaRefeicao(chaveCasa) {
    if (!dieta?.dieta) return null;
    for (let d = 0; d < dieta.dieta.dias.length; d++) {
      const r = dieta.dieta.dias[d].refeicoes.findIndex((x) => x.daCasa && x.chaveCasa === chaveCasa);
      if (r >= 0) return { dia: d, indice: r, refeicao: dieta.dieta.dias[d].refeicoes[r] };
    }
    return null;
  }

  function renderizarSemana() {
    const semSemana = $('cs-sem-semana');
    const bloco = $('cs-semana-bloco');
    semSemana.hidden = Boolean(semana);
    bloco.hidden = !semana;
    if (!semana) {
      $('cs-sem-semana-texto').textContent = casa.souDono
        ? 'A casa ainda não tem uma semana. Gere a primeira: o Ronu monta o almoço e o jantar de todos, e cada um recebe a própria porção na dieta.'
        : 'Quem cuida da casa ainda não gerou a semana. Se quiser, peça uma: o pedido chega sem o seu nome.';
      return;
    }
    renderizarDias();
    renderizarRefeicoes();
    const nota = $('cs-nota-troca');
    const painel = criar('a', null, 'painel');
    painel.href = 'dashboard.html';
    nota.replaceChildren(
      casa.souDono
        ? 'Trocar aqui muda a panela de todo mundo. O substituto respeita o que cada um não come, e a porção de cada pessoa é recalculada. '
        : 'Só quem criou a casa troca, porque a troca muda a panela de todo mundo. O pedido chega sem o seu nome. ',
      'Café da manhã e lanches continuam no seu ', painel, ', só seus.'
    );
  }

  function renderizarDias() {
    const linha = $('cs-dias');
    linha.replaceChildren(...semana.dias.map((dia, i) => {
      const b = criar('button', 'cs-dia', NOMES_DIA_CURTO[i] || dia.diaSemana.slice(0, 3));
      b.type = 'button';
      b.id = `cs-dia-${i}`;
      b.setAttribute('role', 'tab');
      b.setAttribute('aria-selected', String(i === diaSelecionado));
      b.setAttribute('aria-controls', 'cs-refeicoes');
      b.setAttribute('aria-label', dia.diaSemana);
      b.tabIndex = i === diaSelecionado ? 0 : -1;
      b.addEventListener('click', () => selecionarDia(i, false));
      return b;
    }));
  }

  // Abas com setas (padrão de tablist): só a aba marcada fica no Tab.
  $('cs-dias').addEventListener('keydown', (evento) => {
    if (!semana) return;
    const total = semana.dias.length;
    const passo = { ArrowRight: 1, ArrowLeft: -1 }[evento.key];
    let alvo = null;
    if (passo) alvo = (diaSelecionado + passo + total) % total;
    else if (evento.key === 'Home') alvo = 0;
    else if (evento.key === 'End') alvo = total - 1;
    if (alvo == null) return;
    evento.preventDefault();
    selecionarDia(alvo, true);
  });

  function selecionarDia(indice, focar) {
    diaSelecionado = indice;
    Array.from($('cs-dias').children).forEach((b, i) => {
      b.setAttribute('aria-selected', String(i === indice));
      b.tabIndex = i === indice ? 0 : -1;
    });
    if (focar) $('cs-dias').children[indice].focus();
    renderizarRefeicoes();
  }

  function renderizarRefeicoes() {
    if (diaSelecionado >= semana.dias.length) diaSelecionado = 0;
    const dia = semana.dias[diaSelecionado];
    const painel = $('cs-refeicoes');
    painel.setAttribute('aria-labelledby', `cs-dia-${diaSelecionado}`);
    painel.replaceChildren(
      criarRefeicao(dia, dia.almoco, 'Almoço', 0),
      criarRefeicao(dia, dia.jantar, 'Jantar', 1)
    );
  }

  function quantidadeTexto(alimento) {
    if (!alimento || alimento.quantidade == null) return '';
    return `${Math.round(alimento.quantidade).toLocaleString('pt-BR')} ${alimento.unidade}`;
  }

  function criarRefeicao(dia, refeicaoCasa, nome, ordem) {
    const card = criar('article', 'cartao cs-refeicao entra');
    card.style.setProperty('--atraso', `${ordem * 70}ms`);
    const minha = minhaRefeicao(refeicaoCasa.chaveCasa);

    const cabeca = criar('header', 'cs-refeicao-cabeca');
    const titulo = criar('div', 'cs-refeicao-titulo');
    if (refeicaoCasa.horario) {
      const hora = criar('time', 'cs-refeicao-hora num', refeicaoCasa.horario);
      hora.dateTime = refeicaoCasa.horario;
      titulo.append(hora);
    }
    titulo.append(criar('h3', 'cs-refeicao-nome', nome));
    cabeca.append(titulo, RonuCasa.pilula());
    card.append(cabeca);

    // A refeição virou individual para quem vê (não come um item dela): a casa segue com o cardápio, e o
    // painel tem a refeição da pessoa. Não diz qual item, porque a pessoa sabe.
    if (!minha && dieta) {
      card.append(criar('p', 'cs-refeicao-nota', 'Esta refeição ficou individual para você: no seu painel, ela segue o que você come.'));
    }

    const trocados = refeicaoCasa.trocados || [];
    const ul = criar('ul', 'cs-itens');
    refeicaoCasa.alimentos.forEach((nomeCasa, i) => {
      const meu = minha ? minha.refeicao.alimentos[i] : null;
      const alimento = meu || { nome: nomeCasa, alimentoId: null, quantidade: null, unidade: null };
      const li = criar('li', 'cs-item');
      li.dataset.chave = refeicaoCasa.chaveCasa;
      li.dataset.item = String(i);

      const nomeEl = criar('span', 'cs-item-nome');
      nomeEl.append(criar('span', null, alimento.nome));
      if (casa.souDono && trocados.includes(i)) {
        const marca = criar('button', 'cs-trocado', 'trocado');
        marca.type = 'button';
        marca.setAttribute('aria-label', `${alimento.nome} foi trocado na casa. Voltar ao original`);
        marca.addEventListener('click', () => RonuTroca.abrirOriginalCasa(alvoTroca(diaSelecionado, refeicaoCasa, i, alimento, marca, minha)));
        nomeEl.append(marca);
      }
      li.append(nomeEl, criar('span', 'cs-item-qtd num', quantidadeTexto(meu)));
      li.append(casa.souDono ? botaoTrocar(refeicaoCasa, i, alimento, minha) : botaoPedirTroca(refeicaoCasa, i, alimento));
      ul.append(li);
    });
    card.append(ul);
    return card;
  }

  function nomeRefeicaoDe(refeicaoCasa) {
    return refeicaoCasa.chaveCasa.endsWith('-almoco') ? 'Almoço' : 'Jantar';
  }

  function alvoTroca(diaIndice, refeicaoCasa, item, alimento, origem, minha) {
    return {
      dia: minha ? minha.dia : diaIndice,
      refeicao: minha ? minha.indice : null,
      item,
      alimento,
      nomeRefeicao: nomeRefeicaoDe(refeicaoCasa),
      diaSemana: semana.dias[diaIndice].diaSemana,
      origem,
      casa: { semanaId: semana.semanaId, revisao: semana.revisao, chaveCasa: refeicaoCasa.chaveCasa }
    };
  }

  function abrirTroca(diaIndice, refeicaoCasa, item, origem) {
    const minha = minhaRefeicao(refeicaoCasa.chaveCasa);
    const meu = minha ? minha.refeicao.alimentos[item] : null;
    const alimento = meu || { nome: refeicaoCasa.alimentos[item], alimentoId: null, quantidade: null, unidade: null };
    RonuTroca.abrirOpcoes(alvoTroca(diaIndice, refeicaoCasa, item, alimento, origem, minha));
  }

  function botaoTrocar(refeicaoCasa, item, alimento) {
    const b = criar('button', 'botao-texto cs-item-acao', 'Trocar');
    b.type = 'button';
    b.setAttribute('aria-label', `Trocar ${minuscula(alimento.nome)} para a casa toda`);
    b.addEventListener('click', () => abrirTroca(diaSelecionado, refeicaoCasa, item, b));
    return b;
  }

  function botaoPedirTroca(refeicaoCasa, item, alimento) {
    const feito = RonuCasa.jaPediuTroca(semana.semanaId, refeicaoCasa.chaveCasa, item);
    const b = criar('button', 'botao-texto cs-item-acao', feito ? 'Pedido' : 'Pedir troca');
    b.type = 'button';
    b.disabled = feito;
    b.setAttribute('aria-label', feito ? `Troca de ${minuscula(alimento.nome)} pedida` : `Pedir troca de ${minuscula(alimento.nome)}`);
    b.addEventListener('click', async () => {
      b.setAttribute('aria-busy', 'true');
      const r = await RonuCasa.pedir({
        tipo: 'troca', semanaId: semana.semanaId, chaveCasa: refeicaoCasa.chaveCasa, item, alimentoId: alimento.alimentoId
      });
      if (!r) return;
      b.removeAttribute('aria-busy');
      if (!r.ok) {
        // 409: a semana mudou. Recarrega e diz por quê.
        if (r.status === 409) await carregar({ silencioso: true });
        mostrarErro(r.mensagem);
        return;
      }
      b.textContent = 'Pedido';
      b.disabled = true;
      b.setAttribute('aria-label', `Troca de ${minuscula(alimento.nome)} pedida`);
      anunciar('Pedido de troca enviado, sem o seu nome.');
      // O botão desabilitado perde o foco: ele vai para a linha do item.
      const li = b.closest('.cs-item');
      li.tabIndex = -1;
      li.focus();
    });
    return b;
  }

  // ---------- Restrições da casa (S2) ----------
  // casa.restricoes (GET /casa): o "não como" e as alergias de todos numa lista só, e o "prefiro evitar" na outra,
  // já sem dono, sem dizer o que é alergia e em ordem alfabética (RestricoesDaCasa na API). Sem nenhuma, a seção
  // fica escondida.
  function renderizarRestricoes() {
    const r = casa.restricoes;
    const secao = $('cs-restricoes');
    const naoEntra = r?.naoEntra || [];
    const evitamos = r?.evitamosQuandoDa || [];
    secao.hidden = naoEntra.length === 0 && evitamos.length === 0;
    if (secao.hidden) return;
    $('cs-nao-entra').replaceChildren(...naoEntra.map((x) => criar('li', 'chip chip-pequeno chip-evitar', x)));
    $('cs-evitamos-bloco').hidden = evitamos.length === 0;
    $('cs-evitamos').replaceChildren(...evitamos.map((x) => criar('li', 'chip chip-pequeno chip-evitar-leve', x)));
  }

  // ---------- Lateral: lista, orçamento, membros ----------

  function renderizarLista() {
    const resumo = $('cs-lista-resumo');
    if (!lista || !lista.disponivel) {
      resumo.textContent = semana
        ? 'A semana de todos: almoço, jantar, café e lanches.'
        : 'Sai quando a casa tiver uma semana.';
      return;
    }
    const itens = lista.secoes.flatMap((s) => s.itens);
    const tenho = itens.filter((i) => i.tenho).length;
    resumo.textContent = `A semana de todos: almoço, jantar, café e lanches · ${itens.length} ${itens.length === 1 ? 'item' : 'itens'}, ${tenho} já em casa`;
  }

  function renderizarOrcamento() {
    const grupo = $('cs-orcamento');
    const atual = casa.orcamento;
    const podeMudar = casa.souDono;
    grupo.setAttribute('aria-disabled', String(!podeMudar));
    grupo.replaceChildren(...ORCAMENTOS.map(([valor, nome]) => {
      const b = criar('button', null, nome);
      b.type = 'button';
      b.setAttribute('role', 'radio');
      b.setAttribute('aria-checked', String(valor === atual));
      b.dataset.valor = valor;
      b.disabled = !podeMudar;
      b.addEventListener('click', () => escolherOrcamento(valor));
      return b;
    }));
    const radios = Array.from(grupo.children);
    const marcado = radios.find((b) => b.getAttribute('aria-checked') === 'true') || radios[0];
    radios.forEach((b) => { b.tabIndex = b === marcado ? 0 : -1; });

    const nome = (ORCAMENTOS.find(([v]) => v === atual) || [])[1];
    $('cs-orcamento-nota').textContent = podeMudar
      ? 'Vale para o almoço e o jantar da casa, a partir da próxima semana. O café da manhã e os lanches seguem o orçamento de cada um.'
      : `${nome ? `${nome}. ` : 'Sem orçamento escolhido. '}Quem cuida da casa escolhe. O seu café da manhã e os seus lanches seguem o seu orçamento.`;
  }

  $('cs-orcamento').addEventListener('keydown', (evento) => {
    if (!casa?.souDono) return;
    const passo = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[evento.key];
    if (!passo) return;
    evento.preventDefault();
    const radios = Array.from($('cs-orcamento').children);
    const atual = radios.indexOf(document.activeElement);
    const proximo = radios[(atual + passo + radios.length) % radios.length];
    escolherOrcamento(proximo.dataset.valor, true);
  });

  let salvandoOrcamento = false;
  async function escolherOrcamento(valor, focar = false) {
    if (!casa.souDono || salvandoOrcamento || valor === casa.orcamento) {
      if (focar) $('cs-orcamento').querySelector(`[data-valor="${valor}"]`)?.focus();
      return;
    }
    const antes = casa.orcamento;
    casa.orcamento = valor;
    renderizarOrcamento();
    $('cs-orcamento').querySelector(`[data-valor="${valor}"]`)?.focus();
    salvandoOrcamento = true;
    const r = await pedir('/casa/orcamento', comCorpo('PUT', { orcamento: valor }));
    salvandoOrcamento = false;
    if (!r) return;
    if (r.ok) {
      anunciar('Orçamento da casa salvo. Vale a partir da próxima semana.');
      return;
    }
    casa.orcamento = antes;
    renderizarOrcamento();
    if (semAceite(r)) {
      carregar();
      return;
    }
    mostrarErro(mensagem(r, 'Não foi possível salvar o orçamento. Tente de novo.'), { focar: false });
  }

  function renderizarMembros() {
    const membros = casa.membros;
    $('cs-membros-conta').textContent = `${membros.length} de ${MAXIMO_MEMBROS}`;
    $('cs-membros').replaceChildren(...membros.map((m) => {
      const li = criar('li', 'cs-membro');
      const inicial = criar('span', 'cs-membro-inicial', m.primeiroNome.charAt(0).toUpperCase());
      inicial.setAttribute('aria-hidden', 'true');
      const texto = criar('span', 'cs-membro-texto');
      texto.append(
        criar('span', 'cs-membro-nome', m.souEu ? `${m.primeiroNome} (você)` : m.primeiroNome),
        criar('span', 'apoio', m.dono ? 'Cuida da casa' : 'Membro')
      );
      li.append(inicial, texto);

      // O dono vê a oferta que fez: "Aguardando Bruno aceitar · Cancelar".
      if (casa.souDono && m.transferenciaPendente) {
        texto.append(criar('span', 'apoio cs-membro-espera', `Aguardando ${m.primeiroNome} aceitar a casa`));
        li.append(botaoTexto('Cancelar', `Cancelar a passagem da casa para ${m.primeiroNome}`, cancelarTransferencia));
      } else if (casa.souDono && !m.souEu) {
        li.append(botaoTexto('Remover', `Remover ${m.primeiroNome} da casa`, (e) => confirmarRemover(m, e.currentTarget)));
      }
      return li;
    }));
  }

  function botaoTexto(texto, rotulo, aoClicar) {
    const b = criar('button', 'botao-texto cs-membro-acao', texto);
    b.type = 'button';
    b.setAttribute('aria-label', rotulo);
    b.addEventListener('click', aoClicar);
    return b;
  }

  async function cancelarTransferencia() {
    const r = await pedir('/casa/transferencia', { method: 'DELETE' });
    if (!r) return;
    if (!r.ok) {
      mostrarErro(mensagem(r, 'Não foi possível cancelar agora. Tente de novo.'));
      return;
    }
    await carregar({ silencioso: true });
    anunciar('A casa continua com você.');
    $('cs-passar').focus();
  }

  // ---------- Convidar ----------

  function renderizarConvidar() {
    const bloco = $('cs-convidar');
    bloco.hidden = !casa.souDono;
    if (!casa.souDono) return;
    const cheia = casa.membros.length >= MAXIMO_MEMBROS;
    $('cs-convidar-botao').disabled = cheia;
    $('cs-casa-cheia').hidden = !cheia;

    const abertos = casa.convitesAbertos || [];
    $('cs-convites-abertos').hidden = abertos.length === 0;
    $('cs-convites-titulo').textContent = abertos.length === 1 ? '1 convite aberto' : `${abertos.length} convites abertos`;
    $('cs-convites-lista').replaceChildren(...abertos.map((c) => {
      const li = criar('li', 'cs-convite-aberto');
      li.append(criar('span', 'apoio', `Vale até ${diaMes(c.expiraEm)}`),
        botaoTexto('Cancelar', `Cancelar o convite que vale até ${diaMes(c.expiraEm)}`, () => cancelarConvite(c)));
      return li;
    }));
    renderizarConvite();
  }

  function textoConvite() {
    return `Convite para a minha casa no Ronu (vale até ${diaMes(conviteGerado.expiraEm)}): ${conviteGerado.link}`;
  }

  function renderizarConvite() {
    const bloco = $('cs-convite');
    const aberto = Boolean(conviteGerado);
    bloco.hidden = !aberto;
    $('cs-convidar-botao').setAttribute('aria-expanded', String(aberto));
    if (!aberto) return;
    $('cs-convite-link').textContent = conviteGerado.link;
    $('cs-convite-nota').textContent = `Vale até ${diaMes(conviteGerado.expiraEm)} e só para uma pessoa. Quem abrir entra com a própria conta, precisa ter 18 anos ou mais e vê o que é compartilhado antes de aceitar.`;

    const temShare = typeof navigator.share === 'function';
    const url = `https://wa.me/?text=${encodeURIComponent(textoConvite())}`;
    const longoDemais = !temShare && url.length > LIMITE_URL_WHATSAPP;
    $('cs-convite-enviar').hidden = !temShare;
    const zap = $('cs-convite-whatsapp');
    zap.hidden = temShare || longoDemais;
    zap.href = longoDemais ? '#' : url;
    $('cs-convite-longo').hidden = !longoDemais;
    const copiar = $('cs-convite-copiar');
    copiar.classList.toggle('botao-primario', longoDemais);
    copiar.classList.toggle('botao-fantasma', !longoDemais);
  }

  $('cs-convidar-botao').addEventListener('click', async () => {
    const b = $('cs-convidar-botao');
    if (conviteGerado) {
      // Fechar o bloco não cancela o convite: ele continua em "convites abertos".
      conviteGerado = null;
      renderizarConvite();
      return;
    }
    if (b.dataset.loading === 'true') return;
    esconderErro();
    carregando(b, true, 'Gerando o link...');
    const r = await pedir('/casa/convites', { method: 'POST' });
    if (!r) return;
    carregando(b, false);
    if (!r.ok) {
      if (semAceite(r)) {
        carregar();
        return;
      }
      mostrarErro(mensagem(r, 'Não foi possível gerar o convite. Tente de novo.'));
      return;
    }
    conviteGerado = r.corpo;
    // O convite novo entra em "convites abertos".
    const rCasa = await pedir('/casa');
    if (rCasa?.ok) casa = rCasa.corpo;
    renderizarConvidar();
    $('cs-convite-copiar').textContent = 'Copiar link';
    $('cs-convite-titulo').focus();
  });

  $('cs-convite-enviar').addEventListener('click', async () => {
    try {
      await navigator.share({ text: textoConvite() });
    } catch (erro) {
      // Cancelar a folha do aparelho não é erro; o resto cai no "Copiar".
      if (erro && erro.name !== 'AbortError') $('cs-convite-copiar').focus();
    }
  });

  let tempoCopiado;
  $('cs-convite-copiar').addEventListener('click', async () => {
    const b = $('cs-convite-copiar');
    let copiou = false;
    try {
      await navigator.clipboard.writeText(conviteGerado.link);
      copiou = true;
    } catch {
      // Sem a área de transferência: seleciona o link para a pessoa copiar.
      const intervalo = document.createRange();
      intervalo.selectNodeContents($('cs-convite-link'));
      const selecao = window.getSelection();
      selecao.removeAllRanges();
      selecao.addRange(intervalo);
    }
    b.textContent = copiou ? 'Copiado' : 'Link selecionado';
    $('cs-convite-copiado').textContent = copiou ? 'Link copiado.' : 'Link selecionado: use o copiar do aparelho.';
    clearTimeout(tempoCopiado);
    tempoCopiado = setTimeout(() => { b.textContent = 'Copiar link'; }, 2000);
  });

  async function cancelarConvite(convite) {
    const r = await pedir(`/casa/convites/${convite.id}`, { method: 'DELETE' });
    if (!r) return;
    if (!r.ok && r.status !== 404) {
      mostrarErro(mensagem(r, 'Não foi possível cancelar o convite. Tente de novo.'));
      return;
    }
    casa.convitesAbertos = (casa.convitesAbertos || []).filter((c) => c.id !== convite.id);
    renderizarConvidar();
    anunciar('Convite cancelado: o link não vale mais.');
    $('cs-convidar-botao').focus();
  }

  // ---------- Sair, passar a casa, remover ----------

  function renderizarSaidas() {
    const comOutros = casa.membros.length > 1;
    $('cs-passar').hidden = !(casa.souDono && comOutros);
    $('cs-sair-nota').textContent = casa.souDono
      ? (comOutros
        ? 'Enquanto houver membros, passe a casa para alguém antes de sair.'
        : 'Você é o último da casa: sair apaga a casa.')
      : 'Ao sair, o almoço e o jantar desta semana continuam na sua dieta, com a sua porção, até você gerar uma nova.';
  }

  $('cs-passar').addEventListener('click', (e) => abrirPassar(e.currentTarget));
  $('cs-sair').addEventListener('click', (e) => confirmarSair(e.currentTarget));
  $('cs-sem-aceite-sair').addEventListener('click', (e) => confirmarSair(e.currentTarget, { semCasaCarregada: true }));

  // Folha de confirmação: o mesmo componente da troca (showModal prende o foco e o Esc fecha).
  let retornoDialogo = null;
  elDialogo.addEventListener('close', () => {
    const retorno = retornoDialogo;
    retornoDialogo = null;
    elDialogo.replaceChildren();
    if (retorno?.isConnected) retorno.focus();
  });
  elDialogo.addEventListener('click', (evento) => {
    if (evento.target === elDialogo) elDialogo.close();
  });

  function abrirDialogo({ titulo, corpo, acoes, origem }) {
    const cabeca = criar('header', 'folha-cabeca');
    const textoCabeca = criar('div', 'folha-cabeca-texto');
    const h2 = criar('h2', 'folha-titulo', titulo);
    h2.id = 'cs-dialogo-titulo';
    h2.tabIndex = -1;
    textoCabeca.append(h2);
    const fechar = criar('button', 'folha-fechar');
    fechar.type = 'button';
    fechar.setAttribute('aria-label', 'Fechar');
    fechar.innerHTML = ICONE_FECHAR;
    fechar.addEventListener('click', () => elDialogo.close());
    cabeca.append(textoCabeca, fechar);

    const elCorpo = criar('div', 'folha-corpo');
    elCorpo.append(...corpo);
    const rodape = criar('footer', 'folha-rodape cs-dialogo-rodape');
    const erro = criar('p', 'aviso aviso-erro', '');
    erro.setAttribute('role', 'alert');
    erro.hidden = true;
    rodape.append(erro, ...acoes);

    elDialogo.replaceChildren(cabeca, elCorpo, rodape);
    retornoDialogo = origem;
    if (!elDialogo.open) elDialogo.showModal();
    h2.focus();
    return { erro };
  }

  function erroNoDialogo(r, padrao) {
    const erro = elDialogo.querySelector('.aviso-erro');
    if (!erro) return;
    erro.textContent = mensagem(r, padrao);
    erro.hidden = false;
  }

  function confirmarSair(origem, { semCasaCarregada = false } = {}) {
    if (!semCasaCarregada && casa.souDono && casa.membros.length > 1) {
      abrirPassar(origem, { paraSair: true });
      return;
    }
    const ultimo = !semCasaCarregada && casa.souDono;
    const texto = ultimo
      ? 'Você é o último da casa. Sair apaga a casa.'
      : 'O almoço e o jantar desta semana continuam na sua dieta, com a sua porção, até você gerar uma nova. Seu vínculo com a casa é apagado.';
    const sair = botao('botao botao-perigo botao-bloco', ultimo ? 'Apagar a casa e sair' : 'Sair da casa', async () => {
      carregando(sair, true, 'Saindo...');
      const r = await pedir('/casa/eu', { method: 'DELETE' });
      if (!r) return;
      carregando(sair, false);
      if (r.ok) {
        retornoDialogo = null;
        elDialogo.close();
        conviteGerado = null;
        await carregar();
        anunciar(ultimo ? 'A casa foi apagada.' : 'Você saiu da casa.');
        $('cs-criar').focus();
        return;
      }
      // 409 transferir_antes: entrou alguém no meio. A casa recarrega e o caminho vira passar a casa.
      if (r.corpo?.motivo === 'transferir_antes') {
        elDialogo.close();
        await carregar({ silencioso: true });
        abrirPassar($('cs-sair'), { paraSair: true });
        return;
      }
      erroNoDialogo(r, 'Não foi possível sair agora. Tente de novo.');
    });
    const ficar = botao('botao-texto', 'Ficar', () => elDialogo.close());
    abrirDialogo({ titulo: ultimo ? 'Apagar a casa?' : 'Sair da casa?', corpo: [criar('p', 'cs-dialogo-texto', texto)], acoes: [sair, ficar], origem });
  }

  function confirmarRemover(membro, origem) {
    const remover = botao('botao botao-perigo botao-bloco', 'Remover', async () => {
      carregando(remover, true, 'Removendo...');
      const r = await pedir(`/casa/membros/${membro.membroId}`, { method: 'DELETE' });
      if (!r) return;
      carregando(remover, false);
      if (r.ok || r.status === 404) {
        retornoDialogo = null;
        elDialogo.close();
        await carregar({ silencioso: true });
        anunciar(`${membro.primeiroNome} saiu da casa.`);
        $('cs-membros-titulo').tabIndex = -1;
        $('cs-membros-titulo').focus();
        return;
      }
      erroNoDialogo(r, 'Não foi possível remover agora. Tente de novo.');
    });
    const cancelar = botao('botao-texto', 'Cancelar', () => elDialogo.close());
    abrirDialogo({
      titulo: `Remover ${membro.primeiroNome}?`,
      corpo: [criar('p', 'cs-dialogo-texto', `${membro.primeiroNome} sai da casa. O almoço e o jantar desta semana continuam na dieta de ${membro.primeiroNome}.`)],
      acoes: [remover, cancelar],
      origem
    });
  }

  // Passar a casa: escolher o membro (rádios), depois "Aguardando Bruno aceitar" na lista.
  function abrirPassar(origem, { paraSair = false } = {}) {
    const outros = casa.membros.filter((m) => !m.souEu);
    let escolhido = (outros.find((m) => m.transferenciaPendente) || outros[0])?.membroId;

    const grupo = criar('div', 'cs-escolha');
    grupo.setAttribute('role', 'radiogroup');
    grupo.setAttribute('aria-label', 'Para quem passar a casa');
    const desenharRadios = () => {
      grupo.replaceChildren(...outros.map((m) => {
        const r = criar('button', 'opcao cs-escolha-opcao');
        r.type = 'button';
        r.setAttribute('role', 'radio');
        r.setAttribute('aria-checked', String(m.membroId === escolhido));
        r.tabIndex = m.membroId === escolhido ? 0 : -1;
        r.dataset.membro = String(m.membroId);
        const inicial = criar('span', 'cs-membro-inicial', m.primeiroNome.charAt(0).toUpperCase());
        inicial.setAttribute('aria-hidden', 'true');
        r.append(inicial, criar('span', 'cs-escolha-nome', m.primeiroNome));
        r.addEventListener('click', () => { escolhido = m.membroId; desenharRadios(); grupo.querySelector(`[data-membro="${m.membroId}"]`).focus(); });
        return r;
      }));
    };
    desenharRadios();
    grupo.addEventListener('keydown', (evento) => {
      const passo = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[evento.key];
      if (!passo) return;
      evento.preventDefault();
      const atual = outros.findIndex((m) => m.membroId === escolhido);
      escolhido = outros[(atual + passo + outros.length) % outros.length].membroId;
      desenharRadios();
      grupo.querySelector(`[data-membro="${escolhido}"]`).focus();
    });

    const passar = botao('botao botao-primario botao-bloco', 'Passar a casa', async () => {
      if (escolhido == null) return;
      carregando(passar, true, 'Passando...');
      const r = await pedir('/casa/transferencia', comCorpo('POST', { membroId: escolhido }));
      if (!r) return;
      carregando(passar, false);
      if (r.ok) {
        retornoDialogo = null;
        elDialogo.close();
        await carregar({ silencioso: true });
        const nome = casa.membros.find((m) => m.membroId === escolhido)?.primeiroNome || 'a pessoa';
        anunciar(`Aguardando ${nome} aceitar a casa.`);
        $('cs-membros-titulo').tabIndex = -1;
        $('cs-membros-titulo').focus();
        return;
      }
      erroNoDialogo(r, 'Não foi possível passar a casa agora. Tente de novo.');
    });
    const cancelar = botao('botao-texto', 'Cancelar', () => elDialogo.close());

    const corpo = [];
    if (paraSair) corpo.push(criar('p', 'cs-dialogo-texto', 'Passe a casa para outra pessoa antes de sair. Depois que ela aceitar, você sai como membro.'));
    corpo.push(criar('p', 'cs-dialogo-texto texto-2', 'Quem cuida da casa gera a semana, troca alimentos e convida. A pessoa escolhida precisa aceitar.'), grupo);
    abrirDialogo({ titulo: 'Passar a casa', corpo, acoes: [passar, cancelar], origem });
  }

  // ---------- Gerar e pedir semana ----------

  let gerando = false;
  async function gerarSemana(b) {
    if (gerando) return;
    gerando = true;
    esconderErro();
    carregando(b, true, 'Montando a semana da casa...');
    const cancelarEspera = ronuAvisoDeEspera(b.querySelector('.btn-label'));
    const r = await pedir('/casa/semana', { method: 'POST' });
    cancelarEspera();
    gerando = false;
    if (!r) return;
    carregando(b, false);
    if (!r.ok) {
      if (semAceite(r)) {
        carregar();
        return;
      }
      mostrarErro(mensagem(r, 'Não foi possível gerar a semana da casa agora. Tente de novo em alguns instantes.'));
      return;
    }
    RonuTroca.fecharAviso();
    await carregar({ silencioso: true });
    anunciar('Semana da casa gerada. Cada um já tem a própria porção na dieta.');
    focarTituloRefeicoes();
  }

  $('cs-gerar').addEventListener('click', () => gerarSemana($('cs-gerar')));

  $('cs-pedir-semana').addEventListener('click', async () => {
    const b = $('cs-pedir-semana');
    if (b.disabled) return;
    esconderErro();
    carregando(b, true, 'Enviando...');
    const r = await RonuCasa.pedir({ tipo: 'nova_semana', semanaId: semana ? semana.semanaId : null });
    if (!r) return;
    carregando(b, false);
    if (!r.ok) {
      if (r.status === 409) await carregar({ silencioso: true });
      mostrarErro(r.mensagem);
      return;
    }
    renderizarTopo();
    anunciar('Pedido de semana nova enviado, sem o seu nome.');
    focarTituloRefeicoes();
  });

  $('cs-tentar').addEventListener('click', () => carregar());

  // ---------- Troca na casa (js/troca.js) ----------

  RonuTroca.configurar({
    dietaAtual: () => dieta,
    aplicar() {},
    semanaCasa: () => semana,

    // A troca devolve a semana da casa: a porção do dono (e a lista) mudam junto.
    async aplicarCasa(semanaNova) {
      semana = semanaNova;
      await recarregarDietaELista();
      // Pedidos de troca daquele alimento saem na troca: a casa vem de novo, com o contador.
      const rCasa = await pedir('/casa');
      if (rCasa?.ok) {
        casa = rCasa.corpo;
        RonuCabecalho.pedidos(casa.souDono ? casa.pedidosAbertos : 0);
      }
      renderizarCasa();
    },

    focarItem({ chaveCasa, item }) {
      const linha = document.querySelector(`.cs-item[data-chave="${chaveCasa}"][data-item="${item}"]`);
      if (!linha) return;
      const alvo = linha.querySelector('.cs-trocado') || linha.querySelector('.cs-item-acao');
      (alvo || linha).focus();
    },

    async recarregar(texto) {
      await carregar({ silencioso: true });
      mostrarErro(texto);
    },

    gerar() {
      gerarSemana($('cs-gerar'));
    }
  });

  carregar();
});
