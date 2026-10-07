// Ronu — Esqueci minha senha (pedido do link por email)
// A API responde igual exista ou não a conta, então a tela também: depois do
// pedido, sempre "Confira seu email", sem dizer se a conta existe.

const RONU_ESPERA_REENVIO_S = 60;
const RONU_MSG_PEDIDO_FALHOU = 'Não foi possível enviar agora. Tente de novo em instantes.';

document.addEventListener('DOMContentLoaded', () => {
  const secoes = {
    formulario: document.getElementById('estado-formulario'),
    enviado: document.getElementById('estado-enviado')
  };
  const form = document.getElementById('pedido-form');
  const campoEmail = document.getElementById('email');
  const formError = document.getElementById('form-error');
  const submitBtn = document.getElementById('submit-btn');
  const emailEnviado = document.getElementById('email-enviado');
  const reenvioError = document.getElementById('reenvio-error');
  const reenviarBtn = document.getElementById('reenviar-btn');
  const reenvioStatus = document.getElementById('reenvio-status');

  let emailPedido = '';
  let temporizadorReenvio = null;

  // Email que a pessoa já tinha digitado no login (ver login.js).
  const emailDoLogin = sessionStorage.getItem(RONU_EMAIL_REDEFINICAO_KEY);
  if (emailDoLogin) {
    campoEmail.value = emailDoLogin;
    sessionStorage.removeItem(RONU_EMAIL_REDEFINICAO_KEY);
  }

  function mensagemDoErro(erro) {
    return erro.temporario || !erro.message ? RONU_MSG_PEDIDO_FALHOU : erro.message;
  }

  // "Reenviar" fica bloqueado por 60 s depois de cada pedido. A contagem é só
  // da tela: o limite de verdade (3 por hora por conta) fica na API, que
  // responde igual mesmo acima dele.
  function iniciarContagemReenvio() {
    clearInterval(temporizadorReenvio);
    const label = reenviarBtn.querySelector('.btn-label');
    const fim = Date.now() + RONU_ESPERA_REENVIO_S * 1000;

    const atualizar = () => {
      const restante = Math.ceil((fim - Date.now()) / 1000);
      if (restante <= 0) {
        clearInterval(temporizadorReenvio);
        reenviarBtn.disabled = false;
        label.textContent = 'Reenviar';
        return;
      }
      reenviarBtn.disabled = true;
      const minutos = Math.floor(restante / 60);
      const segundos = String(restante % 60).padStart(2, '0');
      label.textContent = `Reenviar em ${minutos}:${segundos}`;
    };

    atualizar();
    temporizadorReenvio = setInterval(atualizar, 250);
  }

  form.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }

    ronuOcultarErroFormulario(formError);
    const email = campoEmail.value.trim();

    ronuDefinirCarregando(submitBtn, true, 'Enviando...');
    const cancelarAvisoDeEspera = ronuAvisoDeEspera(submitBtn.querySelector('.btn-label'));

    try {
      await ronuPedirLinkRedefinicao(email);
      emailPedido = email;
      emailEnviado.textContent = email;
      ronuOcultarErroFormulario(reenvioError);
      reenvioStatus.textContent = '';
      ronuMostrarEstado(secoes, 'enviado');
      iniciarContagemReenvio();
    } catch (erro) {
      ronuMostrarErroFormulario(formError, mensagemDoErro(erro));
    } finally {
      cancelarAvisoDeEspera();
      ronuDefinirCarregando(submitBtn, false);
    }
  });

  reenviarBtn.addEventListener('click', async () => {
    ronuOcultarErroFormulario(reenvioError);
    reenvioStatus.textContent = '';

    ronuDefinirCarregando(reenviarBtn, true, 'Reenviando...');
    const cancelarAvisoDeEspera = ronuAvisoDeEspera(reenviarBtn.querySelector('.btn-label'));

    try {
      await ronuPedirLinkRedefinicao(emailPedido);
      const hora = new Date().toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
      reenvioStatus.textContent = `Reenviado às ${hora}. Use o link do email mais recente.`;
      cancelarAvisoDeEspera();
      ronuDefinirCarregando(reenviarBtn, false);
      iniciarContagemReenvio();
    } catch (erro) {
      cancelarAvisoDeEspera();
      ronuDefinirCarregando(reenviarBtn, false);
      ronuMostrarErroFormulario(reenvioError, mensagemDoErro(erro));
      // Muitas tentativas: nada de reenviar logo em seguida.
      if (erro.status === 429) iniciarContagemReenvio();
    }
  });

  document.getElementById('outro-email-btn').addEventListener('click', () => {
    clearInterval(temporizadorReenvio);
    ronuOcultarErroFormulario(formError);
    ronuMostrarEstado(secoes, 'formulario');
    campoEmail.focus();
    campoEmail.select();
  });
});
