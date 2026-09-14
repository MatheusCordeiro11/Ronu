// Ronu — Tela de cadastro
// Após criar a conta, faz login automaticamente com as mesmas credenciais
// (POST /api/auth/cadastro não devolve token; quem devolve é POST /api/auth/login).

document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('cadastro-form');
  const formError = document.getElementById('form-error');
  const submitBtn = document.getElementById('submit-btn');
  const campoConfirmarSenha = document.getElementById('field-confirmar-senha');
  const confirmarSenhaErro = document.getElementById('confirmar-senha-error');

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
