// Ronu — Autorização dos dados de saúde (consentimento.html)
// Aparece quando a conta ainda não aceitou a versão vigente: conta Google nova,
// conta anterior ao consentimento, ou texto novo. Chega aqui pelo login
// (precisaConsentimento) ou por qualquer 403 com motivo "consentimento"
// (ronuFetchAutenticado). Quem já aceitou segue o caminho normal.
// Recusar não tem botão próprio: sem a autorização o Ronu não funciona, então
// as saídas são sair ou apagar a conta (revogar = apagar).

document.addEventListener('DOMContentLoaded', () => {
  if (!ronuUsuarioLogado()) {
    window.location.href = 'login.html';
    return;
  }

  const form = document.getElementById('consentimento-form');
  const formError = document.getElementById('form-error');
  const submitBtn = document.getElementById('submit-btn');
  const caixa = document.getElementById('aceite');
  const rotulo = document.getElementById('consentimento-rotulo');
  const intro = document.getElementById('consentimento-intro');
  const btnApagar = document.getElementById('apagar-btn');

  // Segue o caminho normal (estado, onboarding ou painel). Um erro da checagem
  // vai para o painel, que tem a tela de erro com "Tentar novamente".
  function seguir() {
    localStorage.removeItem(RONU_PRECISA_CONSENTIMENTO_KEY);
    ronuRedirecionarPosAuth().catch((erro) => {
      if (erro.message !== 'Sessão expirada.') window.location.href = 'dashboard.html';
    });
  }

  RonuAceite.iniciar(caixa);
  caixa.addEventListener('aceite:carregado', (evento) => {
    const { precisaAceitar, aceito } = evento.detail;
    if (!precisaAceitar) {
      seguir();
      return;
    }
    // Já aceitou uma versão anterior: o texto mudou.
    if (aceito) {
      rotulo.textContent = 'O texto mudou';
      intro.textContent = 'O texto da autorização mudou desde que você aceitou. Leia e autorize de novo para continuar.';
    }
  });
  // Mostra a tela só a quem precisa aceitar, ou no erro (para o "Tentar de novo").
  RonuAceite.carregar(caixa, { logado: true }).then((corpo) => {
    if (!corpo || corpo.precisaAceitar) document.getElementById('consentimento-conteudo').hidden = false;
  });

  document.getElementById('sair-btn').addEventListener('click', () => {
    ronuLimparSessao();
    window.location.href = 'login.html';
  });

  // Como a conta confirma a exclusão (senha ou frase) vem da API: o /perfil
  // ainda responde 403 aqui.
  btnApagar.addEventListener('click', async () => {
    if (btnApagar.dataset.carregando === 'true') return;
    btnApagar.dataset.carregando = 'true';
    ronuOcultarErroFormulario(formError);
    try {
      const resposta = await ronuFetchAutenticado('/conta');
      const conta = resposta.ok ? await resposta.json().catch(() => null) : null;
      if (!conta) throw new Error('');
      RonuConta.abrirExclusao({ temSenha: conta.temSenha, revogando: true, origem: btnApagar });
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      ronuMostrarErroFormulario(formError, 'Não foi possível abrir a exclusão da conta agora. Tente de novo.');
    } finally {
      delete btnApagar.dataset.carregando;
    }
  });

  form.addEventListener('submit', async (evento) => {
    evento.preventDefault();
    ronuOcultarErroFormulario(formError);

    const versao = RonuAceite.versaoMarcada(caixa);
    if (versao == null) return;

    ronuDefinirCarregando(submitBtn, true, 'Salvando...');
    try {
      const resposta = await ronuFetchAutenticado('/consentimentos', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tipo: 'geral', versao })
      });

      if (resposta.ok) {
        seguir();
        return;
      }

      const corpo = await resposta.json().catch(() => null);
      ronuDefinirCarregando(submitBtn, false);
      if (resposta.status === 409) {
        await RonuAceite.textoMudou(caixa, corpo?.mensagem);
        return;
      }
      ronuMostrarErroFormulario(formError, corpo?.mensagem || 'Não foi possível salvar a autorização. Tente de novo.');
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return;
      ronuDefinirCarregando(submitBtn, false);
      ronuMostrarErroFormulario(formError, 'Não foi possível salvar a autorização. Verifique sua conexão e tente de novo.');
    }
  });
});
