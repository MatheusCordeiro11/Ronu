// Ronu — Dashboard (área do atleta)
// Fluxo: guarda de sessão -> checagem de perfil/objetivo (GET, nunca chama
// a IA só pra validar) -> carrega o histórico de dietas (o item [0] já é a
// dieta atual, não precisa de uma chamada separada a /dietas/atual) -> renderiza.

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

  const btnGerar = document.getElementById('btn-gerar');
  const btnGerarVazio = document.getElementById('btn-gerar-vazio');
  const btnTentarNovamente = document.getElementById('btn-tentar-novamente');
  const btnLogout = document.getElementById('logout-btn');

  let historico = [];
  let versaoSelecionada = 0;
  let diaSelecionado = 0;

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

  function renderizarMetaStamp(dietaResponse) {
    const meta = dietaResponse.dieta.metaDiariaCalculada;
    elMetaData.textContent = `Gerada em ${formatarData(dietaResponse.dataGeracao)}`;
    elMetaCalorias.textContent = `${formatarNumero(meta.calorias)} kcal`;
    elMetaProteina.textContent = `${formatarNumero(meta.proteinasG)} g`;
    elMetaCarbo.textContent = `${formatarNumero(meta.carboidratosG)} g`;
    elMetaGordura.textContent = `${formatarNumero(meta.gordurasG)} g`;
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

      botao.addEventListener('click', () => {
        diaSelecionado = indice;
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

    const macros = document.createElement('dl');
    macros.className = 'meal-macros num';
    macros.innerHTML = `
      <div><dt>kcal</dt><dd>${formatarNumero(refeicao.macros.calorias)}</dd></div>
      <div><dt>P</dt><dd>${formatarNumero(refeicao.macros.proteinasG)}g</dd></div>
      <div><dt>C</dt><dd>${formatarNumero(refeicao.macros.carboidratosG)}g</dd></div>
      <div><dt>G</dt><dd>${formatarNumero(refeicao.macros.gordurasG)}g</dd></div>
    `;

    head.append(nome, macros);

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
    ronuOcultarErroFormulario(elGerarError);
    ronuDefinirCarregando(botao, true, 'Montando sua dieta da semana...');

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
    }
  }

  btnGerar.addEventListener('click', () => gerarDieta(btnGerar));
  btnGerarVazio.addEventListener('click', () => gerarDieta(btnGerarVazio));

  // ---------- Carga inicial ----------

  async function iniciar() {
    mostrarSomente(elLoading);

    // Checagem de perfil/objetivo feita com GETs simples — nunca chamando
    // POST /dietas/gerar só pra validar, o que gastaria cota da Gemini à toa.
    try {
      const [respostaPerfil, respostaObjetivo] = await Promise.all([
        ronuFetchAutenticado('/perfil'),
        ronuFetchAutenticado('/objetivos/atual')
      ]);

      const perfil = respostaPerfil.ok ? await respostaPerfil.json() : null;
      const perfilCompleto = Boolean(
        perfil && perfil.altura != null && perfil.sexo != null && perfil.dataNascimento != null
      );
      const temObjetivo = respostaObjetivo.status === 200;

      if (!perfilCompleto || !temObjetivo) {
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
