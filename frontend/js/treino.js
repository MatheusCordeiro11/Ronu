// Ronu — Treino (modalidades praticadas)
// Lista, adiciona, edita (dias + duração) e remove as modalidades do usuário.
//
// Como no canvas aprovado, não há botão "Salvar": cada mudança é gravada
// sozinha (POST /usuarios/modalidades faz upsert pela modalidade) pouco depois
// do último toque, e o card mostra "Salvando..." / "Salvo" — nunca em
// silêncio. Uma modalidade nova só vai para a API quando ganha o primeiro dia,
// porque a API exige pelo menos um.
//
// Os números (kcal por sessão, gasto por dia, total da semana) vêm do
// POST /metas/simulacao com a lista em edição, a mesma conta da dieta, com o
// peso de tendência salvo.

document.addEventListener('DOMContentLoaded', async () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const $ = (id) => document.getElementById(id);

  const elLoading = $('treino-loading');
  const elLoadError = $('treino-load-error');
  const elContent = $('treino-content');
  const elErro = $('treino-error');
  const elLista = $('tr-lista');
  const elVazio = $('tr-vazio');
  const elCartas = $('tr-cartas');
  const elBarras = $('tr-barras');

  // 1 = segunda ... 7 = domingo (ISO 8601, mesmo padrão do backend).
  const DIAS = [
    { valor: 1, curto: 'Seg', letra: 'S', nome: 'segunda-feira' },
    { valor: 2, curto: 'Ter', letra: 'T', nome: 'terça-feira' },
    { valor: 3, curto: 'Qua', letra: 'Q', nome: 'quarta-feira' },
    { valor: 4, curto: 'Qui', letra: 'Q', nome: 'quinta-feira' },
    { valor: 5, curto: 'Sex', letra: 'S', nome: 'sexta-feira' },
    { valor: 6, curto: 'Sáb', letra: 'S', nome: 'sábado' },
    { valor: 7, curto: 'Dom', letra: 'D', nome: 'domingo' }
  ];

  // Mesma faixa do UsuarioModalidadeRequest (0,25 a 5 h), em passos de 15 min.
  const DURACAO_MIN = 15;
  const DURACAO_MAX = 300;
  const PASSO_MIN = 15;
  const ESPERA_SALVAR_MS = 700;
  const ESPERA_SIMULAR_MS = 300;

  // Kanji de enfeite de cada carta do catálogo (aria-hidden). Modalidade nova
  // no catálogo, sem kanji aqui, usa 技 ("técnica").
  const KANJI = {
    'jiu-jitsu': '柔', 'muay thai': '拳', boxe: '打', 'musculação': '力',
    futebol: '蹴', basquete: '籠', 'natação': '泳'
  };

  let catalogo = [];
  let itens = [];          // modalidades na tela, salvas ou ainda não
  let aberta = null;       // modalidadeId do card aberto
  let simulacao = null;    // última resposta do POST /metas/simulacao
  let mudou = false;       // houve mudança salva nesta visita

  // ---------- Formatação ----------

  function formatarNumero(valor) {
    return Math.round(valor).toLocaleString('pt-BR');
  }

  function formatarDecimal(valor, casas = 2) {
    return Number(valor).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: casas });
  }

  function formatarDuracao(minutos) {
    const h = Math.floor(minutos / 60);
    const m = minutos % 60;
    return [h ? `${h} h` : '', m ? `${m} min` : ''].filter(Boolean).join(' ');
  }

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto != null) el.textContent = texto;
    return el;
  }

  function diasOrdenados(item) {
    return [...item.dias].sort((a, b) => a - b);
  }

  // Duração salva em decimal (pode ter vindo de antes, com minutos "soltos",
  // ex.: 1,4 h): arredonda para o múltiplo de 15 min mais próximo.
  function horasParaMinutos(horas) {
    const minutos = Math.round((horas * 60) / PASSO_MIN) * PASSO_MIN;
    return Math.min(DURACAO_MAX, Math.max(DURACAO_MIN, minutos));
  }

  // ---------- Estados de topo ----------

  function mostrarSomente(elementoVisivel) {
    [elLoading, elLoadError, elContent].forEach((el) => {
      el.hidden = el !== elementoVisivel;
    });
    $('tr-resumo').hidden = elementoVisivel !== elContent;
  }

  // ---------- Simulação (números da tela) ----------

  let pedidoSimulacao = 0;
  let simulacaoPendente = null;

  // Só entram as modalidades com dia: é o que a próxima dieta usaria.
  function agendarSimulacao() {
    clearTimeout(simulacaoPendente);
    simulacaoPendente = setTimeout(simular, ESPERA_SIMULAR_MS);
  }

  async function simular() {
    const numero = ++pedidoSimulacao;
    const modalidades = itens
      .filter((item) => item.dias.size > 0)
      .map((item) => ({ modalidadeId: item.modalidade.id, diasSemana: diasOrdenados(item), duracaoMediaHoras: item.duracao / 60 }));
    try {
      const resposta = await ronuFetchAutenticado('/metas/simulacao', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ modalidades })
      });
      if (!resposta.ok) throw new Error();
      const dados = await resposta.json();
      // Uma resposta antiga que chega depois de uma nova não vale mais.
      if (numero !== pedidoSimulacao) return;
      simulacao = dados;
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      if (numero !== pedidoSimulacao) return;
      simulacao = null;
    }
    atualizarNumeros();
  }

  function kcalSessao(item) {
    if (!simulacao || item.dias.size === 0) return null;
    const sim = simulacao.modalidades.find((m) => m.modalidadeId === item.modalidade.id);
    return sim ? sim.kcalSessao : null;
  }

  // ---------- Card de cada modalidade ----------

  function criarCard(item) {
    const id = item.modalidade.id;
    const card = criar('article', 'cartao tr-card');
    card.dataset.modalidadeId = id;

    const cabeca = criar('button', 'tr-card-cabeca');
    cabeca.type = 'button';
    cabeca.id = `tr-cabeca-${id}`;
    cabeca.setAttribute('aria-controls', `tr-corpo-${id}`);
    const textos = criar('span', 'tr-card-textos');
    const nome = criar('span', 'tr-card-nome', item.modalidade.nome);
    const resumo = criar('span', 'tr-card-resumo');
    textos.append(nome, resumo);
    const numeros = criar('span', 'tr-card-kcal');
    const kcal = criar('span', 'numero tr-card-kcal-valor');
    numeros.append(kcal, criar('span', 'apoio', 'kcal por sessão'));
    cabeca.append(textos, numeros);
    cabeca.addEventListener('click', () => {
      aberta = aberta === id ? null : id;
      atualizarAberturas();
      atualizarNumeros();
    });

    const corpo = criar('div', 'tr-card-corpo');
    corpo.id = `tr-corpo-${id}`;
    corpo.setAttribute('role', 'region');
    corpo.setAttribute('aria-labelledby', cabeca.id);

    // Dias
    const blocoDias = criar('div', 'tr-bloco');
    const rotuloDias = criar('span', 'campo-rotulo', 'Dias da semana');
    rotuloDias.id = `tr-dias-rotulo-${id}`;
    const grupoDias = criar('div', 'dias tr-dias');
    grupoDias.setAttribute('role', 'group');
    grupoDias.setAttribute('aria-labelledby', rotuloDias.id);
    DIAS.forEach((dia) => {
      const botao = criar('button');
      botao.type = 'button';
      botao.dataset.dia = dia.valor;
      botao.setAttribute('aria-label', dia.nome);
      botao.append(criar('span', 'tr-so-largo', dia.curto), criar('span', 'tr-so-celular', dia.letra));
      botao.addEventListener('click', () => {
        if (item.dias.has(dia.valor)) item.dias.delete(dia.valor);
        else item.dias.add(dia.valor);
        aoMudar(item);
      });
      grupoDias.appendChild(botao);
    });
    blocoDias.append(rotuloDias, grupoDias);

    // Duração e remover
    const linha = criar('div', 'tr-linha');
    const blocoDuracao = criar('div', 'tr-bloco tr-bloco-duracao');
    const rotuloDuracao = criar('span', 'campo-rotulo');
    rotuloDuracao.append(criar('span', 'tr-so-largo', 'Duração de cada sessão'), criar('span', 'tr-so-celular', 'Duração'));
    const controle = criar('div', 'tr-duracao');
    controle.setAttribute('role', 'group');
    controle.setAttribute('aria-label', `Duração de cada sessão de ${item.modalidade.nome}`);
    const menos = criar('button', null, '−');
    menos.type = 'button';
    menos.setAttribute('aria-label', 'Diminuir 15 minutos');
    const valor = criar('output');
    valor.setAttribute('aria-live', 'polite');
    const mais = criar('button', null, '+');
    mais.type = 'button';
    mais.setAttribute('aria-label', 'Aumentar 15 minutos');
    menos.addEventListener('click', () => { item.duracao = Math.max(DURACAO_MIN, item.duracao - PASSO_MIN); aoMudar(item); });
    mais.addEventListener('click', () => { item.duracao = Math.min(DURACAO_MAX, item.duracao + PASSO_MIN); aoMudar(item); });
    controle.append(menos, valor, mais);
    blocoDuracao.append(rotuloDuracao, controle);

    const acoes = criar('div', 'tr-acoes');
    linha.append(blocoDuracao, acoes);

    // Conta da sessão
    const conta = criar('div', 'tr-conta');
    const rotuloConta = criar('span', 'rotulo tr-conta-rotulo');
    rotuloConta.append(criar('span', 'tr-so-largo', 'Como o gasto da sessão é calculado'), criar('span', 'tr-so-celular', 'Gasto da sessão'));
    conta.append(
      rotuloConta,
      criar('span', 'tr-conta-formula num'),
      criar('span', 'apoio', 'O “− 1” tira o que você já gastaria parado, que entra na conta do dia a dia.')
    );

    const status = criar('p', 'tr-status');
    status.setAttribute('role', 'status');

    corpo.append(blocoDias, linha, conta, status);
    card.append(cabeca, corpo);

    item.el = { card, cabeca, resumo, kcal, corpo, grupoDias, valor, menos, mais, acoes, formula: conta.querySelector('.tr-conta-formula'), status };
    renderizarAcoes(item);
    return card;
  }

  // "Remover modalidade" -> "Remover? Sim / Cancelar". A última modalidade
  // salva não sai: sem ela o cadastro fica incompleto (ronuChecarCadastroCompleto
  // exige uma) e o próximo login cairia no onboarding.
  function renderizarAcoes(item, confirmando = false) {
    const acoes = item.el.acoes;
    acoes.replaceChildren();
    if (!confirmando) {
      const remover = criar('button', 'botao-texto tr-remover', 'Remover modalidade');
      remover.type = 'button';
      remover.addEventListener('click', () => {
        ronuOcultarErroFormulario(elErro);
        if (!item.vinculoId) {
          removerDaTela(item);
          return;
        }
        if (itens.filter((i) => i.vinculoId).length <= 1) {
          ronuMostrarErroFormulario(elErro, 'Você precisa manter pelo menos uma modalidade.');
          return;
        }
        renderizarAcoes(item, true);
        acoes.querySelector('.tr-confirmar').focus();
      });
      acoes.appendChild(remover);
      return;
    }
    const pergunta = criar('span', 'apoio', `Remover ${item.modalidade.nome}?`);
    // .btn-label: o ronuDefinirCarregando troca o texto dele ("Removendo...").
    const sim = criar('button', 'botao botao-fantasma botao-pequeno tr-confirmar');
    sim.type = 'button';
    sim.appendChild(criar('span', 'btn-label', 'Sim, remover'));
    const nao = criar('button', 'botao-texto', 'Cancelar');
    nao.type = 'button';
    sim.addEventListener('click', () => removerModalidade(item, sim));
    nao.addEventListener('click', () => {
      renderizarAcoes(item);
      acoes.querySelector('.tr-remover').focus();
    });
    acoes.append(pergunta, sim, nao);
  }

  function atualizarCard(item) {
    const { resumo, kcal, grupoDias, valor, menos, mais, formula } = item.el;
    const dias = diasOrdenados(item);
    const nomesDias = dias.map((d) => DIAS[d - 1].curto).join(', ');
    resumo.textContent = `${dias.length ? nomesDias : 'Nenhum dia escolhido'} · ${formatarDuracao(item.duracao)}`;

    grupoDias.querySelectorAll('button').forEach((botao) => {
      botao.setAttribute('aria-pressed', String(item.dias.has(Number(botao.dataset.dia))));
    });
    valor.textContent = formatarDuracao(item.duracao);
    menos.disabled = item.duracao <= DURACAO_MIN;
    mais.disabled = item.duracao >= DURACAO_MAX;

    const k = kcalSessao(item);
    kcal.textContent = k == null ? '—' : formatarNumero(k);
    if (k != null && simulacao) {
      const met = formatarDecimal(item.modalidade.metReferencia);
      // No celular sem a palavra "intensidade", como no canvas, para caber.
      formula.replaceChildren(
        '(',
        criar('span', 'tr-so-largo', 'intensidade '),
        `${met} − 1) × ${formatarDecimal(simulacao.entradas.peso, 1)} kg × ${formatarDecimal(item.duracao / 60)} h = `,
        criar('strong', 'tr-sem-quebra', `${formatarNumero(k)} kcal`)
      );
    } else {
      formula.textContent = item.dias.size === 0
        ? 'Escolha pelo menos um dia para ver o gasto da sessão.'
        : 'Calculando...';
    }
  }

  function atualizarAberturas() {
    itens.forEach((item) => {
      const abertaAgora = item.modalidade.id === aberta;
      item.el.card.classList.toggle('tr-card-aberto', abertaAgora);
      item.el.cabeca.setAttribute('aria-expanded', String(abertaAgora));
      item.el.corpo.hidden = !abertaAgora;
    });
  }

  function renderizarLista() {
    elLista.replaceChildren(...itens.map((item) => item.el.card));
    elVazio.hidden = itens.length > 0;
    itens.forEach(atualizarCard);
    atualizarAberturas();
    renderizarCatalogo();
  }

  // ---------- Gravação ----------

  function mostrarStatus(item, texto, tipo) {
    item.el.status.textContent = texto;
    item.el.status.dataset.tipo = tipo || '';
  }

  function aoMudar(item) {
    atualizarCard(item);
    agendarSimulacao();
    clearTimeout(item.salvarPendente);
    if (item.dias.size === 0) {
      mostrarStatus(item, item.vinculoId
        ? 'Escolha pelo menos um dia para salvar.'
        : 'Escolha os dias para adicionar aos seus treinos.', 'aviso');
      return;
    }
    mostrarStatus(item, '');
    item.salvarPendente = setTimeout(() => salvar(item), ESPERA_SALVAR_MS);
  }

  function igualAoSalvo(item) {
    return item.salvo
      && item.salvo.duracao === item.duracao
      && JSON.stringify(item.salvo.dias) === JSON.stringify(diasOrdenados(item));
  }

  // Uma gravação por vez por card: o que mudar durante uma vai na seguinte.
  async function salvar(item) {
    if (item.salvando) { item.salvarDeNovo = true; return; }
    if (item.dias.size === 0 || igualAoSalvo(item)) return;

    item.salvando = true;
    const dias = diasOrdenados(item);
    const duracao = item.duracao;
    mostrarStatus(item, 'Salvando...');
    ronuOcultarErroFormulario(elErro);

    try {
      const resposta = await ronuFetchAutenticado('/usuarios/modalidades', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ modalidadeId: item.modalidade.id, diasSemana: dias, duracaoMediaHoras: duracao / 60 })
      });
      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível salvar. Tente novamente.');
      }
      const salvo = await resposta.json();
      item.vinculoId = salvo.id;
      item.salvo = { dias, duracao };
      mudou = true;
      atualizarAvisoGerar();
      mostrarStatus(item, igualAoSalvo(item) ? 'Salvo' : '', 'ok');
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        mostrarStatus(item, erro.message, 'erro');
      }
    } finally {
      item.salvando = false;
      if (item.salvarDeNovo) {
        item.salvarDeNovo = false;
        salvar(item);
      }
    }
  }

  function removerDaTela(item) {
    clearTimeout(item.salvarPendente);
    itens = itens.filter((i) => i !== item);
    if (aberta === item.modalidade.id) aberta = itens[0] ? itens[0].modalidade.id : null;
    renderizarLista();
    agendarSimulacao();
    // O foco vai para o título da seção, em vez de se perder com o card.
    $('tr-minhas-titulo').tabIndex = -1;
    $('tr-minhas-titulo').focus();
  }

  async function removerModalidade(item, botao) {
    if (item.salvando) return;
    ronuDefinirCarregando(botao, true, 'Removendo...');
    try {
      const resposta = await ronuFetchAutenticado(`/usuarios/modalidades/${item.vinculoId}`, { method: 'DELETE' });
      if (!resposta.ok && resposta.status !== 404) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível remover. Tente novamente.');
      }
      mudou = true;
      atualizarAvisoGerar();
      removerDaTela(item);
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      ronuDefinirCarregando(botao, false);
      renderizarAcoes(item);
      ronuMostrarErroFormulario(elErro, erro.message);
    }
  }

  function atualizarAvisoGerar() {
    $('tr-gerar').hidden = !mudou;
    $('tr-gerar-celular').hidden = !mudou;
  }

  // ---------- Catálogo ----------

  function kanjiDe(nome) {
    return KANJI[nome.toLowerCase()] || '技';
  }

  function renderizarCatalogo() {
    const temNaLista = new Set(itens.map((i) => i.modalidade.id));
    const peso = simulacao ? simulacao.entradas.peso : null;

    elCartas.replaceChildren(...catalogo.map((modalidade) => {
      const tem = temNaLista.has(modalidade.id);
      const carta = criar('button', `tr-carta${tem ? ' tr-carta-tem' : ''}`);
      carta.type = 'button';
      carta.setAttribute('aria-label', tem
        ? `${modalidade.nome}, já está nos seus treinos. Abrir`
        : `Adicionar ${modalidade.nome}`);

      const arte = criar('span', 'tr-carta-arte');
      arte.setAttribute('aria-hidden', 'true');
      const zoom = criar('span', 'tr-carta-zoom grade');
      zoom.appendChild(criar('span', 'tr-carta-kanji', kanjiDe(modalidade.nome)));
      arte.appendChild(zoom);

      const textos = criar('span', 'tr-carta-textos');
      // ~kcal por hora com o seu peso: (MET − 1) × peso, só como referência.
      const porHora = peso ? ` · ~${formatarNumero((modalidade.metReferencia - 1) * peso)} kcal/h` : '';
      textos.append(
        criar('span', 'tr-carta-nome', modalidade.nome),
        criar('span', 'apoio num tr-so-largo', `Intensidade ${formatarDecimal(modalidade.metReferencia, 1)}${porHora}`),
        criar('span', 'tr-carta-acao', tem ? 'Já está nos seus treinos' : '+ Adicionar')
      );
      carta.append(arte, textos);
      carta.addEventListener('click', () => (tem ? abrirExistente(modalidade.id) : adicionar(modalidade)));
      return carta;
    }));
  }

  function abrirExistente(modalidadeId) {
    aberta = modalidadeId;
    atualizarAberturas();
    atualizarNumeros();
    const item = itens.find((i) => i.modalidade.id === modalidadeId);
    item.el.card.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    item.el.cabeca.focus({ preventScroll: true });
  }

  // Entra na tela aberta e sem dia; só vai para a API com o primeiro dia.
  function adicionar(modalidade) {
    const item = { modalidade, vinculoId: null, dias: new Set(), duracao: 60, salvo: null };
    criarCard(item);
    itens.push(item);
    aberta = modalidade.id;
    renderizarLista();
    mostrarStatus(item, 'Escolha os dias para adicionar aos seus treinos.', 'aviso');
    item.el.card.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    item.el.grupoDias.querySelector('button').focus({ preventScroll: true });
  }

  // ---------- Gasto por dia ----------

  function renderizarBarras() {
    const itemAberto = itens.find((i) => i.modalidade.id === aberta);
    const kcalAberta = itemAberto ? kcalSessao(itemAberto) || 0 : 0;
    const porDia = DIAS.map((dia) => {
      const total = simulacao ? (simulacao.dias.find((d) => d.diaSemana === dia.valor) || {}).treinoKcal || 0 : 0;
      const daAberta = itemAberto && itemAberto.dias.has(dia.valor) ? Math.min(kcalAberta, total) : 0;
      return { dia, total, daAberta };
    });
    const maior = Math.max(1200, ...porDia.map((d) => d.total));

    elBarras.replaceChildren(...porDia.map(({ dia, total, daAberta }, i) => {
      const coluna = criar('div', 'tr-coluna');
      const valor = criar('span', 'tr-coluna-valor num tr-so-largo', total ? formatarNumero(total) : '—');
      const trilho = criar('div', `tr-coluna-trilho${total ? '' : ' tr-coluna-descanso'}`);
      trilho.style.height = `${Math.max(4, (total / maior) * 100)}%`;
      if (total) {
        const barra = criar('div', 'tr-coluna-barra');
        barra.dataset.anima = 'barra-v';
        barra.style.setProperty('--aberta', `${((daAberta / total) * 100).toFixed(1)}%`);
        barra.style.setProperty('--atraso', `${i * 70}ms`);
        trilho.appendChild(barra);
      }
      // A altura em % da barra é da área, sem o valor e o dia em volta.
      const area = criar('div', 'tr-coluna-area');
      area.appendChild(trilho);
      const nome = criar('span', 'tr-coluna-dia', dia.curto);
      nome.setAttribute('aria-hidden', 'true');
      valor.setAttribute('aria-hidden', 'true');
      coluna.setAttribute('role', 'img');
      coluna.setAttribute('aria-label', total
        ? `${dia.nome}: ${formatarNumero(total)} kcal de treino`
        : `${dia.nome}: descanso`);
      coluna.append(valor, area, nome);
      return coluna;
    }));
  }

  function atualizarNumeros() {
    itens.forEach(atualizarCard);
    renderizarBarras();
    renderizarCatalogo();
    const total = simulacao ? simulacao.dias.reduce((soma, d) => soma + d.treinoKcal, 0) : null;
    $('tr-total-semana').textContent = total == null ? '—' : formatarNumero(total);
    $('tr-sessoes').textContent = String(itens.reduce((soma, i) => soma + i.dias.size, 0));
  }

  // ---------- Carga inicial ----------

  async function iniciar() {
    mostrarSomente(elLoading);
    try {
      const [respostaCatalogo, respostaPraticadas] = await Promise.all([
        fetch(`${RONU_CONFIG.API_BASE}/modalidades`),
        ronuFetchAutenticado('/usuarios/modalidades')
      ]);
      if (!respostaCatalogo.ok || !respostaPraticadas.ok) throw new Error('Falha ao carregar.');
      catalogo = await respostaCatalogo.json();
      const praticadas = await respostaPraticadas.json();

      itens = praticadas.map((vinculo) => {
        const modalidade = catalogo.find((m) => m.id === vinculo.modalidade.id) || vinculo.modalidade;
        const dias = [...vinculo.diasSemana].sort((a, b) => a - b);
        const duracao = horasParaMinutos(vinculo.duracaoMediaHoras);
        const item = { modalidade, vinculoId: vinculo.id, dias: new Set(dias), duracao, salvo: { dias, duracao } };
        criarCard(item);
        return item;
      });
      aberta = itens[0] ? itens[0].modalidade.id : null;

      mostrarSomente(elContent);
      renderizarLista();
      await simular();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') mostrarSomente(elLoadError);
    }
  }

  $('btn-tentar-novamente').addEventListener('click', iniciar);

  await iniciar();
});
