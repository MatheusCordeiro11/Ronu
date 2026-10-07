// Ronu — Criar senha nova (link do email: redefinir-senha.html#token=...)
// Fluxo: tira o token da URL, confere o link na API e só então mostra o
// formulário. Link inválido, expirado ou usado vira um estado próprio, com o
// próximo passo. Senha trocada: a sessão local sai (os tokens antigos já não
// valem) e a pessoa vai ao login com a faixa de sucesso.

const RONU_MSG_VERIFICAR_FALHOU = 'Tente de novo em instantes.';
const RONU_MSG_REDEFINIR_FALHOU = 'Não foi possível salvar a senha agora. Tente de novo em instantes.';
const RONU_MSG_LINK_INVALIDO = 'Este link de redefinição não é válido. Se você pediu mais de um, use o mais recente.';

// Texto de cada motivo. O do "invalido" vem da API quando ela manda.
const RONU_LINK_PROBLEMAS = {
  invalido: {
    titulo: 'Link inválido',
    texto: null,
    principal: { texto: 'Pedir um novo link', href: 'esqueci-senha.html' },
    secundaria: { texto: 'Voltar para o login', href: 'login.html' }
  },
  expirado: {
    titulo: 'Link expirado',
    texto: 'Os links valem por 30 minutos. Peça um novo: leva menos de um minuto.',
    principal: { texto: 'Pedir um novo link', href: 'esqueci-senha.html' },
    secundaria: { texto: 'Voltar para o login', href: 'login.html' }
  },
  // O mais provável é a pessoa já ter trocado a senha: "Entrar" vem primeiro.
  usado: {
    titulo: 'Link já usado',
    texto: 'Este link já criou uma senha nova. Se foi você, é só entrar.',
    principal: { texto: 'Entrar', href: 'login.html' },
    secundaria: { texto: 'Pedir um novo link', href: 'esqueci-senha.html' }
  }
};

document.addEventListener('DOMContentLoaded', () => {
  const secoes = {
    verificando: document.getElementById('estado-verificando'),
    link: document.getElementById('estado-link'),
    erro: document.getElementById('estado-erro'),
    formulario: document.getElementById('estado-formulario')
  };
  const verificandoStatus = document.getElementById('verificando-status');
  const verificarError = document.getElementById('verificar-error');
  const form = document.getElementById('redefinir-form');
  const formError = document.getElementById('form-error');
  const submitBtn = document.getElementById('submit-btn');
  const campoSenha = document.getElementById('field-senha-nova');
  const senhaErro = document.getElementById('senha-nova-error');
  const campoConfirmar = document.getElementById('field-confirmar-senha');
  const confirmarErro = document.getElementById('confirmar-senha-error');

  // O token sai da barra de endereço na hora (não fica no histórico nem em
  // print da tela) e fica guardado só nesta aba, para sobreviver a um F5.
  const tokenDaUrl = new URLSearchParams(window.location.hash.slice(1)).get('token');
  if (tokenDaUrl) {
    sessionStorage.setItem(RONU_TOKEN_REDEFINICAO_KEY, tokenDaUrl);
    history.replaceState(null, '', window.location.pathname + window.location.search);
  }
  const token = sessionStorage.getItem(RONU_TOKEN_REDEFINICAO_KEY);

  function mostrarProblemaNoLink(motivo, mensagemDaApi, focar = true) {
    sessionStorage.removeItem(RONU_TOKEN_REDEFINICAO_KEY);
    const problema = RONU_LINK_PROBLEMAS[motivo] || RONU_LINK_PROBLEMAS.invalido;
    const principal = document.getElementById('link-acao-principal');
    const secundaria = document.getElementById('link-acao-secundaria');

    document.getElementById('link-titulo').textContent = problema.titulo;
    document.getElementById('link-texto').textContent = problema.texto || mensagemDaApi || RONU_MSG_LINK_INVALIDO;
    principal.textContent = problema.principal.texto;
    principal.href = problema.principal.href;
    secundaria.textContent = problema.secundaria.texto;
    secundaria.href = problema.secundaria.href;
    ronuMostrarEstado(secoes, 'link', { focar });
  }

  async function verificarLink(focar) {
    ronuMostrarEstado(secoes, 'verificando', { focar });
    verificandoStatus.textContent = 'Conferindo o link...';
    const cancelarAvisoDeEspera = ronuAvisoDeEspera(verificandoStatus);

    try {
      const dados = await ronuVerificarLinkRedefinicao(token);
      document.getElementById('email-conta').value = dados?.email || '';
      ronuMostrarEstado(secoes, 'formulario');
    } catch (erro) {
      if (erro.motivo) {
        mostrarProblemaNoLink(erro.motivo, erro.message);
      } else {
        ronuMostrarErroFormulario(verificarError, erro.temporario || !erro.message ? RONU_MSG_VERIFICAR_FALHOU : erro.message);
        ronuMostrarEstado(secoes, 'erro');
      }
    } finally {
      cancelarAvisoDeEspera();
    }
  }

  if (!token) {
    mostrarProblemaNoLink('invalido', null, false);
  } else {
    verificarLink(false);
  }

  document.getElementById('tentar-de-novo-btn').addEventListener('click', () => verificarLink(true));

  [[campoSenha, senhaErro], [campoConfirmar, confirmarErro]].forEach(([campo, mensagem]) => {
    campo.querySelector('input').addEventListener('input', () => ronuLimparCampoInvalido(campo, mensagem));
  });

  form.addEventListener('submit', async (evento) => {
    evento.preventDefault();

    ronuOcultarErroFormulario(formError);
    ronuLimparCampoInvalido(campoSenha, senhaErro);
    ronuLimparCampoInvalido(campoConfirmar, confirmarErro);

    const senha = document.getElementById('senha-nova').value;
    const confirmar = document.getElementById('confirmar-senha').value;

    if (senha.length < 8) {
      ronuMarcarCampoInvalido(campoSenha, senhaErro, 'A senha deve ter pelo menos 8 caracteres.');
      document.getElementById('senha-nova').focus();
      return;
    }
    if (confirmar !== senha) {
      ronuMarcarCampoInvalido(campoConfirmar, confirmarErro, 'As senhas não coincidem.');
      document.getElementById('confirmar-senha').focus();
      return;
    }

    ronuDefinirCarregando(submitBtn, true, 'Salvando...');
    const cancelarAvisoDeEspera = ronuAvisoDeEspera(submitBtn.querySelector('.btn-label'));

    try {
      await ronuRedefinirSenha(token, senha);
      sessionStorage.removeItem(RONU_TOKEN_REDEFINICAO_KEY);
      ronuLimparSessao();
      window.location.replace('login.html?senha=redefinida');
    } catch (erro) {
      cancelarAvisoDeEspera();
      ronuDefinirCarregando(submitBtn, false);
      if (erro.motivo) {
        // O link venceu (ou foi trocado por outro) enquanto a pessoa digitava.
        mostrarProblemaNoLink(erro.motivo, erro.message);
      } else {
        ronuMostrarErroFormulario(formError, erro.temporario || !erro.message ? RONU_MSG_REDEFINIR_FALHOU : erro.message);
      }
    } finally {
      cancelarAvisoDeEspera();
    }
  });
});
