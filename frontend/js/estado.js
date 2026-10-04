// Ronu — Tela "Só mais um passo" (estado.html)
// Aparece depois do login quando a conta ainda não tem estado (UF). Só é
// mostrada se ronuLogin/ronuLoginComGoogle marcaram RONU_PRECISA_ESTADO_KEY;
// em qualquer outro caso segue o fluxo normal de pós-login.

document.addEventListener('DOMContentLoaded', () => {
  if (!ronuUsuarioLogado()) {
    window.location.href = 'login.html';
    return;
  }

  if (localStorage.getItem(RONU_PRECISA_ESTADO_KEY) !== 'true') {
    // O formulário desta tela fica oculto nesse caminho, então um erro da
    // checagem vai para o dashboard, que tem a tela de erro com "Tentar
    // novamente" (o 401 já redirecionou pro login sozinho).
    ronuRedirecionarPosAuth().catch((erro) => {
      if (erro.message !== 'Sessão expirada.') {
        window.location.href = 'dashboard.html';
      }
    });
    return;
  }

  const conteudo = document.getElementById('estado-conteudo');
  const form = document.getElementById('estado-form');
  const formError = document.getElementById('form-error');
  const submitBtn = document.getElementById('submit-btn');
  const selectEstado = document.getElementById('estado');
  const btnSair = document.getElementById('sair-btn');

  conteudo.hidden = false;

  btnSair.addEventListener('click', () => {
    ronuLimparSessao();
    window.location.href = 'login.html';
  });

  form.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    if (!form.checkValidity()) {
      form.reportValidity();
      return;
    }

    ronuOcultarErroFormulario(formError);
    ronuDefinirCarregando(submitBtn, true, 'Salvando...');

    try {
      await ronuSalvarEstado(selectEstado.value);
      await ronuRedirecionarPosAuth();
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
      ronuDefinirCarregando(submitBtn, false);
    }
  });
});
