// Ronu — Onboarding (perfil, objetivo, modalidades, preferências)
// Wizard de 4 passos: cada um só avança depois de salvar com sucesso na API.
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
    btnAvancar.querySelector('.btn-label').textContent = passo === 4 ? 'Concluir' : 'Avançar';

    const primeiroCampo = document.querySelector(`.onboarding-step[data-step="${passo}"] input`);
    if (primeiroCampo) primeiroCampo.focus();
  }

  // ---------- Passo 1: perfil ----------

  // Data de nascimento não pode ser hoje/futuro nem implicar uma idade
  // absurda — mesmo espírito das faixas de min/max já usadas no campo peso.
  function definirFaixaDataNascimento() {
    const input = document.getElementById('data-nascimento');
    const hoje = new Date();
    const cemAnosAtras = new Date(hoje.getFullYear() - 100, hoje.getMonth(), hoje.getDate());

    input.max = hoje.toISOString().split('T')[0];
    input.min = cemAnosAtras.toISOString().split('T')[0];
  }

  function validarPassoPerfil() {
    const altura = document.getElementById('altura');
    const campoAltura = document.getElementById('field-altura');
    const alturaErro = document.getElementById('altura-error');
    const sexoSelecionado = document.querySelector('input[name="sexo"]:checked');
    const dataNascimento = document.getElementById('data-nascimento');

    ronuLimparCampoInvalido(campoAltura, alturaErro);

    if (!altura.reportValidity()) {
      return null;
    }

    // Campo é texto (pra aceitar vírgula), então a faixa 1,00–2,50 m não pode
    // ser validada via min/max nativo — checada manualmente em centímetros.
    const alturaCm = ronuParseAlturaCm(altura.value);
    if (alturaCm === null || alturaCm < 100 || alturaCm > 250) {
      ronuMarcarCampoInvalido(campoAltura, alturaErro, 'Informe uma altura entre 1,00 e 2,50 m.');
      return null;
    }

    if (!sexoSelecionado) {
      ronuMostrarErroFormulario(formError, 'Selecione seu sexo.');
      return null;
    }

    if (!dataNascimento.reportValidity()) {
      return null;
    }

    return {
      altura: alturaCm,
      sexo: sexoSelecionado.value,
      dataNascimento: dataNascimento.value
    };
  }

  async function avancarPassoPerfil() {
    const dados = validarPassoPerfil();
    if (!dados) return;

    ronuOcultarErroFormulario(formError);
    ronuDefinirCarregando(btnAvancar, true, 'Salvando...');

    try {
      const resposta = await ronuFetchAutenticado('/perfil', {
        method: 'PUT',
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

  // ---------- Passo 2: objetivo ----------

  function validarPassoObjetivo() {
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

  async function avancarPassoObjetivo() {
    const dados = validarPassoObjetivo();
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

      mostrarPasso(3);
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
    } finally {
      ronuDefinirCarregando(btnAvancar, false);
    }
  }

  // ---------- Passo 3: modalidades ----------

  // 1=Segunda ... 7=Domingo (ISO 8601, mesmo padrão do backend). Abreviação
  // de 3 letras — com 1 letra só, "Segunda/Sexta/Sábado" colidem todas em
  // "S" e "Quarta/Quinta" colidem em "Q", o que ficaria ambíguo em pt-BR.
  const DIAS_SEMANA = [
    { valor: 1, rotulo: 'SEG', nomeCompleto: 'segunda-feira' },
    { valor: 2, rotulo: 'TER', nomeCompleto: 'terça-feira' },
    { valor: 3, rotulo: 'QUA', nomeCompleto: 'quarta-feira' },
    { valor: 4, rotulo: 'QUI', nomeCompleto: 'quinta-feira' },
    { valor: 5, rotulo: 'SEX', nomeCompleto: 'sexta-feira' },
    { valor: 6, rotulo: 'SÁB', nomeCompleto: 'sábado' },
    { valor: 7, rotulo: 'DOM', nomeCompleto: 'domingo' }
  ];

  const MINUTOS_PERMITIDOS = [0, 15, 30, 45];

  // A API só aceita um decimal (DuracaoMediaHoras) — a UI coleta horas e
  // minutos separados (minutos travado em múltiplos de 15, sem digitação
  // livre) e converte só na hora de montar o payload. Sem inverso aqui: o
  // onboarding nunca pré-carrega um valor já salvo (isso só acontece na
  // edição em configuracoes.js).
  function horasMinutosParaDecimal(horas, minutos) {
    return horas + minutos / 60;
  }

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

    const detalhes = document.createElement('div');
    detalhes.className = 'modalidade-details';

    const diasGroup = document.createElement('div');
    diasGroup.className = 'modalidade-dias';
    diasGroup.setAttribute('role', 'group');
    diasGroup.setAttribute('aria-label', `Dias da semana de treino de ${modalidade.nome}`);

    const botoesDia = DIAS_SEMANA.map(({ valor, rotulo, nomeCompleto }) => {
      const botao = document.createElement('button');
      botao.type = 'button';
      botao.className = 'dia-toggle';
      botao.dataset.dia = valor;
      botao.setAttribute('aria-pressed', 'false');
      botao.setAttribute('aria-label', nomeCompleto);
      botao.disabled = true;
      botao.textContent = rotulo;

      botao.addEventListener('click', () => {
        const pressionado = botao.getAttribute('aria-pressed') === 'true';
        botao.setAttribute('aria-pressed', String(!pressionado));
      });

      diasGroup.appendChild(botao);
      return botao;
    });

    const duracaoField = document.createElement('div');
    duracaoField.className = 'modalidade-duracao-field';
    duracaoField.setAttribute('role', 'group');
    duracaoField.setAttribute('aria-label', `Duração média por sessão de ${modalidade.nome}`);

    const duracaoTitulo = document.createElement('span');
    duracaoTitulo.className = 'modalidade-duracao-titulo';
    duracaoTitulo.textContent = 'Duração média por sessão';

    const duracaoGrupo = document.createElement('div');
    duracaoGrupo.className = 'modalidade-duracao-grupo';

    const labelHoras = document.createElement('label');
    labelHoras.className = 'modalidade-duracao-unidade';
    const spanHoras = document.createElement('span');
    spanHoras.textContent = 'Horas';
    const horasInput = document.createElement('input');
    horasInput.type = 'number';
    horasInput.className = 'modalidade-duracao-horas num';
    horasInput.min = '0';
    horasInput.max = '5';
    horasInput.step = '1';
    horasInput.value = '0';
    horasInput.disabled = true;
    labelHoras.append(spanHoras, horasInput);

    const labelMinutos = document.createElement('label');
    labelMinutos.className = 'modalidade-duracao-unidade';
    const spanMinutos = document.createElement('span');
    spanMinutos.textContent = 'Min';
    const minutosSelect = document.createElement('select');
    minutosSelect.className = 'modalidade-duracao-minutos num';
    MINUTOS_PERMITIDOS.forEach((valor) => {
      const opcao = document.createElement('option');
      opcao.value = String(valor);
      opcao.textContent = String(valor);
      minutosSelect.appendChild(opcao);
    });
    minutosSelect.disabled = true;
    labelMinutos.append(spanMinutos, minutosSelect);

    duracaoGrupo.append(labelHoras, labelMinutos);
    duracaoField.append(duracaoTitulo, duracaoGrupo);
    detalhes.append(diasGroup, duracaoField);

    const colapsavel = document.createElement('div');
    colapsavel.className = 'modalidade-collapse';
    colapsavel.appendChild(detalhes);

    checkbox.addEventListener('change', () => {
      botoesDia.forEach((botao) => { botao.disabled = !checkbox.checked; });
      horasInput.disabled = !checkbox.checked;
      minutosSelect.disabled = !checkbox.checked;
      colapsavel.classList.toggle('is-aberto', checkbox.checked);

      if (checkbox.checked) {
        botoesDia[0].focus();
      } else {
        // Desmarcar a modalidade limpa a seleção de dias e a duração, pra
        // não reenviar dias escolhidos antes de desmarcar sem querer.
        botoesDia.forEach((botao) => botao.setAttribute('aria-pressed', 'false'));
        horasInput.value = '0';
        minutosSelect.value = '0';
      }
    });

    linha.append(label, colapsavel);
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

  async function avancarPassoModalidades() {
    const linhas = Array.from(document.querySelectorAll('.modalidade-row'));
    const selecionadas = linhas
      .map((linha) => ({
        checkbox: linha.querySelector('input[type="checkbox"]'),
        nome: linha.querySelector('.modalidade-check span').textContent,
        botoesDia: Array.from(linha.querySelectorAll('.dia-toggle')),
        horasInput: linha.querySelector('.modalidade-duracao-horas'),
        minutosSelect: linha.querySelector('.modalidade-duracao-minutos')
      }))
      .filter((linha) => linha.checkbox.checked);

    if (selecionadas.length === 0) {
      ronuMostrarErroFormulario(formError, 'Selecione pelo menos uma modalidade.');
      return;
    }

    for (const linha of selecionadas) {
      const temDiaSelecionado = linha.botoesDia.some((botao) => botao.getAttribute('aria-pressed') === 'true');

      if (!temDiaSelecionado) {
        ronuMostrarErroFormulario(formError, `Selecione pelo menos um dia da semana para ${linha.nome}.`);
        return;
      }

      if (!linha.horasInput.reportValidity()) {
        return;
      }

      // Horas e minutos são válidos isoladamente (0-5h, 0/15/30/45min), mas a
      // SOMA pode ficar fora da faixa que a API aceita (ex: 0h+0min=0, abaixo
      // do mínimo; 5h+45min=5.75, acima do máximo) — por isso valida o total.
      const duracaoTotal = horasMinutosParaDecimal(Number(linha.horasInput.value || 0), Number(linha.minutosSelect.value));
      if (duracaoTotal < 0.25 || duracaoTotal > 5) {
        ronuMostrarErroFormulario(formError, `Informe uma duração entre 15 minutos e 5 horas para ${linha.nome}.`);
        return;
      }
    }

    ronuOcultarErroFormulario(formError);
    ronuDefinirCarregando(btnAvancar, true, 'Salvando...');

    try {
      await Promise.all(selecionadas.map(async (linha) => {
        const diasSemana = linha.botoesDia
          .filter((botao) => botao.getAttribute('aria-pressed') === 'true')
          .map((botao) => Number(botao.dataset.dia));

        const duracaoMediaHoras = horasMinutosParaDecimal(Number(linha.horasInput.value || 0), Number(linha.minutosSelect.value));

        const resposta = await ronuFetchAutenticado('/usuarios/modalidades', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            modalidadeId: Number(linha.checkbox.dataset.modalidadeId),
            diasSemana,
            duracaoMediaHoras
          })
        });

        if (!resposta.ok) {
          const corpo = await resposta.json().catch(() => null);
          throw new Error(corpo?.mensagem || 'Não foi possível salvar uma das modalidades.');
        }
      }));

      mostrarPasso(4);
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
    } finally {
      ronuDefinirCarregando(btnAvancar, false);
    }
  }

  // ---------- Passo 4: preferências alimentares ----------

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
      avancarPassoPerfil();
    } else if (passoAtual === 2) {
      avancarPassoObjetivo();
    } else if (passoAtual === 3) {
      avancarPassoModalidades();
    } else {
      window.location.href = 'dashboard.html';
    }
  });

  definirFaixaDataNascimento();
  await carregarCatalogoModalidades();
  mostrarPasso(1);
});
