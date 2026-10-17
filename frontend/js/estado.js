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
  const btnSair = document.getElementById('sair-btn');
  const previa = document.getElementById('estado-previa');

  conteudo.hidden = false;

  // Prévia do que muda com o estado. Espelha as regras do backend: a regra 9
  // do prompt (GeradorDietaGemini.MontarPrompt) para os nomes e o
  // FusoHorarioEstado para o fuso. Se uma delas mudar, mudar aqui também.
  const NORTE_NORDESTE = ['AC', 'AM', 'AP', 'PA', 'RO', 'RR', 'TO', 'AL', 'BA', 'CE', 'MA', 'PB', 'PE', 'PI', 'RN', 'SE'];
  function mostrarPrevia(uf) {
    document.getElementById('previa-uf').textContent = uf;
    document.getElementById('previa-mandioca').textContent =
      uf === 'RJ' ? 'Aipim' : NORTE_NORDESTE.includes(uf) ? 'Macaxeira' : 'Mandioca';
    document.getElementById('previa-feijao').textContent =
      ['PR', 'RS', 'SC'].includes(uf) ? 'Feijão preto' : 'Feijão';
    document.getElementById('previa-fuso').textContent =
      uf === 'AC' ? 'do Acre (UTC−5)'
        : ['AM', 'RO', 'RR', 'MT', 'MS'].includes(uf) ? 'do Amazonas (UTC−4)'
          : 'de Brasília (UTC−3)';
    previa.hidden = false;
  }

  form.addEventListener('change', (evento) => {
    if (evento.target.name === 'estado') mostrarPrevia(evento.target.value);
  });

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
      await ronuSalvarEstado(form.elements.estado.value);
      await ronuRedirecionarPosAuth();
    } catch (erro) {
      ronuMostrarErroFormulario(formError, erro.message);
      ronuDefinirCarregando(submitBtn, false);
    }
  });
});
