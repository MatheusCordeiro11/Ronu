// Ronu — Auth
// Cliente mínimo para os endpoints públicos de autenticação da API
// (POST /api/auth/cadastro, POST /api/auth/login) e utilitários de sessão/formulário
// compartilhados entre cadastro.html, login.html e dashboard.html.

// Único ponto de configuração do frontend — ao fazer deploy, troque
// API_BASE aqui (não há build step/variável de ambiente neste projeto,
// então esse valor tem que ser editado à mão por ambiente).
const RONU_CONFIG = {
  API_BASE: 'https://ronu-api-dwcbcwgqgzgdhhag.chilecentral-01.azurewebsites.net/api',
};

const RONU_TOKEN_KEY = 'ronu:token';
const RONU_USUARIO_KEY = 'ronu:usuario';
// 'true' quando o último login avisou que a conta ainda não tem estado (UF) —
// contas via Google ou anteriores ao campo. Lido por ronuRedirecionarPosAuth.
const RONU_PRECISA_ESTADO_KEY = 'ronu:precisaEstado';

function ronuSalvarSessao(token, usuario) {
  localStorage.setItem(RONU_TOKEN_KEY, token);
  localStorage.setItem(RONU_USUARIO_KEY, JSON.stringify(usuario));
}

function ronuLimparSessao() {
  localStorage.removeItem(RONU_TOKEN_KEY);
  localStorage.removeItem(RONU_USUARIO_KEY);
  localStorage.removeItem(RONU_PRECISA_ESTADO_KEY);
}

function ronuUsuarioLogado() {
  const bruto = localStorage.getItem(RONU_USUARIO_KEY);
  return bruto ? JSON.parse(bruto) : null;
}

// Duas iniciais (primeiro + último nome) para o avatar do header. Usado por
// dashboard.html — nome com uma palavra só usa as duas primeiras letras dela.
function ronuIniciais(nome) {
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '';
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();
  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
}

async function ronuLogin(email, senha) {
  const resposta = await fetch(`${RONU_CONFIG.API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, senha })
  });

  const dados = await resposta.json().catch(() => null);

  if (!resposta.ok) {
    throw new Error(dados?.mensagem || 'Não foi possível entrar. Tente novamente.');
  }

  ronuSalvarSessao(dados.token, dados.usuario);
  localStorage.setItem(RONU_PRECISA_ESTADO_KEY, String(dados.precisaInformarEstado));
  return dados;
}

// Recebe o credential (ID token) que o Google Identity Services devolve no
// callback do botão "Entrar/Cadastrar com Google" e troca por uma sessão
// Ronu — o backend é quem valida a assinatura do token junto ao Google antes
// de confiar em qualquer dado nele; o frontend só repassa.
async function ronuLoginComGoogle(idToken) {
  const resposta = await fetch(`${RONU_CONFIG.API_BASE}/auth/google`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ idToken })
  });

  const dados = await resposta.json().catch(() => null);

  if (!resposta.ok) {
    throw new Error(dados?.mensagem || 'Não foi possível entrar com o Google. Tente novamente.');
  }

  ronuSalvarSessao(dados.token, dados.usuario);
  localStorage.setItem(RONU_PRECISA_ESTADO_KEY, String(dados.precisaInformarEstado));
  return dados;
}

async function ronuCadastrar(nome, email, senha, estado) {
  const resposta = await fetch(`${RONU_CONFIG.API_BASE}/auth/cadastro`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ nome, email, senha, estado })
  });

  const dados = await resposta.json().catch(() => null);

  if (!resposta.ok) {
    throw new Error(dados?.mensagem || 'Não foi possível criar sua conta. Tente novamente.');
  }

  return dados;
}

// Faz uma chamada autenticada, anexando o Bearer token guardado na sessão.
// Se a API responder 401 (token ausente/expirado), limpa a sessão e manda
// pro login com um aviso — nenhuma tela protegida deveria seguir renderizando
// depois de um 401, então isso já interrompe o fluxo com um throw.
async function ronuFetchAutenticado(caminho, opcoes = {}) {
  const headers = new Headers(opcoes.headers || {});
  headers.set('Authorization', `Bearer ${localStorage.getItem(RONU_TOKEN_KEY)}`);

  const resposta = await fetch(`${RONU_CONFIG.API_BASE}${caminho}`, { ...opcoes, headers });

  if (resposta.status === 401) {
    ronuLimparSessao();
    window.location.href = 'login.html?sessao=expirada';
    throw new Error('Sessão expirada.');
  }

  return resposta;
}

// Um cadastro é considerado completo quando o usuário já tem perfil (altura,
// sexo, data de nascimento), objetivo/peso registrado e pelo menos uma
// modalidade vinculada (preferências é opcional). Usado logo após
// login/cadastro e ao abrir o onboarding, pra decidir entre mandar o usuário
// pro onboarding ou direto pro dashboard.
async function ronuChecarCadastroCompleto() {
  const [respostaPerfil, respostaObjetivo, respostaModalidades] = await Promise.all([
    ronuFetchAutenticado('/perfil'),
    ronuFetchAutenticado('/objetivos/atual'),
    ronuFetchAutenticado('/usuarios/modalidades')
  ]);

  const perfil = respostaPerfil.ok ? await respostaPerfil.json() : null;
  const perfilCompleto = Boolean(
    perfil && perfil.altura != null && perfil.sexo != null && perfil.dataNascimento != null
  );
  const temObjetivo = respostaObjetivo.status === 200;
  const modalidades = respostaModalidades.ok ? await respostaModalidades.json() : [];
  const temModalidade = Array.isArray(modalidades) && modalidades.length > 0;

  return perfilCompleto && temObjetivo && temModalidade;
}

// Chamado depois de um login/cadastro bem-sucedido. Se a checagem de
// completude falhar por erro de rede (não 401, que ronuFetchAutenticado já
// trata sozinho), assume o caminho mais seguro: manda pro onboarding, que
// vai mostrar o próprio erro se a API realmente estiver fora do ar.
async function ronuRedirecionarPosAuth() {
  if (localStorage.getItem(RONU_PRECISA_ESTADO_KEY) === 'true') {
    window.location.href = 'estado.html';
    return;
  }

  try {
    const completo = await ronuChecarCadastroCompleto();
    window.location.href = completo ? 'dashboard.html' : 'onboarding.html';
  } catch (erro) {
    window.location.href = 'onboarding.html';
  }
}

// PUT /perfil/estado — usado por estado.html. A API normaliza e valida a UF;
// em 400 devolve { mensagem }, repassada aqui como Error pra tela exibir.
async function ronuSalvarEstado(estado) {
  const resposta = await ronuFetchAutenticado('/perfil/estado', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ estado })
  });

  const dados = await resposta.json().catch(() => null);

  if (!resposta.ok) {
    throw new Error(dados?.mensagem || 'Não foi possível salvar seu estado. Tente novamente.');
  }

  localStorage.removeItem(RONU_PRECISA_ESTADO_KEY);
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

// O campo de altura é digitado em metros (hábito natural em pt-BR, ex: "1,74"),
// mas a API sempre recebe/devolve centímetros (PerfilRequest/PerfilResponse não
// mudam) — a conversão fica isolada aqui pra ser usada igual no onboarding e em
// configurações. Aceita vírgula ou ponto como separador decimal. Retorna null
// se o texto não for um número válido (quem chama decide a mensagem de erro).
function ronuParseAlturaCm(texto) {
  const normalizado = texto.trim().replace(',', '.');
  const metros = parseFloat(normalizado);
  if (Number.isNaN(metros)) return null;
  // Arredonda pra 1 casa decimal de cm (mesma granularidade que o campo em cm
  // já usava) evitando erro de ponto flutuante (ex: 1.74 * 100 = 173.99999...).
  return Math.round(metros * 1000) / 10;
}

// Inverso de ronuParseAlturaCm — usado ao carregar um valor salvo, pra
// preencher o campo já em metros com vírgula (ex: 174 -> "1,74").
function ronuFormatarAlturaMetros(alturaCm) {
  return (alturaCm / 100).toFixed(2).replace('.', ',');
}

// Estado de envio de um botão. Usa o disabled nativo (não só o
// pointer-events: none do CSS, que bloqueia o mouse mas não o teclado):
// assim Enter/Espaço no botão focado e Enter num campo do formulário
// (envio implícito) também não disparam um segundo envio. Guarda o disabled
// de antes e o restaura ao terminar — o botão pode já estar desabilitado por
// outro motivo (perfil incompleto, nada alterado) e não deve ser reabilitado.
function ronuDefinirCarregando(botao, carregando, textoCarregando) {
  const label = botao.querySelector('.btn-label');

  if (carregando) {
    // Chamada dupla sem terminar a primeira: guardar de novo registraria o
    // disabled que a primeira acabou de pôr como "estado anterior", e o
    // botão nunca mais seria reabilitado.
    if (botao.dataset.loading === 'true') return;

    botao.dataset.loading = 'true';
    botao.dataset.disabledAntes = String(botao.disabled);
    botao.disabled = true;
    botao.dataset.textoOriginal = label.textContent;
    label.textContent = textoCarregando;
  } else {
    botao.dataset.loading = 'false';
    // Só restaura se houve um "carregando" antes; senão não mexe no disabled.
    if ('disabledAntes' in botao.dataset) {
      botao.disabled = botao.dataset.disabledAntes === 'true';
      delete botao.dataset.disabledAntes;
    }
    label.textContent = botao.dataset.textoOriginal || label.textContent;
  }
}
