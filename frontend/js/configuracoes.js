// Ronu — Configurações (conta)
// Três seções independentes: perfil (editar, PUT /perfil), modalidades
// (listar/adicionar/remover/editar frequência) e preferências alimentares
// (listar/adicionar/remover). Cada seção carrega e falha por conta própria —
// um erro numa não deve impedir as outras de funcionar.

document.addEventListener('DOMContentLoaded', async () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const btnLogout = document.getElementById('logout-btn');
  btnLogout.addEventListener('click', () => {
    ronuLimparSessao();
    window.location.href = 'login.html';
  });

  // ---------- Perfil ----------

  const elPerfilLoading = document.getElementById('perfil-loading');
  const elPerfilForm = document.getElementById('perfil-form');
  const elPerfilError = document.getElementById('perfil-error');
  const elPerfilSuccess = document.getElementById('perfil-success');
  const elAltura = document.getElementById('perfil-altura');
  const campoAltura = document.getElementById('field-perfil-altura');
  const alturaErro = document.getElementById('perfil-altura-error');
  const elDataNascimento = document.getElementById('perfil-data-nascimento');
  const btnSalvarPerfil = document.getElementById('btn-salvar-perfil');

  // Mesma faixa usada no onboarding: data de nascimento não pode ser
  // hoje/futuro nem implicar uma idade absurda.
  function definirFaixaDataNascimento() {
    const hoje = new Date();
    const cemAnosAtras = new Date(hoje.getFullYear() - 100, hoje.getMonth(), hoje.getDate());
    elDataNascimento.max = hoje.toISOString().split('T')[0];
    elDataNascimento.min = cemAnosAtras.toISOString().split('T')[0];
  }

  async function carregarPerfil() {
    try {
      const resposta = await ronuFetchAutenticado('/perfil');
      if (!resposta.ok) throw new Error();

      const perfil = await resposta.json();
      elAltura.value = perfil.altura != null ? ronuFormatarAlturaMetros(perfil.altura) : '';
      elDataNascimento.value = perfil.dataNascimento ?? '';
      if (perfil.sexo) {
        const radio = document.querySelector(`input[name="perfil-sexo"][value="${perfil.sexo}"]`);
        if (radio) radio.checked = true;
      }

      elPerfilLoading.hidden = true;
      elPerfilForm.hidden = false;
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        elPerfilLoading.textContent = 'Não foi possível carregar seus dados. Recarregue a página.';
      }
    }
  }

  async function salvarPerfil(evento) {
    evento.preventDefault();

    const sexoSelecionado = document.querySelector('input[name="perfil-sexo"]:checked');

    ronuLimparCampoInvalido(campoAltura, alturaErro);

    if (!elAltura.reportValidity()) return;

    // Campo é texto (pra aceitar vírgula), então a faixa 1,00–2,50 m não pode
    // ser validada via min/max nativo — checada manualmente em centímetros.
    const alturaCm = ronuParseAlturaCm(elAltura.value);
    if (alturaCm === null || alturaCm < 100 || alturaCm > 250) {
      ronuMarcarCampoInvalido(campoAltura, alturaErro, 'Informe uma altura entre 1,00 e 2,50 m.');
      return;
    }

    if (!sexoSelecionado) {
      ronuMostrarErroFormulario(elPerfilError, 'Selecione seu sexo.');
      return;
    }
    if (!elDataNascimento.reportValidity()) return;

    ronuOcultarErroFormulario(elPerfilError);
    elPerfilSuccess.hidden = true;
    ronuDefinirCarregando(btnSalvarPerfil, true, 'Salvando...');

    try {
      const resposta = await ronuFetchAutenticado('/perfil', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          altura: alturaCm,
          sexo: sexoSelecionado.value,
          dataNascimento: elDataNascimento.value
        })
      });

      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível salvar. Tente novamente.');
      }

      elPerfilSuccess.textContent = 'Dados salvos.';
      elPerfilSuccess.hidden = false;
    } catch (erro) {
      ronuMostrarErroFormulario(elPerfilError, erro.message);
    } finally {
      ronuDefinirCarregando(btnSalvarPerfil, false);
    }
  }

  elPerfilForm.addEventListener('submit', salvarPerfil);

  // ---------- Modalidades ----------
  // Cada modalidade praticada é um vínculo (UsuarioModalidade) com dias da
  // semana + duração média editáveis e ação de remover; "adicionar" só
  // oferece as modalidades do catálogo que o usuário ainda não pratica. Os
  // toggles de dia (.dia-toggle/.modalidade-dias) e o campo de duração
  // (.modalidade-duracao) vêm de onboarding.css, já carregado nesta página —
  // mesmo widget usado no Passo 3 do onboarding, sem duplicar CSS. Reaproveita
  // a mecânica de "Remover -> Sim/Cancelar" de Preferências, mas com classes
  // próprias — seção independente, sem acoplar às internals da outra.

  const elModalidadesLoading = document.getElementById('modalidades-loading');
  const elModalidadesError = document.getElementById('modalidades-error');
  const elModalidadeList = document.getElementById('modalidade-config-list');
  const elModalidadeAddForm = document.getElementById('modalidade-add-form');
  const elModalidadeAddOptions = document.getElementById('modalidade-add-options');
  const elModalidadeAddCollapse = document.getElementById('modalidade-add-collapse');
  const elModalidadeAddDias = document.getElementById('modalidade-add-dias');
  const elModalidadeAddDuracao = document.getElementById('modalidade-add-duracao');
  const elModalidadesCatalogoCompleto = document.getElementById('modalidades-catalogo-completo');
  const btnAdicionarModalidade = document.getElementById('btn-adicionar-modalidade');

  // 1=Segunda ... 7=Domingo (ISO 8601, mesmo padrão do backend e do
  // onboarding — abreviação de 3 letras pra não colidir em pt-BR).
  const DIAS_SEMANA = [
    { valor: 1, rotulo: 'SEG', nomeCompleto: 'segunda-feira' },
    { valor: 2, rotulo: 'TER', nomeCompleto: 'terça-feira' },
    { valor: 3, rotulo: 'QUA', nomeCompleto: 'quarta-feira' },
    { valor: 4, rotulo: 'QUI', nomeCompleto: 'quinta-feira' },
    { valor: 5, rotulo: 'SEX', nomeCompleto: 'sexta-feira' },
    { valor: 6, rotulo: 'SÁB', nomeCompleto: 'sábado' },
    { valor: 7, rotulo: 'DOM', nomeCompleto: 'domingo' }
  ];

  let catalogoModalidades = [];

  function modalidadesJaAdicionadas() {
    return new Set(
      Array.from(elModalidadeList.querySelectorAll('.modalidade-item')).map(
        (item) => Number(item.dataset.modalidadeId)
      )
    );
  }

  function renderizarAcaoRemoverModalidade(elAcoes) {
    elAcoes.innerHTML = '';

    const botao = document.createElement('button');
    botao.type = 'button';
    botao.className = 'modalidade-remove-btn';
    botao.dataset.acao = 'remover';
    botao.textContent = 'Remover';

    elAcoes.appendChild(botao);
  }

  function renderizarConfirmacaoRemoverModalidade(elAcoes) {
    elAcoes.innerHTML = '';

    const texto = document.createElement('span');
    texto.className = 'modalidade-confirm-text';
    texto.textContent = 'Remover?';

    const btnConfirmar = document.createElement('button');
    btnConfirmar.type = 'button';
    btnConfirmar.className = 'modalidade-confirm-btn';
    btnConfirmar.dataset.acao = 'confirmar';
    btnConfirmar.textContent = 'Sim';

    const btnCancelar = document.createElement('button');
    btnCancelar.type = 'button';
    btnCancelar.className = 'modalidade-cancel-btn';
    btnCancelar.dataset.acao = 'cancelar';
    btnCancelar.textContent = 'Cancelar';

    elAcoes.append(texto, btnConfirmar, btnCancelar);
  }

  function criarItemModalidade(vinculo) {
    const item = document.createElement('li');
    item.className = 'modalidade-item';
    item.dataset.vinculoId = vinculo.id;
    item.dataset.modalidadeId = vinculo.modalidade.id;
    // Baseline pra saber se os dias mudaram desde o último salvo — dataset
    // (não uma const capturada no closure) porque salvarModalidade() roda
    // fora deste escopo, disparada pela delegação de evento da lista.
    item.dataset.diasSalvos = JSON.stringify(vinculo.diasSemana.slice().sort((a, b) => a - b));

    const info = document.createElement('div');
    info.className = 'modalidade-item-info';

    const nomeSpan = document.createElement('span');
    nomeSpan.className = 'modalidade-item-nome';
    nomeSpan.textContent = vinculo.modalidade.nome;

    const detalhes = document.createElement('div');
    detalhes.className = 'modalidade-item-detalhes';

    const diasGroup = document.createElement('div');
    diasGroup.className = 'modalidade-dias';
    diasGroup.setAttribute('role', 'group');
    diasGroup.setAttribute('aria-label', `Dias da semana de treino de ${vinculo.modalidade.nome}`);

    const diasAtivos = new Set(vinculo.diasSemana);
    const btnSalvar = document.createElement('button');

    function diasSelecionadosOrdenados() {
      return Array.from(diasGroup.querySelectorAll('.dia-toggle'))
        .filter((botao) => botao.getAttribute('aria-pressed') === 'true')
        .map((botao) => Number(botao.dataset.dia))
        .sort((a, b) => a - b);
    }

    // Só habilita "Salvar" quando dias OU duração diferem do último salvo —
    // evita autosave silencioso, consistente com o resto da página.
    function atualizarEstadoSalvar() {
      const diasMudaram = JSON.stringify(diasSelecionadosOrdenados()) !== item.dataset.diasSalvos;
      const duracaoMudou = duracaoInput.value !== duracaoInput.dataset.valorSalvo;
      btnSalvar.disabled = (!diasMudaram && !duracaoMudou) || duracaoInput.value === '';
    }

    DIAS_SEMANA.forEach(({ valor, rotulo, nomeCompleto }) => {
      const botao = document.createElement('button');
      botao.type = 'button';
      botao.className = 'dia-toggle';
      botao.dataset.dia = valor;
      botao.setAttribute('aria-pressed', String(diasAtivos.has(valor)));
      botao.setAttribute('aria-label', nomeCompleto);
      botao.textContent = rotulo;

      botao.addEventListener('click', () => {
        const pressionado = botao.getAttribute('aria-pressed') === 'true';
        botao.setAttribute('aria-pressed', String(!pressionado));
        atualizarEstadoSalvar();
      });

      diasGroup.appendChild(botao);
    });

    const duracaoField = document.createElement('div');
    duracaoField.className = 'modalidade-duracao-field';

    const duracaoLabel = document.createElement('label');
    duracaoLabel.textContent = 'Duração média por sessão (h)';
    duracaoLabel.htmlFor = `modalidade-duracao-${vinculo.id}`;

    const duracaoInput = document.createElement('input');
    duracaoInput.type = 'number';
    duracaoInput.id = `modalidade-duracao-${vinculo.id}`;
    duracaoInput.className = 'modalidade-duracao num';
    duracaoInput.min = '0.25';
    duracaoInput.max = '5';
    duracaoInput.step = '0.25';
    duracaoInput.value = vinculo.duracaoMediaHoras;
    duracaoInput.dataset.valorSalvo = String(vinculo.duracaoMediaHoras);
    // aria-label prevalece sobre o <label> visível pra leitor de tela — texto
    // mais específico (com o nome da modalidade) do que o rótulo compartilhado.
    duracaoInput.setAttribute('aria-label', `Duração média por sessão de ${vinculo.modalidade.nome}, em horas`);
    duracaoInput.addEventListener('input', atualizarEstadoSalvar);

    duracaoField.append(duracaoLabel, duracaoInput);

    btnSalvar.type = 'button';
    btnSalvar.className = 'modalidade-save-btn';
    btnSalvar.dataset.acao = 'salvar-modalidade';
    btnSalvar.textContent = 'Salvar';
    btnSalvar.disabled = true;

    detalhes.append(diasGroup, duracaoField, btnSalvar);
    info.append(nomeSpan, detalhes);

    const acoes = document.createElement('div');
    acoes.className = 'modalidade-item-actions';
    renderizarAcaoRemoverModalidade(acoes);

    item.append(info, acoes);
    return item;
  }

  function mostrarListaModalidades() {
    elModalidadeList.hidden = elModalidadeList.children.length === 0;
  }

  // Recalculada a cada adição/remoção: as pills de "adicionar" só mostram
  // modalidades do catálogo que ainda não estão na lista de praticadas.
  function renderizarOpcoesAdicionar() {
    const jaAdicionadas = modalidadesJaAdicionadas();
    const disponiveis = catalogoModalidades.filter((modalidade) => !jaAdicionadas.has(modalidade.id));

    elModalidadeAddOptions.innerHTML = '';
    disponiveis.forEach((modalidade) => {
      const pill = document.createElement('label');
      pill.className = 'option-pill';

      const radio = document.createElement('input');
      radio.type = 'radio';
      radio.name = 'modalidade-add-escolha';
      radio.value = String(modalidade.id);

      const span = document.createElement('span');
      span.textContent = modalidade.nome;

      pill.append(radio, span);
      elModalidadeAddOptions.appendChild(pill);
    });

    Array.from(elModalidadeAddDias.querySelectorAll('.dia-toggle')).forEach((botao) => {
      botao.setAttribute('aria-pressed', 'false');
      botao.disabled = true;
    });
    elModalidadeAddDuracao.value = '';
    elModalidadeAddDuracao.disabled = true;
    elModalidadeAddCollapse.classList.remove('is-aberto');

    const temDisponiveis = disponiveis.length > 0;
    elModalidadeAddForm.hidden = !temDisponiveis;
    elModalidadesCatalogoCompleto.hidden = temDisponiveis;
  }

  elModalidadeAddOptions.addEventListener('change', (evento) => {
    if (evento.target.name !== 'modalidade-add-escolha') return;
    elModalidadeAddCollapse.classList.add('is-aberto');
    const botoesDia = Array.from(elModalidadeAddDias.querySelectorAll('.dia-toggle'));
    botoesDia.forEach((botao) => { botao.disabled = false; });
    elModalidadeAddDuracao.disabled = false;
    botoesDia[0].focus();
  });

  elModalidadeAddDias.addEventListener('click', (evento) => {
    const botao = evento.target.closest('.dia-toggle');
    if (!botao) return;
    const pressionado = botao.getAttribute('aria-pressed') === 'true';
    botao.setAttribute('aria-pressed', String(!pressionado));
  });

  async function carregarModalidades() {
    try {
      const [respostaCatalogo, respostaPraticadas] = await Promise.all([
        fetch(`${RONU_CONFIG.API_BASE}/modalidades`),
        ronuFetchAutenticado('/usuarios/modalidades')
      ]);

      if (!respostaCatalogo.ok || !respostaPraticadas.ok) throw new Error();

      catalogoModalidades = await respostaCatalogo.json();
      const praticadas = await respostaPraticadas.json();

      elModalidadeList.innerHTML = '';
      praticadas.forEach((vinculo) => elModalidadeList.appendChild(criarItemModalidade(vinculo)));

      elModalidadesLoading.hidden = true;
      mostrarListaModalidades();
      renderizarOpcoesAdicionar();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        elModalidadesLoading.textContent = 'Não foi possível carregar suas modalidades. Recarregue a página.';
      }
    }
  }

  async function adicionarModalidade() {
    const escolhida = elModalidadeAddOptions.querySelector('input[name="modalidade-add-escolha"]:checked');

    if (!escolhida) {
      ronuMostrarErroFormulario(elModalidadesError, 'Selecione uma modalidade.');
      return;
    }

    const diasSemana = Array.from(elModalidadeAddDias.querySelectorAll('.dia-toggle'))
      .filter((botao) => botao.getAttribute('aria-pressed') === 'true')
      .map((botao) => Number(botao.dataset.dia));

    if (diasSemana.length === 0) {
      ronuMostrarErroFormulario(elModalidadesError, 'Selecione pelo menos um dia da semana.');
      return;
    }

    if (!elModalidadeAddDuracao.reportValidity()) return;

    ronuOcultarErroFormulario(elModalidadesError);
    ronuDefinirCarregando(btnAdicionarModalidade, true, 'Adicionando...');

    try {
      const resposta = await ronuFetchAutenticado('/usuarios/modalidades', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          modalidadeId: Number(escolhida.value),
          diasSemana,
          duracaoMediaHoras: Number(elModalidadeAddDuracao.value)
        })
      });

      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível adicionar. Tente novamente.');
      }

      const vinculoSalvo = await resposta.json();
      const modalidade = catalogoModalidades.find((m) => m.id === vinculoSalvo.modalidadeId);
      elModalidadeList.appendChild(criarItemModalidade({ ...vinculoSalvo, modalidade }));
      mostrarListaModalidades();
      renderizarOpcoesAdicionar();
    } catch (erro) {
      ronuMostrarErroFormulario(elModalidadesError, erro.message);
    } finally {
      ronuDefinirCarregando(btnAdicionarModalidade, false);
    }
  }

  btnAdicionarModalidade.addEventListener('click', adicionarModalidade);

  async function salvarModalidade(item, botao) {
    const duracaoInput = item.querySelector('.modalidade-duracao');
    if (!duracaoInput.reportValidity()) return;

    const diasSemana = Array.from(item.querySelectorAll('.dia-toggle'))
      .filter((b) => b.getAttribute('aria-pressed') === 'true')
      .map((b) => Number(b.dataset.dia))
      .sort((a, b) => a - b);

    if (diasSemana.length === 0) {
      const nome = item.querySelector('.modalidade-item-nome').textContent;
      ronuMostrarErroFormulario(elModalidadesError, `Selecione pelo menos um dia da semana para ${nome}.`);
      return;
    }

    const modalidadeId = Number(item.dataset.modalidadeId);
    const duracaoMediaHoras = Number(duracaoInput.value);

    botao.disabled = true;
    const textoOriginal = botao.textContent;
    botao.textContent = 'Salvando...';
    ronuOcultarErroFormulario(elModalidadesError);

    try {
      const resposta = await ronuFetchAutenticado('/usuarios/modalidades', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ modalidadeId, diasSemana, duracaoMediaHoras })
      });

      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível salvar. Tente novamente.');
      }

      duracaoInput.dataset.valorSalvo = String(duracaoMediaHoras);
      item.dataset.diasSalvos = JSON.stringify(diasSemana);
      botao.textContent = textoOriginal;
    } catch (erro) {
      botao.disabled = false;
      botao.textContent = textoOriginal;
      ronuMostrarErroFormulario(elModalidadesError, erro.message);
    }
  }

  // Bloqueia a remoção da última modalidade restante: sem isso, o usuário
  // ficaria com o cadastro "incompleto" (ronuChecarCadastroCompleto exige ao
  // menos uma) e cairia de volta no loop do onboarding no próximo login.
  async function removerModalidade(item) {
    const vinculoId = item.dataset.vinculoId;
    item.classList.add('is-removing');
    ronuOcultarErroFormulario(elModalidadesError);

    try {
      const resposta = await ronuFetchAutenticado(`/usuarios/modalidades/${vinculoId}`, { method: 'DELETE' });

      if (!resposta.ok && resposta.status !== 404) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível remover. Tente novamente.');
      }

      item.remove();
      mostrarListaModalidades();
      renderizarOpcoesAdicionar();
    } catch (erro) {
      item.classList.remove('is-removing');
      renderizarAcaoRemoverModalidade(item.querySelector('.modalidade-item-actions'));
      ronuMostrarErroFormulario(elModalidadesError, erro.message);
    }
  }

  elModalidadeList.addEventListener('click', (evento) => {
    const botao = evento.target.closest('button[data-acao]');
    if (!botao) return;

    const item = botao.closest('.modalidade-item');

    if (botao.dataset.acao === 'salvar-modalidade') {
      salvarModalidade(item, botao);
      return;
    }

    if (botao.dataset.acao === 'remover') {
      if (elModalidadeList.children.length <= 1) {
        ronuMostrarErroFormulario(elModalidadesError, 'Você precisa manter pelo menos uma modalidade.');
        return;
      }
      renderizarConfirmacaoRemoverModalidade(item.querySelector('.modalidade-item-actions'));
    } else if (botao.dataset.acao === 'cancelar') {
      renderizarAcaoRemoverModalidade(item.querySelector('.modalidade-item-actions'));
    } else if (botao.dataset.acao === 'confirmar') {
      removerModalidade(item);
    }
  });

  // ---------- Preferências alimentares ----------

  const elPreferenciasLoading = document.getElementById('preferencias-loading');
  const elPreferenciasEmpty = document.getElementById('preferencias-empty');
  const elPreferenciasError = document.getElementById('preferencias-error');
  const elPreferenciaList = document.getElementById('preferencia-list');
  const elAlimentoInput = document.getElementById('config-alimento');
  const btnAdicionarPreferencia = document.getElementById('btn-adicionar-preferencia');

  // Ação de remover fica em dois estados dentro do próprio item (sem modal):
  // "Remover" e, depois de um clique, "Remover? Sim / Cancelar".
  function renderizarAcaoRemover(elAcoes, id) {
    elAcoes.innerHTML = '';
    elAcoes.classList.remove('is-confirming');

    const botao = document.createElement('button');
    botao.type = 'button';
    botao.className = 'preferencia-remove-btn';
    botao.dataset.acao = 'remover';
    botao.textContent = 'Remover';

    elAcoes.appendChild(botao);
  }

  function renderizarConfirmacaoRemover(elAcoes) {
    elAcoes.innerHTML = '';
    elAcoes.classList.add('is-confirming');

    const texto = document.createElement('span');
    texto.className = 'preferencia-confirm-text';
    texto.textContent = 'Remover?';

    const btnConfirmar = document.createElement('button');
    btnConfirmar.type = 'button';
    btnConfirmar.className = 'preferencia-confirm-btn';
    btnConfirmar.dataset.acao = 'confirmar';
    btnConfirmar.textContent = 'Sim';

    const btnCancelar = document.createElement('button');
    btnCancelar.type = 'button';
    btnCancelar.className = 'preferencia-cancel-btn';
    btnCancelar.dataset.acao = 'cancelar';
    btnCancelar.textContent = 'Cancelar';

    elAcoes.append(texto, btnConfirmar, btnCancelar);
  }

  function criarItemPreferencia(preferencia) {
    const item = document.createElement('li');
    item.className = 'preferencia-item';
    item.dataset.tipo = preferencia.tipo;
    item.dataset.id = preferencia.id;

    const info = document.createElement('div');
    info.className = 'preferencia-info';

    const nomeSpan = document.createElement('span');
    nomeSpan.className = 'preferencia-alimento';
    nomeSpan.textContent = preferencia.alimento;

    const tipoSpan = document.createElement('span');
    tipoSpan.className = 'preferencia-tipo';
    tipoSpan.textContent = preferencia.tipo === 'evitar' ? 'Evitar' : 'Prefiro';

    info.append(nomeSpan, tipoSpan);

    const acoes = document.createElement('div');
    acoes.className = 'preferencia-actions';
    renderizarAcaoRemover(acoes, preferencia.id);

    item.append(info, acoes);
    return item;
  }

  function mostrarListaOuVazio() {
    const temItens = elPreferenciaList.children.length > 0;
    elPreferenciaList.hidden = !temItens;
    elPreferenciasEmpty.hidden = temItens;
  }

  async function carregarPreferencias() {
    try {
      const resposta = await ronuFetchAutenticado('/preferencias-alimentares');
      if (!resposta.ok) throw new Error();

      const preferencias = await resposta.json();
      elPreferenciaList.innerHTML = '';
      preferencias.forEach((preferencia) => elPreferenciaList.appendChild(criarItemPreferencia(preferencia)));

      elPreferenciasLoading.hidden = true;
      mostrarListaOuVazio();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        elPreferenciasLoading.textContent = 'Não foi possível carregar suas preferências. Recarregue a página.';
      }
    }
  }

  async function adicionarPreferencia() {
    const alimento = elAlimentoInput.value.trim();
    const tipo = document.querySelector('input[name="config-tipo-preferencia"]:checked').value;

    if (!alimento) {
      elAlimentoInput.reportValidity();
      return;
    }

    ronuOcultarErroFormulario(elPreferenciasError);
    ronuDefinirCarregando(btnAdicionarPreferencia, true, 'Adicionando...');

    try {
      const resposta = await ronuFetchAutenticado('/preferencias-alimentares', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ alimento, tipo })
      });

      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível adicionar. Tente novamente.');
      }

      const preferenciaSalva = await resposta.json();

      // Upsert no back-end: se o alimento já existia, atualiza o item existente
      // em vez de duplicar na lista.
      const itemExistente = elPreferenciaList.querySelector(`[data-id="${preferenciaSalva.id}"]`);
      if (itemExistente) itemExistente.remove();

      elPreferenciaList.appendChild(criarItemPreferencia(preferenciaSalva));
      mostrarListaOuVazio();

      elAlimentoInput.value = '';
      elAlimentoInput.focus();
    } catch (erro) {
      ronuMostrarErroFormulario(elPreferenciasError, erro.message);
    } finally {
      ronuDefinirCarregando(btnAdicionarPreferencia, false);
    }
  }

  btnAdicionarPreferencia.addEventListener('click', adicionarPreferencia);

  elAlimentoInput.addEventListener('keydown', (evento) => {
    if (evento.key === 'Enter') {
      evento.preventDefault();
      adicionarPreferencia();
    }
  });

  async function removerPreferencia(item) {
    const id = item.dataset.id;
    item.classList.add('is-removing');
    ronuOcultarErroFormulario(elPreferenciasError);

    try {
      const resposta = await ronuFetchAutenticado(`/preferencias-alimentares/${id}`, { method: 'DELETE' });

      if (!resposta.ok && resposta.status !== 404) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível remover. Tente novamente.');
      }

      item.remove();
      mostrarListaOuVazio();
    } catch (erro) {
      item.classList.remove('is-removing');
      renderizarAcaoRemover(item.querySelector('.preferencia-actions'), id);
      ronuMostrarErroFormulario(elPreferenciasError, erro.message);
    }
  }

  // Delegação de evento na lista: os itens são recriados a cada carga/adição,
  // então um listener por item vazaria handlers órfãos toda vez que a lista
  // é redesenhada.
  elPreferenciaList.addEventListener('click', (evento) => {
    const botao = evento.target.closest('button[data-acao]');
    if (!botao) return;

    const item = botao.closest('.preferencia-item');
    const acoes = item.querySelector('.preferencia-actions');

    if (botao.dataset.acao === 'remover') {
      renderizarConfirmacaoRemover(acoes);
    } else if (botao.dataset.acao === 'cancelar') {
      renderizarAcaoRemover(acoes, item.dataset.id);
    } else if (botao.dataset.acao === 'confirmar') {
      removerPreferencia(item);
    }
  });

  definirFaixaDataNascimento();
  await Promise.all([carregarPerfil(), carregarModalidades(), carregarPreferencias()]);
});
