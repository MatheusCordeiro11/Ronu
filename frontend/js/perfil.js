// Ronu — Perfil (dados pessoais)
// Editar altura, sexo e data de nascimento (GET/PUT /perfil). Uma das três
// páginas de configurações da conta, ao lado de treino.html e alimentacao.html.

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

  // Rotina diária e orçamento semanal são editados em alimentacao.html, mas
  // o PUT /perfil recebe tudo junto — reenviados no PUT, senão salvar só os
  // dados físicos aqui apagaria os dois (iriam como null). Vêm de um GET
  // feito na hora de salvar, não do carregado quando a página abriu: com
  // alimentacao.html aberta em outra aba, a rotina/orçamento salvos lá
  // depois seriam sobrescritos pelos valores antigos. A janela cai para o
  // intervalo entre o GET e o PUT (menos de 1 s); a solução definitiva
  // (endpoints separados por seção) está em docs/lacunas-conhecidas.md.
  // Se o GET falhar, o salvamento é cancelado — sem cair nos dados antigos.
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
      const atual = await buscarPerfilAtual();
      const resposta = await ronuFetchAutenticado('/perfil', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          altura: alturaCm,
          sexo: sexoSelecionado.value,
          dataNascimento: elDataNascimento.value,
          rotinaDiaria: atual.rotinaDiaria ?? null,
          orcamentoSemanal: atual.orcamentoSemanal ?? null
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

  definirFaixaDataNascimento();
  await carregarPerfil();
});
