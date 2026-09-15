// Ronu — Onboarding (objetivo, modalidades, preferências)
// Wizard de 3 passos: cada um só avança depois de salvar com sucesso na API.
// Voltar não re-busca dados da API — os campos ficam no DOM (só ocultos),
// então o que foi digitado nesta sessão permanece ao ir e voltar de passo.

document.addEventListener('DOMContentLoaded', async () => {
  if (!ronuUsuarioLogado()) {
    window.location.href = 'login.html';
    return;
  }

  const formError = document.getElementById('form-error');
  const btnVoltar = document.getElementById('btn-voltar');
  const btnAvancar = document.getElementById('btn-avancar');
  const modalidadeListEl = document.getElementById('modalidade-list');
  const alimentoInput = document.getElementById('alimento');
  const btnAdicionarPreferencia = document.getElementById('adicionar-preferencia-btn');

  let passoAtual = 1;

  // Se o cadastro já estiver completo (ex: usuário voltou a esta URL à toa),
  // não faz sentido forçar o wizard de novo.
  try {
    const completo = await ronuChecarCadastroCompleto();
    if (completo) {
      window.location.href = 'dashboard.html';
      return;
    }
  } catch (erro) {
    // 401 já foi tratado dentro de ronuChecarCadastroCompleto/ronuFetchAutenticado
    // (redireciona pro login). Qualquer outro erro não deve travar o onboarding.
  }

  function mostrarPasso(passo) {
    passoAtual = passo;

    document.querySelectorAll('.onboarding-step').forEach((secao) => {
      secao.hidden = Number(secao.dataset.step) !== passo;
    });

    document.querySelectorAll('.stepper-item').forEach((item) => {
      const indice = Number(item.dataset.step);
      item.classList.toggle('is-current', indice === passo);
      item.classList.toggle('is-done', indice < passo);
    });

    ronuOcultarErroFormulario(formError);
    btnVoltar.hidden = passo === 1;
    btnAvancar.querySelector('.btn-label').textContent = passo === 3 ? 'Concluir' : 'Avançar';

    const primeiroCampo = document.querySelector(`.onboarding-step[data-step="${passo}"] input`);
    if (primeiroCampo) primeiroCampo.focus();
  }

  // ---------- Passo 1: objetivo ----------

  function validarPasso1() {
    const peso = document.getElementById('peso');
    const objetivoSelecionado = document.querySelector('input[name="objetivo"]:checked');

    if (!peso.reportValidity()) {
      return null;
    }

    if (!objetivoSelecionado) {
      ronuMostrarErroFormulario(formError, 'Selecione um objetivo.');
      return null;
    }

    return { peso: parseFloat(peso.value), objetivo: objetivoSelecionado.value };
  }

  async function avancarPasso1() {
    const dados = validarPasso1();
    if (!dados) return;

    ronuOcultarErroFormulario(formError);
    ronuDefinirCarregando(btnAvancar, true, 'Salvando...');

    try {
      const resposta = await ronuFetchAutenticado('/objetivos', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(dados)
      });

      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível salvar. Tente novamente.');
      }

      mostrarPasso(2);
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
    } finally {
      ronuDefinirCarregando(btnAvancar, false);
    }
  }

  // ---------- Passo 2: modalidades ----------

  function criarLinhaModalidade(modalidade) {
    const linha = document.createElement('div');
    linha.className = 'modalidade-row';

    const label = document.createElement('label');
    label.className = 'modalidade-check';

    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.dataset.modalidadeId = modalidade.id;

    const nomeSpan = document.createElement('span');
    nomeSpan.textContent = modalidade.nome;

    label.append(checkbox, nomeSpan);

    const frequenciaInput = document.createElement('input');
    frequenciaInput.type = 'number';
    frequenciaInput.className = 'modalidade-frequencia';
    frequenciaInput.min = '1';
    frequenciaInput.max = '7';
    frequenciaInput.placeholder = 'x/semana';
    frequenciaInput.disabled = true;
    frequenciaInput.setAttribute('aria-label', `Frequência semanal de ${modalidade.nome}`);

    checkbox.addEventListener('change', () => {
      frequenciaInput.disabled = !checkbox.checked;
      frequenciaInput.required = checkbox.checked;
      if (checkbox.checked) {
        frequenciaInput.focus();
      }
    });

    linha.append(label, frequenciaInput);
    return linha;
  }

  async function carregarCatalogoModalidades() {
    try {
      const resposta = await fetch(`${RONU_CONFIG.API_BASE}/modalidades`);
      if (!resposta.ok) throw new Error();

      const modalidades = await resposta.json();
      modalidadeListEl.innerHTML = '';

      if (modalidades.length === 0) {
        modalidadeListEl.innerHTML = '<p class="modalidade-catalog-message">Nenhuma modalidade disponível no momento.</p>';
        return;
      }

      modalidades.forEach((modalidade) => {
        modalidadeListEl.appendChild(criarLinhaModalidade(modalidade));
      });
    } catch (erro) {
      modalidadeListEl.innerHTML = '<p class="modalidade-catalog-message">Não foi possível carregar as modalidades. Recarregue a página.</p>';
    }
  }

  async function avancarPasso2() {
    const linhas = Array.from(document.querySelectorAll('.modalidade-row'));
    const selecionadas = linhas
      .map((linha) => ({
        checkbox: linha.querySelector('input[type="checkbox"]'),
        frequenciaInput: linha.querySelector('.modalidade-frequencia')
      }))
      .filter((linha) => linha.checkbox.checked);

    if (selecionadas.length === 0) {
      ronuMostrarErroFormulario(formError, 'Selecione pelo menos uma modalidade.');
      return;
    }

    for (const linha of selecionadas) {
      if (!linha.frequenciaInput.reportValidity()) {
        return;
      }
    }

    ronuOcultarErroFormulario(formError);
    ronuDefinirCarregando(btnAvancar, true, 'Salvando...');

    try {
      await Promise.all(selecionadas.map(async (linha) => {
        const resposta = await ronuFetchAutenticado('/usuarios/modalidades', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            modalidadeId: Number(linha.checkbox.dataset.modalidadeId),
            frequenciaSemanal: Number(linha.frequenciaInput.value)
          })
        });

        if (!resposta.ok) {
          const corpo = await resposta.json().catch(() => null);
          throw new Error(corpo?.mensagem || 'Não foi possível salvar uma das modalidades.');
        }
      }));

      mostrarPasso(3);
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
    } finally {
      ronuDefinirCarregando(btnAvancar, false);
    }
  }

  // ---------- Passo 3: preferências alimentares ----------

  function adicionarItemNaLista(preferencia) {
    const lista = document.getElementById('preferencia-list');
    const item = document.createElement('li');
    item.className = 'preferencia-item';
    item.dataset.tipo = preferencia.tipo;

    const nomeSpan = document.createElement('span');
    nomeSpan.className = 'preferencia-alimento';
    nomeSpan.textContent = preferencia.alimento;

    const tipoSpan = document.createElement('span');
    tipoSpan.className = 'preferencia-tipo';
    tipoSpan.textContent = preferencia.tipo === 'evitar' ? 'Evitar' : 'Prefiro';

    item.append(nomeSpan, tipoSpan);
    lista.appendChild(item);
  }

  async function adicionarPreferencia() {
    const alimento = alimentoInput.value.trim();
    const tipo = document.querySelector('input[name="tipo-preferencia"]:checked').value;

    if (!alimento) {
      alimentoInput.reportValidity();
      return;
    }

    ronuOcultarErroFormulario(formError);
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
      adicionarItemNaLista(preferenciaSalva);
      alimentoInput.value = '';
      alimentoInput.focus();
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
    } finally {
      ronuDefinirCarregando(btnAdicionarPreferencia, false);
    }
  }

  btnAdicionarPreferencia.addEventListener('click', adicionarPreferencia);

  alimentoInput.addEventListener('keydown', (evento) => {
    if (evento.key === 'Enter') {
      evento.preventDefault();
      adicionarPreferencia();
    }
  });

  // ---------- Navegação geral ----------

  btnVoltar.addEventListener('click', () => {
    if (passoAtual > 1) {
      mostrarPasso(passoAtual - 1);
    }
  });

  btnAvancar.addEventListener('click', () => {
    if (passoAtual === 1) {
      avancarPasso1();
    } else if (passoAtual === 2) {
      avancarPasso2();
    } else {
      window.location.href = 'dashboard.html';
    }
  });

  await carregarCatalogoModalidades();
  mostrarPasso(1);
});
