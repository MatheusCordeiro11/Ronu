// Ronu — Alimentação (preferências alimentares)
// Listar/adicionar/remover alimentos preferidos ou a evitar. Uma das três
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

  await carregarPreferencias();
});
