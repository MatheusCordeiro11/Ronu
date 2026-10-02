// Ronu — Alimentação (preferências alimentares + rotina diária)
// Listar/adicionar/remover alimentos preferidos ou a evitar, e editar a
// rotina diária usada para estimar os horários das refeições. Uma das três
// páginas de configurações da conta, ao lado de perfil.html e treino.html.

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

  // ---------- Perfil salvo (compartilhado por Rotina e Orçamento) ----------
  // Rotina e orçamento não têm endpoint próprio: vivem no perfil, e o PUT
  // /perfil grava todos os campos juntos (altura, sexo e data de nascimento
  // são required no PerfilRequest). Cada seção salva reenviando o perfil com
  // só o próprio campo trocado — assim salvar uma seção não apaga (nem leva
  // junto, sem salvar) o campo da outra.
  // perfilSalvo (do GET ao abrir a página) só preenche a tela e decide se
  // ela abre travada. Para salvar, o perfil é buscado de novo na hora (ver
  // salvarNoPerfil).

  let perfilSalvo = null;

  function perfilIncompleto() {
    return perfilSalvo.altura == null || perfilSalvo.sexo == null || perfilSalvo.dataNascimento == null;
  }

  // Sem os dados do perfil o PUT sempre falharia — em vez de deixar o
  // usuário descobrir isso só ao salvar, a seção já abre travada.
  function mostrarPerfilIncompleto(elErro, complemento) {
    elErro.textContent = 'Complete seus dados pessoais em ';
    const link = document.createElement('a');
    link.href = 'perfil.html';
    link.textContent = 'Perfil';
    elErro.append(link, ` antes de salvar ${complemento}.`);
    elErro.hidden = false;
  }

  // Perfil gravado AGORA, logo antes de salvar — não o carregado quando a
  // página abriu. Com duas abas abertas (esta e perfil.html, por exemplo), a
  // que salvasse por último mandaria de volta os dados antigos e apagaria o
  // que a outra salvou. Buscando na hora, a janela cai de minutos/horas para
  // o intervalo entre este GET e o PUT (menos de 1 s); a solução definitiva
  // (endpoints separados por seção) está em docs/lacunas-conhecidas.md.
  // Se o GET falhar, o salvamento é cancelado: cair de volta no perfilSalvo
  // traria o problema de volta justamente quando a rede está instável.
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

    // O PUT devolve o perfil como ficou gravado — fonte mais confiável que o
    // corpo enviado.
    perfilSalvo = await resposta.json();
  }

  // ---------- Rotina diária ----------

  const ROTINA_LIMITE = 500;

  const elRotinaLoading = document.getElementById('rotina-loading');
  const elRotinaError = document.getElementById('rotina-error');
  const elRotinaSuccess = document.getElementById('rotina-success');
  const elRotinaForm = document.getElementById('rotina-form');
  const elRotinaTexto = document.getElementById('rotina-texto');
  const elRotinaContador = document.getElementById('rotina-contador');
  const btnSalvarRotina = document.getElementById('btn-salvar-rotina');

  function atualizarContadorRotina() {
    const tamanho = elRotinaTexto.value.length;
    elRotinaContador.textContent = `${tamanho}/${ROTINA_LIMITE}`;
    elRotinaContador.classList.toggle('is-excedido', tamanho > ROTINA_LIMITE);
  }

  function bloquearRotina() {
    elRotinaTexto.disabled = true;
    btnSalvarRotina.disabled = true;
  }

  function preencherRotina() {
    elRotinaTexto.value = perfilSalvo.rotinaDiaria ?? '';
    atualizarContadorRotina();

    elRotinaLoading.hidden = true;
    elRotinaForm.hidden = false;

    if (perfilIncompleto()) {
      mostrarPerfilIncompleto(elRotinaError, 'sua rotina');
      bloquearRotina();
    }
  }

  function mostrarFalhaRotina() {
    elRotinaLoading.hidden = true;
    ronuMostrarErroFormulario(elRotinaError, 'Não foi possível carregar sua rotina. Recarregue a página.');
    elRotinaForm.hidden = false;
    bloquearRotina();
  }

  async function salvarRotina(evento) {
    evento.preventDefault();

    ronuOcultarErroFormulario(elRotinaError);
    elRotinaSuccess.hidden = true;

    // maxlength="500" já impede passar do limite no navegador; esta checagem
    // é só a rede de segurança caso o atributo seja contornado.
    if (elRotinaTexto.value.length > ROTINA_LIMITE) {
      ronuMostrarErroFormulario(elRotinaError, `Sua rotina pode ter no máximo ${ROTINA_LIMITE} caracteres.`);
      return;
    }

    ronuDefinirCarregando(btnSalvarRotina, true, 'Salvando...');

    // Campo vazio (ou só com espaços) vira null, não "" — "sem rotina"
    // fica com um único valor no banco.
    const rotinaDiaria = elRotinaTexto.value.trim() || null;

    try {
      await salvarNoPerfil({ rotinaDiaria });

      elRotinaSuccess.textContent = 'Rotina salva.';
      elRotinaSuccess.hidden = false;
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        ronuMostrarErroFormulario(elRotinaError, erro.message);
      }
    } finally {
      ronuDefinirCarregando(btnSalvarRotina, false);
    }
  }

  elRotinaTexto.addEventListener('input', atualizarContadorRotina);
  elRotinaForm.addEventListener('submit', salvarRotina);

  // ---------- Orçamento semanal ----------
  // Valores fixos ("economico", "moderado", "sem_restricao"), usados como
  // estão pela regra 12 do prompt. Opcional: sem escolha salva, nenhuma pill
  // vem marcada (e não há "limpar" — "moderado" e "não informado" têm o
  // mesmo efeito na dieta).

  const elOrcamentoLoading = document.getElementById('orcamento-loading');
  const elOrcamentoError = document.getElementById('orcamento-error');
  const elOrcamentoSuccess = document.getElementById('orcamento-success');
  const elOrcamentoForm = document.getElementById('orcamento-form');
  const btnSalvarOrcamento = document.getElementById('btn-salvar-orcamento');
  const radiosOrcamento = Array.from(document.querySelectorAll('input[name="orcamento-semanal"]'));

  function bloquearOrcamento() {
    radiosOrcamento.forEach((radio) => { radio.disabled = true; });
    btnSalvarOrcamento.disabled = true;
  }

  function preencherOrcamento() {
    const radioSalvo = radiosOrcamento.find((radio) => radio.value === perfilSalvo.orcamentoSemanal);
    if (radioSalvo) radioSalvo.checked = true;

    elOrcamentoLoading.hidden = true;
    elOrcamentoForm.hidden = false;

    if (perfilIncompleto()) {
      mostrarPerfilIncompleto(elOrcamentoError, 'seu orçamento');
      bloquearOrcamento();
    }
  }

  function mostrarFalhaOrcamento() {
    elOrcamentoLoading.hidden = true;
    ronuMostrarErroFormulario(elOrcamentoError, 'Não foi possível carregar seu orçamento. Recarregue a página.');
    elOrcamentoForm.hidden = false;
    bloquearOrcamento();
  }

  async function salvarOrcamento(evento) {
    evento.preventDefault();

    ronuOcultarErroFormulario(elOrcamentoError);
    elOrcamentoSuccess.hidden = true;

    const selecionado = radiosOrcamento.find((radio) => radio.checked);
    if (!selecionado) {
      ronuMostrarErroFormulario(elOrcamentoError, 'Selecione uma opção.');
      return;
    }

    ronuDefinirCarregando(btnSalvarOrcamento, true, 'Salvando...');

    try {
      await salvarNoPerfil({ orcamentoSemanal: selecionado.value });

      elOrcamentoSuccess.textContent = 'Orçamento salvo.';
      elOrcamentoSuccess.hidden = false;
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        ronuMostrarErroFormulario(elOrcamentoError, erro.message);
      }
    } finally {
      ronuDefinirCarregando(btnSalvarOrcamento, false);
    }
  }

  elOrcamentoForm.addEventListener('submit', salvarOrcamento);

  // ---------- Carga do perfil ----------

  async function carregarPerfilSalvo() {
    try {
      const resposta = await ronuFetchAutenticado('/perfil');
      if (!resposta.ok) throw new Error();

      perfilSalvo = await resposta.json();
      preencherRotina();
      preencherOrcamento();
    } catch (erro) {
      if (erro.message !== 'Sessão expirada.') {
        mostrarFalhaRotina();
        mostrarFalhaOrcamento();
      }
    }
  }

  // Seções independentes: uma falha numa não impede a outra de carregar.
  await Promise.all([carregarPreferencias(), carregarPerfilSalvo()]);
});
