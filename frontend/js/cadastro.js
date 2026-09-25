// Ronu — Tela de cadastro
// Após criar a conta, faz login automaticamente com as mesmas credenciais
// (POST /api/auth/cadastro não devolve token; quem devolve é POST /api/auth/login).

document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('cadastro-form');
  const formError = document.getElementById('form-error');
  const submitBtn = document.getElementById('submit-btn');
  const campoConfirmarSenha = document.getElementById('field-confirmar-senha');
  const confirmarSenhaErro = document.getElementById('confirmar-senha-error');

  // ---------- Revelação progressiva dos campos ----------
  // Mesmo padrão do accordeon de modalidades do onboarding: cada etapa fica
  // em um .modalidade-collapse fechado, com os controles desabilitados (fora
  // do Tab e do submit), e abre com .is-aberto quando o campo anterior deixa
  // de estar vazio. Nunca fecha de novo — apagar um campo já revelado não
  // esconde o que o usuário já digitou nos seguintes; o checkValidity() do
  // submit é quem barra campo vazio nesse caso.
  const etapas = Array.from(form.querySelectorAll('[data-etapa-cadastro]'));
  const camposEmOrdem = ['nome', 'email', 'senha', 'confirmar-senha']
    .map((id) => document.getElementById(id));

  function revelarEtapa(etapa) {
    if (!etapa || etapa.classList.contains('is-aberto')) return;
    etapa.querySelectorAll('input, button').forEach((controle) => { controle.disabled = false; });
    etapa.classList.add('is-aberto');
  }

  camposEmOrdem.forEach((campo, indice) => {
    campo.addEventListener('input', () => {
      if (campo.value.trim() !== '') {
        revelarEtapa(etapas[indice]);
      }
    });

    // Enter num campo que não é o último leva pro próximo em vez de tentar
    // enviar o formulário com etapas ainda fechadas.
    campo.addEventListener('keydown', (evento) => {
      const proximo = camposEmOrdem[indice + 1];
      if (evento.key !== 'Enter' || !proximo) return;

      evento.preventDefault();
      if (campo.value.trim() === '') {
        campo.reportValidity();
        return;
      }
      revelarEtapa(etapas[indice]);
      proximo.focus();
    });
  });

  form.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }

    ronuOcultarErroFormulario(formError);
    ronuLimparCampoInvalido(campoConfirmarSenha, confirmarSenhaErro);

    const nome = document.getElementById('nome').value.trim();
    const email = document.getElementById('email').value.trim();
    const senha = document.getElementById('senha').value;
    const confirmarSenha = document.getElementById('confirmar-senha').value;

    if (confirmarSenha !== senha) {
      ronuMarcarCampoInvalido(campoConfirmarSenha, confirmarSenhaErro, 'As senhas não coincidem.');
      return;
    }

    ronuDefinirCarregando(submitBtn, true, 'Criando conta...');

    try {
      await ronuCadastrar(nome, email, senha);
      await ronuLogin(email, senha);
      await ronuRedirecionarPosAuth();
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
      ronuDefinirCarregando(submitBtn, false);
    }
  });
});
