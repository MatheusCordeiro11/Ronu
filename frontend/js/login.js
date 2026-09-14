// Ronu — Tela de login

document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('login-form');
  const formError = document.getElementById('form-error');
  const submitBtn = document.getElementById('submit-btn');

  form.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }

    ronuOcultarErroFormulario(formError);

    const email = document.getElementById('email').value.trim();
    const senha = document.getElementById('senha').value;

    ronuDefinirCarregando(submitBtn, true, 'Entrando...');

    try {
      await ronuLogin(email, senha);
      window.location.href = 'dashboard.html';
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
      ronuDefinirCarregando(submitBtn, false);
    }
  });
});
