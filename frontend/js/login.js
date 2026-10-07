// Ronu — Tela de login

document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('login-form');
  const formError = document.getElementById('form-error');
  const formSuccess = document.getElementById('form-success');
  const submitBtn = document.getElementById('submit-btn');
  const campoEmail = document.getElementById('email');

  const parametros = new URLSearchParams(window.location.search);

  if (parametros.get('sessao') === 'expirada') {
    ronuMostrarErroFormulario(formError, 'Sua sessão expirou. Entre novamente.');
  }

  // Vindo de redefinir-senha.html. O parâmetro sai da URL logo em seguida,
  // para a faixa não voltar ao recarregar a página.
  if (parametros.get('senha') === 'redefinida') {
    ronuMostrarErroFormulario(formSuccess, 'Senha alterada. Entre com a senha nova.');
    parametros.delete('senha');
    const busca = parametros.toString();
    history.replaceState(null, '', window.location.pathname + (busca ? `?${busca}` : '') + window.location.hash);
  }

  // Leva o email já digitado para a página do pedido (sem pôr na URL).
  document.getElementById('link-esqueci-senha').addEventListener('click', () => {
    const email = campoEmail.value.trim();
    if (email) {
      sessionStorage.setItem(RONU_EMAIL_REDEFINICAO_KEY, email);
    } else {
      sessionStorage.removeItem(RONU_EMAIL_REDEFINICAO_KEY);
    }
  });

  form.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }

    ronuOcultarErroFormulario(formError);
    ronuOcultarErroFormulario(formSuccess);

    const email = campoEmail.value.trim();
    const senha = document.getElementById('senha').value;

    ronuDefinirCarregando(submitBtn, true, 'Entrando...');
    const cancelarAvisoDeEspera = ronuAvisoDeEspera(submitBtn.querySelector('.btn-label'));

    try {
      await ronuLogin(email, senha);
      await ronuRedirecionarPosAuth();
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
      ronuDefinirCarregando(submitBtn, false);
    } finally {
      cancelarAvisoDeEspera();
    }
  });
});
