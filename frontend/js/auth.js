// Ronu — Auth
// Cliente mínimo para os endpoints públicos de autenticação da API
// (POST /api/auth/cadastro, POST /api/auth/login) e utilitários de sessão/formulário
// compartilhados entre cadastro.html, login.html e dashboard.html.

const RONU_API_BASE = 'http://localhost:5011/api';
const RONU_TOKEN_KEY = 'ronu:token';
const RONU_USUARIO_KEY = 'ronu:usuario';

function ronuSalvarSessao(token, usuario) {
  localStorage.setItem(RONU_TOKEN_KEY, token);
  localStorage.setItem(RONU_USUARIO_KEY, JSON.stringify(usuario));
}

function ronuLimparSessao() {
  localStorage.removeItem(RONU_TOKEN_KEY);
  localStorage.removeItem(RONU_USUARIO_KEY);
}

function ronuUsuarioLogado() {
  const bruto = localStorage.getItem(RONU_USUARIO_KEY);
  return bruto ? JSON.parse(bruto) : null;
}

async function ronuLogin(email, senha) {
  const resposta = await fetch(`${RONU_API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, senha })
  });

  const dados = await resposta.json().catch(() => null);

  if (!resposta.ok) {
    throw new Error(dados?.mensagem || 'Não foi possível entrar. Tente novamente.');
  }

  ronuSalvarSessao(dados.token, dados.usuario);
  return dados;
}

async function ronuCadastrar(nome, email, senha) {
  const resposta = await fetch(`${RONU_API_BASE}/auth/cadastro`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ nome, email, senha })
  });

  const dados = await resposta.json().catch(() => null);

  if (!resposta.ok) {
    throw new Error(dados?.mensagem || 'Não foi possível criar sua conta. Tente novamente.');
  }

  return dados;
}

function ronuMostrarErroFormulario(elemento, mensagem) {
  elemento.textContent = mensagem;
  elemento.hidden = false;
}

function ronuOcultarErroFormulario(elemento) {
  elemento.hidden = true;
  elemento.textContent = '';
}

function ronuMarcarCampoInvalido(campoEl, mensagemEl, mensagem) {
  campoEl.classList.add('field-error');
  mensagemEl.hidden = false;
  mensagemEl.textContent = mensagem;
}

function ronuLimparCampoInvalido(campoEl, mensagemEl) {
  campoEl.classList.remove('field-error');
  mensagemEl.hidden = true;
  mensagemEl.textContent = '';
}

function ronuDefinirCarregando(botao, carregando, textoCarregando) {
  const label = botao.querySelector('.btn-label');

  if (carregando) {
    botao.dataset.loading = 'true';
    botao.dataset.textoOriginal = label.textContent;
    label.textContent = textoCarregando;
  } else {
    botao.dataset.loading = 'false';
    label.textContent = botao.dataset.textoOriginal || label.textContent;
  }
}
