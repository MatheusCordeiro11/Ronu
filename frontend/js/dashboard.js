// Ronu — Dashboard (área do atleta)
// Fluxo: guarda de sessão -> checagem de cadastro completo (pulada se a marca
// de cadastro completo já existe; senão GETs simples, nunca chama a IA só pra
// validar) -> carrega o histórico de dietas (o item [0] já é a dieta atual,
// não precisa de uma chamada separada a /dietas/atual) -> renderiza.

document.addEventListener('DOMContentLoaded', async () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const elLoading = document.getElementById('dashboard-loading');
  const elLoadError = document.getElementById('dashboard-load-error');
  const elEmpty = document.getElementById('dashboard-empty');
  const elContent = document.getElementById('dashboard-content');
  const elGerarError = document.getElementById('dashboard-gerar-error');

  const elVersionSwitch = document.getElementById('version-switch');
  const elDayRow = document.getElementById('day-row');
  const elDayPanel = document.getElementById('day-panel');
  const elMetaData = document.getElementById('meta-data');
  const elMetaCalorias = document.getElementById('meta-calorias');
  const elMetaProteina = document.getElementById('meta-proteina');
  const elMetaCarbo = document.getElementById('meta-carbo');
  const elMetaGordura = document.getElementById('meta-gordura');
  const elAjuste = document.getElementById('meta-ajuste');
  const elAjusteMensagem = document.getElementById('meta-ajuste-mensagem');
  const elAjusteDetalhe = document.getElementById('meta-ajuste-detalhe');

  const btnGerar = document.getElementById('btn-gerar');
  const btnGerarVazio = document.getElementById('btn-gerar-vazio');
  const btnTentarNovamente = document.getElementById('btn-tentar-novamente');
  const btnLogout = document.getElementById('logout-btn');
  const elAvatarLink = document.getElementById('avatar-link');

  let historico = [];
  let versaoSelecionada = 0;
  let diaSelecionado = 0;

  elAvatarLink.textContent = ronuIniciais(usuario.nome);
  elAvatarLink.setAttribute('aria-label', `Configurações da conta — ${usuario.nome}`);
  elAvatarLink.title = 'Configurações';

  btnLogout.addEventListener('click', () => {
    ronuLimparSessao();
    window.location.href = 'login.html';
  });

  // ---------- Formatação ----------

  function formatarNumero(valor) {
    return Math.round(valor).toLocaleString('pt-BR');
  }

  function formatarData(isoString) {
    return new Date(isoString).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
  }

  // ---------- Estados de topo (mutuamente exclusivos) ----------

  function mostrarSomente(elementoVisivel) {
    [elLoading, elLoadError, elEmpty, elContent].forEach((el) => {
      el.hidden = el !== elementoVisivel;
    });
  }

  // ---------- Render: carimbo de meta diária ----------

  // A meta agora varia por dia (dia de treino vs. dia de descanso), então lê
  // sempre do dia atualmente selecionado (dietaResponse.dieta.dias[diaSelecionado]),
  // não mais de um campo único da dieta inteira — esse campo não existe mais
  // no backend (cada dia carrega sua própria meta calculada).
  function renderizarMetaStamp(dietaResponse) {
    const dia = dietaResponse.dieta.dias[diaSelecionado];
    const meta = dia.metaCalculada;
    elMetaData.textContent = `${dia.diaSemana} · gerada em ${formatarData(dietaResponse.dataGeracao)}`;
    elMetaCalorias.textContent = `${formatarNumero(meta.calorias)} kcal`;
    elMetaProteina.textContent = `${formatarNumero(meta.proteinasG)} g`;
    elMetaCarbo.textContent = `${formatarNumero(meta.carboidratosG)} g`;
    elMetaGordura.textContent = `${formatarNumero(meta.gordurasG)} g`;
  }

  // ---------- Render: ajuste pelo progresso (meta adaptativa) ----------

  // O ajuste é um registro de como ESTA dieta foi feita (vale para a semana
  // toda, não muda com o dia), por isso os textos falam "até esta dieta ser
  // gerada", nunca "agora". Nada de kcal: em dias com MetaElevadaPeloPiso a
  // diferença não dá para derivar.

  // Abaixo de 50 g/semana o texto trata como "praticamente estável" / "no
  // ritmo". Só afeta a redação — a classificação da causa usa o sinal exato.
  const TOLERANCIA_RITMO_KG = 0.05;

  function formatarPercentualAjuste(percentual) {
    return `${(Math.abs(percentual) * 100).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
  }

  function formatarPesoSemana(kg) {
    const absoluto = Math.abs(kg);
    if (absoluto >= 1) {
      return `${absoluto.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} kg`;
    }
    return `${Math.round(absoluto * 100) * 10} g`;
  }

  function frasePesoReal(ritmo) {
    if (Math.abs(ritmo) < TOLERANCIA_RITMO_KG) return 'ficou praticamente estável';
    const verbo = ritmo < 0 ? 'caiu' : 'subiu';
    return `${verbo} em média cerca de ${formatarPesoSemana(ritmo)} por semana`;
  }

  // O sinal do ritmo esperado diz o objetivo da dieta (a dieta não guarda o
  // objetivo): negativo = perder, positivo = ganhar, zero = manter.
  function fraseEsperado(ritmo) {
    if (ritmo === 0) return 'ficar estável';
    const verbo = ritmo < 0 ? 'cair' : 'subir';
    return `${verbo} cerca de ${formatarPesoSemana(ritmo)} por semana`;
  }

  function criarParagrafo(...partes) {
    const p = document.createElement('p');
    p.append(...partes);
    return p;
  }

  const RODAPE_AJUSTE = 'O ajuste é gradual, de no máximo 5% por vez, e é refeito do zero a cada nova dieta.';

  // Ajuste aplicado (para menos ou para mais). O motivo vem do sinal do
  // fatorBruto = (ingestão − 1) − (real − esperado)·k, com k > 0. Se a meta
  // caiu mas o peso NÃO está acima do esperado (real ≤ esperado), só a
  // ingestão < 1 explica — a pessoa contou que comeu menos. O inverso vale
  // para a meta que subiu. Nos outros casos o peso explica, e a aderência
  // pode ter somado ou amenizado (o DTO não diz quanto).
  function detalheAjusteAplicado(ajuste, paraMenos) {
    const real = ajuste.ritmoRealKgSemana;
    const esperado = ajuste.ritmoEsperadoKgSemana;
    const percentual = formatarPercentualAjuste(ajuste.percentual);
    const maiorMenor = paraMenos ? 'menor' : 'maior';
    const resultado = `então a meta desta dieta ficou ${percentual} ${maiorMenor}`;

    if (real == null || esperado == null) {
      return [
        criarParagrafo(`Pelo seu histórico de peso, seu gasto parece um pouco ${maiorMenor} do que o Ronu estimou pelo seu corpo e pelo seu treino, ${resultado}.`),
        criarParagrafo(RODAPE_AJUSTE)
      ];
    }

    const diferenca = real - esperado;
    const abertura = `Nas pesagens até esta dieta ser gerada, seu peso ${frasePesoReal(real)}`;
    const explicadoPelaAderencia = paraMenos ? diferenca <= 0 : diferenca >= 0;

    let texto;
    if (!explicadoPelaAderencia) {
      texto = `${abertura}. Para o seu objetivo, o esperado era ${fraseEsperado(esperado)}. `
        + `Considerando esse ritmo (e suas respostas sobre seguir a dieta, quando houver), `
        + `seu gasto parece um pouco ${maiorMenor} do que o Ronu estimou pelo seu corpo e pelo seu treino, ${resultado}.`;
    } else {
      const comendo = `você contou que vem comendo um pouco ${paraMenos ? 'menos' : 'mais'} do que a dieta pede`;
      const conclusao = `seu gasto parece um pouco ${maiorMenor} do que o Ronu estimou, ${resultado}, mais perto do que você realmente come.`;
      texto = Math.abs(diferenca) < TOLERANCIA_RITMO_KG
        ? `${abertura}, no ritmo esperado para o seu objetivo. Mas ${comendo}. Pelo seu peso e pelo que você come, ${conclusao}`
        : `${abertura}; para o seu objetivo, o esperado era ${fraseEsperado(esperado)}. `
          + `O que explica o ajuste é que ${comendo}: pelo seu peso e pelo que você come, ${conclusao}`;
    }

    return [criarParagrafo(texto), criarParagrafo(RODAPE_AJUSTE)];
  }

  // Dentro da zona morta (|fatorBruto| < 1%). Com o peso a 50 g ou mais do
  // esperado, só a ingestão relatada compensa a diferença — mas o sentido
  // dela não é dito, porque depende da meta base, que o DTO não traz.
  function detalheDentroDoEsperado(ajuste) {
    const real = ajuste.ritmoRealKgSemana;
    const esperado = ajuste.ritmoEsperadoKgSemana;
    if (real == null || esperado == null) {
      return [criarParagrafo('Pelo seu histórico de peso, seu progresso está no ritmo, então a meta não mudou.')];
    }

    const base = `Nas pesagens até esta dieta ser gerada, seu peso ${frasePesoReal(real)}; `
      + `para o seu objetivo, o esperado era ${fraseEsperado(esperado)}.`;
    const fecho = Math.abs(real - esperado) < TOLERANCIA_RITMO_KG
      ? ' Está no ritmo, então a meta não mudou. Diferenças pequenas não mexem na meta, para ela não oscilar à toa.'
      : ' A diferença é explicada pelo que você contou sobre seguir a dieta, então a meta não mudou.';
    return [criarParagrafo(base + fecho)];
  }

  // Sem dado suficiente: menos de 5 pesagens, ou elas não cobrem 14 dias
  // (dentro dos últimos 28, no objetivo atual, sem "não deu pra seguir").
  // PontosUsados não diz quantos dias faltam, só quantas pesagens contaram.
  function detalheHistoricoInsuficiente(ajuste) {
    const n = ajuste.pontosUsados ?? 0;
    let situacao;
    if (n === 0) situacao = 'ainda não havia pesagens nesse período';
    else if (n === 1) situacao = 'havia 1 pesagem nesse período';
    else if (n < 5) situacao = `havia ${n} pesagens nesse período`;
    else situacao = `havia ${n} pesagens, mas elas ainda não cobriam 2 semanas`;

    const link = document.createElement('a');
    link.href = 'progresso.html';
    link.textContent = 'Ver progresso';

    return [
      criarParagrafo(
        'O Ronu ajusta a meta pelo seu progresso quando tem pelo menos 5 pesagens cobrindo 2 semanas ou mais, '
        + `nas últimas 4 semanas e com o mesmo objetivo. Quando esta dieta foi gerada, ${situacao}.`
      ),
      criarParagrafo(
        'Pesagens marcadas como “não deu pra seguir” não entram na conta. Continue registrando seu peso em ',
        link,
        '.'
      )
    ];
  }

  // Devolve { aplicado, mensagem, detalhe } ou null (dieta anterior à meta
  // adaptativa, ou um motivo que esta tela ainda não conhece).
  function montarAjuste(ajuste) {
    if (!ajuste) return null;

    switch (ajuste.motivo) {
      case 'PesoAcimaDoEsperado':
      case 'PesoAbaixoDoEsperado': {
        const paraMenos = ajuste.motivo === 'PesoAcimaDoEsperado';
        const valor = document.createElement('strong');
        valor.className = 'num';
        valor.textContent = `${formatarPercentualAjuste(ajuste.percentual)} ${paraMenos ? 'menor' : 'maior'}`;
        return {
          aplicado: true,
          mensagem: ['Meta ajustada pelo seu progresso: ', valor, '.'],
          detalhe: detalheAjusteAplicado(ajuste, paraMenos)
        };
      }
      case 'DentroDoEsperado':
        return {
          aplicado: false,
          mensagem: ['Meta mantida: seu progresso está dentro do esperado.'],
          detalhe: detalheDentroDoEsperado(ajuste)
        };
      case 'HistoricoInsuficiente':
        return {
          aplicado: false,
          mensagem: ['Ajuste pelo seu progresso ainda não ativo.'],
          detalhe: detalheHistoricoInsuficiente(ajuste)
        };
      default:
        return null;
    }
  }

  function renderizarAjuste(dietaResponse) {
    const conteudo = montarAjuste(dietaResponse.dieta.ajusteAdaptativo);
    elAjuste.hidden = !conteudo;
    if (!conteudo) return;

    elAjuste.dataset.estado = conteudo.aplicado ? 'aplicado' : 'neutro';
    elAjusteMensagem.replaceChildren(...conteudo.mensagem);
    elAjusteDetalhe.replaceChildren(...conteudo.detalhe);
  }

  // ---------- Render: seletor de versão (atual / histórico) ----------

  function renderizarVersionSwitch() {
    elVersionSwitch.innerHTML = '';
    elVersionSwitch.hidden = historico.length <= 1;

    historico.forEach((dietaResponse, indice) => {
      const botao = document.createElement('button');
      botao.type = 'button';
      botao.className = 'version-pill';
      botao.setAttribute('role', 'tab');
      botao.setAttribute('aria-selected', String(indice === versaoSelecionada));
      botao.textContent = indice === 0 ? 'Atual' : `Anterior · ${formatarData(dietaResponse.dataGeracao)}`;

      botao.addEventListener('click', () => {
        versaoSelecionada = indice;
        diaSelecionado = 0;
        renderizarVersaoSelecionada();
      });

      elVersionSwitch.appendChild(botao);
    });
  }

  // ---------- Render: fileira de dias ----------

  function renderizarDayRow(dietaResponse) {
    elDayRow.innerHTML = '';

    dietaResponse.dieta.dias.forEach((dia, indice) => {
      const botao = document.createElement('button');
      botao.type = 'button';
      botao.className = 'day-pill';
      botao.setAttribute('role', 'tab');
      botao.setAttribute('aria-selected', String(indice === diaSelecionado));

      const label = document.createElement('span');
      label.className = 'day-pill-label';
      label.textContent = dia.diaSemana;

      const kcal = document.createElement('span');
      kcal.className = 'day-pill-kcal num';
      kcal.textContent = `${formatarNumero(dia.totalDoDia.calorias)} kcal`;

      botao.append(label, kcal);

      // Trocar de dia agora também atualiza o carimbo de meta do topo (a
      // meta varia por dia), além do painel de refeições que já atualizava.
      botao.addEventListener('click', () => {
        diaSelecionado = indice;
        renderizarMetaStamp(dietaResponse);
        renderizarDayPanel(dietaResponse);
        Array.from(elDayRow.children).forEach((filho, i) => {
          filho.setAttribute('aria-selected', String(i === diaSelecionado));
        });
      });

      elDayRow.appendChild(botao);
    });
  }

  // ---------- Render: painel do dia (refeições) ----------

  function criarLinhaAlimento(alimento) {
    const item = document.createElement('li');
    item.className = 'meal-food-item';

    const nome = document.createElement('span');
    nome.className = 'meal-food-name';
    nome.textContent = alimento.nome;

    const qtd = document.createElement('span');
    qtd.className = 'meal-food-qty num';
    qtd.textContent = `${formatarNumero(alimento.quantidade)} ${alimento.unidade}`;

    item.append(nome, qtd);
    return item;
  }

  function criarCardRefeicao(refeicao) {
    const card = document.createElement('article');
    card.className = 'meal-card';

    const head = document.createElement('header');
    head.className = 'meal-card-head';

    const nome = document.createElement('h3');
    nome.className = 'meal-name';
    nome.textContent = refeicao.nome;

    // Horário antes do nome, como numa agenda. Dietas geradas antes do campo
    // existir vêm com horario null — aí o card fica só com o nome.
    const titulo = document.createElement('div');
    titulo.className = 'meal-title';
    if (refeicao.horario) {
      const horario = document.createElement('time');
      horario.className = 'meal-time num';
      horario.dateTime = refeicao.horario;
      horario.textContent = refeicao.horario;
      titulo.appendChild(horario);
    }
    titulo.appendChild(nome);

    const macros = document.createElement('dl');
    macros.className = 'meal-macros num';
    macros.innerHTML = `
      <div><dt>kcal</dt><dd>${formatarNumero(refeicao.macros.calorias)}</dd></div>
      <div><dt>P</dt><dd>${formatarNumero(refeicao.macros.proteinasG)}g</dd></div>
      <div><dt>C</dt><dd>${formatarNumero(refeicao.macros.carboidratosG)}g</dd></div>
      <div><dt>G</dt><dd>${formatarNumero(refeicao.macros.gordurasG)}g</dd></div>
    `;

    head.append(titulo, macros);

    const lista = document.createElement('ul');
    lista.className = 'meal-food-list';
    refeicao.alimentos.forEach((alimento) => lista.appendChild(criarLinhaAlimento(alimento)));

    card.append(head, lista);
    return card;
  }

  function renderizarDayPanel(dietaResponse) {
    const dia = dietaResponse.dieta.dias[diaSelecionado];
    elDayPanel.innerHTML = '';

    const total = document.createElement('div');
    total.className = 'day-total num';
    total.innerHTML = `
      <span>Total do dia: <strong>${formatarNumero(dia.totalDoDia.calorias)} kcal</strong></span>
      <span>P <strong>${formatarNumero(dia.totalDoDia.proteinasG)}g</strong></span>
      <span>C <strong>${formatarNumero(dia.totalDoDia.carboidratosG)}g</strong></span>
      <span>G <strong>${formatarNumero(dia.totalDoDia.gordurasG)}g</strong></span>
    `;

    const lista = document.createElement('div');
    lista.className = 'meal-list';
    dia.refeicoes.forEach((refeicao) => lista.appendChild(criarCardRefeicao(refeicao)));

    elDayPanel.append(total, lista);
  }

  // ---------- Orquestração ----------

  function renderizarVersaoSelecionada() {
    const dietaResponse = historico[versaoSelecionada];
    renderizarMetaStamp(dietaResponse);
    renderizarAjuste(dietaResponse);
    renderizarVersionSwitch();
    renderizarDayRow(dietaResponse);
    renderizarDayPanel(dietaResponse);
  }

  function renderizarTudo() {
    if (historico.length === 0) {
      mostrarSomente(elEmpty);
      return;
    }

    renderizarVersaoSelecionada();
    mostrarSomente(elContent);
  }

  async function carregarHistorico() {
    const resposta = await ronuFetchAutenticado('/dietas/historico');
    if (!resposta.ok) throw new Error('Não foi possível carregar suas dietas.');
    historico = await resposta.json();
  }

  async function gerarDieta(botao) {
    // O estado de carregamento só bloqueia o mouse (pointer-events: none) —
    // Enter/Espaço com o botão em foco disparariam outra geração no meio da
    // atual, por isso a ativação é ignorada aqui enquanto ele carrega.
    if (botao.dataset.loading === 'true') return;

    ronuOcultarErroFormulario(elGerarError);
    ronuDefinirCarregando(botao, true, 'Montando sua dieta da semana...');
    botao.setAttribute('aria-busy', 'true');

    try {
      const resposta = await ronuFetchAutenticado('/dietas/gerar', { method: 'POST' });

      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível gerar sua dieta. Tente novamente.');
      }

      await carregarHistorico();
      versaoSelecionada = 0;
      diaSelecionado = 0;
      renderizarTudo();
    } catch (erro) {
      ronuMostrarErroFormulario(elGerarError, erro.message);
    } finally {
      ronuDefinirCarregando(botao, false);
      botao.removeAttribute('aria-busy');
    }
  }

  btnGerar.addEventListener('click', () => gerarDieta(btnGerar));
  btnGerarVazio.addEventListener('click', () => gerarDieta(btnGerarVazio));

  // ---------- Carga inicial ----------

  async function iniciar() {
    mostrarSomente(elLoading);

    // Cadastro já visto completo neste navegador (marca gravada pela checagem
    // pós-login ou por uma abertura anterior): vai direto ao histórico. Sem a
    // marca (acesso direto, sessão antiga), faz a checagem completa — GETs
    // simples, nunca POST /dietas/gerar, que gastaria cota da Gemini à toa.
    try {
      if (!ronuCadastroJaCompleto() && !(await ronuChecarCadastroCompleto())) {
        window.location.href = 'onboarding.html';
        return;
      }
    } catch (erro) {
      // Em caso de 401, ronuFetchAutenticado já redirecionou pro login
      // sozinho — não sobrescrever esse redirecionamento com outro (mesmo
      // cuidado já tomado em onboarding.js). Qualquer outro erro (rede fora
      // do ar) mostra o estado de erro desta própria tela.
      if (erro.message !== 'Sessão expirada.') {
        mostrarSomente(elLoadError);
      }
      return;
    }

    try {
      await carregarHistorico();
      renderizarTudo();
    } catch (erro) {
      mostrarSomente(elLoadError);
    }
  }

  btnTentarNovamente.addEventListener('click', iniciar);

  await iniciar();
});
