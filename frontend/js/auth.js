// Ronu — Auth
// Cliente mínimo para os endpoints públicos de autenticação da API
// (POST /api/auth/cadastro, POST /api/auth/login, esqueci minha senha) e
// utilitários de sessão/formulário compartilhados entre cadastro.html,
// login.html, esqueci-senha.html, redefinir-senha.html e dashboard.html.

// Único ponto de configuração do frontend. Não há build step nem variável de
// ambiente neste projeto, então o ambiente é deduzido do endereço da página:
// aberta em localhost ou 127.0.0.1 (Live Server), fala com a API local; em
// qualquer outro endereço, com a de produção. Acesso pelo IP da rede (ex.:
// celular em 192.168.x.x) cai em produção de propósito — a API local não
// escuta na rede nem libera essa origem no CORS.
const RONU_API_LOCAL = 'http://localhost:5011/api';
const RONU_API_PRODUCAO = 'https://ronu-api-dwcbcwgqgzgdhhag.chilecentral-01.azurewebsites.net/api';
const RONU_CONFIG = {
  API_BASE: ['localhost', '127.0.0.1'].includes(window.location.hostname)
    ? RONU_API_LOCAL
    : RONU_API_PRODUCAO,
};

const RONU_TOKEN_KEY = 'ronu:token';
const RONU_USUARIO_KEY = 'ronu:usuario';
// 'true' quando o último login avisou que a conta ainda não tem estado (UF) —
// contas via Google ou anteriores ao campo. Lido por ronuRedirecionarPosAuth.
const RONU_PRECISA_ESTADO_KEY = 'ronu:precisaEstado';
// Id do usuário cujo cadastro já foi visto completo (ver ronuCadastroJaCompleto).
const RONU_CADASTRO_COMPLETO_KEY = 'ronu:cadastroCompleto';

function ronuSalvarSessao(token, usuario) {
  localStorage.setItem(RONU_TOKEN_KEY, token);
  localStorage.setItem(RONU_USUARIO_KEY, JSON.stringify(usuario));
}

function ronuLimparSessao() {
  localStorage.removeItem(RONU_TOKEN_KEY);
  localStorage.removeItem(RONU_USUARIO_KEY);
  localStorage.removeItem(RONU_PRECISA_ESTADO_KEY);
  localStorage.removeItem(RONU_CADASTRO_COMPLETO_KEY);
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

// ---------- Esqueci minha senha ----------
// Email digitado no login, levado para esqueci-senha.html ao clicar em
// "Esqueci minha senha" (no sessionStorage, nunca na URL).
const RONU_EMAIL_REDEFINICAO_KEY = 'ronu:emailRedefinicao';
// Token do link de redefinição, tirado da URL ao abrir redefinir-senha.html.
// Fica no sessionStorage (só desta aba) para a página sobreviver a um
// recarregamento; sai ao trocar a senha ou quando o link não vale.
const RONU_TOKEN_REDEFINICAO_KEY = 'ronu:tokenRedefinicao';

// POST público das rotas de redefinição. Devolve o corpo da resposta 2xx;
// senão lança Error com status, motivo (link inválido/expirado/usado, só no
// 400 do link) e temporario (rede ou 5xx). A mensagem é a da API quando ela
// mandou uma; vazia, quem chama usa o próprio texto.
async function ronuPostRedefinicao(caminho, corpo) {
  let resposta;
  try {
    resposta = await fetch(`${RONU_CONFIG.API_BASE}${caminho}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(corpo)
    });
  } catch (falhaDeRede) {
    const erro = new Error('');
    erro.temporario = true;
    throw erro;
  }

  const dados = await resposta.json().catch(() => null);
  if (resposta.ok) return dados;

  const erro = new Error(resposta.status >= 500 ? '' : (dados?.mensagem || ''));
  erro.status = resposta.status;
  erro.motivo = dados?.motivo || null;
  erro.temporario = resposta.status >= 500;
  throw erro;
}

// A resposta é sempre a mesma, exista ou não a conta (202).
function ronuPedirLinkRedefinicao(email) {
  return ronuPostRedefinicao('/auth/esqueci-senha', { email });
}

// 200 { email } se o link vale; 400 com motivo se não.
function ronuVerificarLinkRedefinicao(token) {
  return ronuPostRedefinicao('/auth/redefinir-senha/verificar', { token });
}

function ronuRedefinirSenha(token, novaSenha) {
  return ronuPostRedefinicao('/auth/redefinir-senha', { token, novaSenha });
}

// Troca de estado nas telas de redefinição: cada página é um bloco só, com
// uma <section class="auth-estado"> por estado. Mostra a pedida, esconde as
// outras e, fora da primeira exibição, anima a entrada e leva o foco ao
// título (o leitor de tela anuncia onde a pessoa está).
function ronuMostrarEstado(secoes, nome, { focar = true } = {}) {
  Object.entries(secoes).forEach(([chave, secao]) => {
    secao.hidden = chave !== nome;
  });

  const secao = secoes[nome];
  if (!focar) return secao;

  secao.dataset.entrando = 'true';
  secao.addEventListener('animationend', () => delete secao.dataset.entrando, { once: true });
  secao.querySelector('.auth-title')?.focus();
  return secao;
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

// Mensagem de quando a checagem de cadastro não consegue uma resposta válida
// nem na segunda tentativa. Quem chama mostra na área de erro da própria tela.
const RONU_MSG_FALHA_CHECAGEM = 'Não foi possível carregar seus dados. Tente de novo em instantes.';
// Espera antes da segunda tentativa: cobre a API ainda subindo depois de um
// reinício do App Service, quando as primeiras requisições voltam 5xx.
const RONU_ESPERA_NOVA_TENTATIVA_MS = 2000;

function ronuErroChecagem(temporario) {
  const erro = new Error(RONU_MSG_FALHA_CHECAGEM);
  erro.temporario = temporario;
  return erro;
}

// Uma rodada das 3 chamadas. Devolve true/false só com respostas válidas:
// perfil 200 (completo se tiver altura, sexo e data de nascimento), objetivo
// 200 ou 404 (404 = ainda sem objetivo) e modalidades 200 (lista vazia = sem
// modalidade). Qualquer outra coisa lança erro, nunca vira "incompleto" —
// antes, um 5xx logo depois de um reinício da API mandava para o onboarding
// quem já tinha cadastro completo. 5xx e falha de rede são temporários (vale
// tentar de novo); outro status inesperado não. O 401 segue como já era:
// ronuFetchAutenticado redireciona pro login e lança 'Sessão expirada.'.
async function ronuConsultarCadastro() {
  let respostas;
  try {
    respostas = await Promise.all([
      ronuFetchAutenticado('/perfil'),
      ronuFetchAutenticado('/objetivos/atual'),
      ronuFetchAutenticado('/usuarios/modalidades')
    ]);
  } catch (erro) {
    if (erro.message === 'Sessão expirada.') throw erro;
    throw ronuErroChecagem(true);
  }

  const [respostaPerfil, respostaObjetivo, respostaModalidades] = respostas;

  if (respostas.some((resposta) => resposta.status >= 500)) {
    throw ronuErroChecagem(true);
  }
  if (respostaPerfil.status !== 200
    || (respostaObjetivo.status !== 200 && respostaObjetivo.status !== 404)
    || respostaModalidades.status !== 200) {
    throw ronuErroChecagem(false);
  }

  let perfil;
  let modalidades;
  try {
    perfil = await respostaPerfil.json();
    modalidades = await respostaModalidades.json();
  } catch (erro) {
    // Corpo cortado no meio (conexão caiu) ou que não é JSON.
    throw ronuErroChecagem(true);
  }

  const perfilCompleto = Boolean(
    perfil && perfil.altura != null && perfil.sexo != null && perfil.dataNascimento != null
  );
  const temObjetivo = respostaObjetivo.status === 200;
  const temModalidade = Array.isArray(modalidades) && modalidades.length > 0;

  return perfilCompleto && temObjetivo && temModalidade;
}

// Um cadastro é considerado completo quando o usuário já tem perfil (altura,
// sexo, data de nascimento), objetivo/peso registrado e pelo menos uma
// modalidade vinculada (preferências é opcional). Fonte única desse critério:
// usada depois de login/cadastro, ao abrir o onboarding e ao abrir o dashboard
// sem a marca de cadastro completo, pra decidir entre onboarding e dashboard.
// Quando dá completo, grava a marca (ver ronuCadastroJaCompleto).
// Em erro temporário (5xx, rede), tenta mais uma vez depois de ~2 s; se falhar
// de novo, lança Error(RONU_MSG_FALHA_CHECAGEM) e não mexe na marca — quem
// chama mostra o erro em vez de decidir o caminho.
async function ronuChecarCadastroCompleto() {
  let completo;
  try {
    completo = await ronuConsultarCadastro();
  } catch (erro) {
    if (!erro.temporario) throw erro;
    await new Promise((resolver) => setTimeout(resolver, RONU_ESPERA_NOVA_TENTATIVA_MS));
    completo = await ronuConsultarCadastro();
  }

  const usuario = ronuUsuarioLogado();
  if (completo && usuario) {
    localStorage.setItem(RONU_CADASTRO_COMPLETO_KEY, String(usuario.id));
  } else {
    localStorage.removeItem(RONU_CADASTRO_COMPLETO_KEY);
  }
  return completo;
}

// Atalho para não repetir as 3 chamadas de ronuChecarCadastroCompleto a cada
// abertura do dashboard/onboarding. Um cadastro completo não volta a ficar
// incompleto pela interface: o PUT /perfil exige altura, sexo e data de
// nascimento; a API recusa apagar o último objetivo; e treino.js bloqueia
// remover a última modalidade. Por isso a marca não expira — só some ao
// sair da conta (ronuLimparSessao), e vale só para o usuário que a gravou
// (outro login no mesmo navegador não a herda). Se algo escapar disso, a
// própria API ainda recusa gerar dieta com perfil/objetivo incompleto.
function ronuCadastroJaCompleto() {
  const usuario = ronuUsuarioLogado();
  return Boolean(usuario) && localStorage.getItem(RONU_CADASTRO_COMPLETO_KEY) === String(usuario.id);
}

// Chamado depois de um login/cadastro bem-sucedido. Se a checagem de
// completude falhar (já com a nova tentativa), o erro sobe para quem chamou,
// que o mostra na área de erro da tela: sem uma resposta válida, não dá para
// saber se o caminho é o dashboard ou o onboarding. O 401 também sobe, mas
// ronuFetchAutenticado já redirecionou pro login.
async function ronuRedirecionarPosAuth() {
  if (localStorage.getItem(RONU_PRECISA_ESTADO_KEY) === 'true') {
    window.location.href = 'estado.html';
    return;
  }

  const completo = await ronuChecarCadastroCompleto();
  window.location.href = completo ? 'dashboard.html' : 'onboarding.html';
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

// Aviso de espera para uma operação que pode cair no cold start da API: ela
// roda no plano gratuito do Azure e "dorme" sem uso — o primeiro request
// depois disso levou ~45 s nas medições, contra ~1,5 s com ela acordada. Se a
// operação passar de 5 s, troca o texto do elemento (o .btn-label de um botão
// em carregamento, ou a linha de status do login com Google) explicando a
// demora; aos 15 s, dá a expectativa real de tempo. Devolve a função que
// cancela os avisos: quem chama TEM de chamá-la ao terminar (no finally), senão
// um aviso atrasado sobrescreve o texto já restaurado — ou o de um novo envio.
function ronuAvisoDeEspera(elementoTexto) {
  const avisos = [
    [5000, 'Acordando o servidor...'],
    [15000, 'Pode levar até 1 minuto...']
  ];

  const temporizadores = avisos.map(([atraso, texto]) => setTimeout(() => {
    elementoTexto.textContent = texto;
  }, atraso));

  return () => temporizadores.forEach(clearTimeout);
}

// true do momento em que uma credencial do Google chega até a página trocar
// (ou o login falhar). Uma segunda credencial nesse intervalo é ignorada.
let ronuLoginGoogleEmAndamento = false;

// Callback do Google Identity Services, compartilhado por login.html e
// cadastro.html (initialize({ callback: ronuTratarCredencialGoogle })). O botão
// do Google é um iframe deles — não dá pra trocar o texto dele como no botão
// do formulário —, então o progresso vai numa linha de status logo abaixo
// (#google-status, role="status", anunciada por leitores de tela). Durante a
// espera, o slot do Google e o formulário ficam bloqueados: o formulário fica
// inert (não só o botão desabilitado — no cadastro, digitar reabilitaria o
// botão via revelarEtapa) para não haver dois logins em paralelo.
async function ronuTratarCredencialGoogle(response) {
  const form = document.querySelector('form');
  const submitBtn = document.getElementById('submit-btn');
  const formError = document.getElementById('form-error');
  const slotGoogle = document.getElementById('google-signin-button');
  const status = document.getElementById('google-status');

  // Login por email/senha já em andamento também conta: só um por vez.
  if (ronuLoginGoogleEmAndamento || submitBtn.dataset.loading === 'true') return;
  ronuLoginGoogleEmAndamento = true;

  const submitDesabilitadoAntes = submitBtn.disabled;
  ronuOcultarErroFormulario(formError);
  slotGoogle.dataset.loading = 'true';
  form.inert = true;
  submitBtn.disabled = true;
  status.textContent = 'Entrando com o Google...';
  const cancelarAvisoDeEspera = ronuAvisoDeEspera(status);

  try {
    await ronuLoginComGoogle(response.credential);
    await ronuRedirecionarPosAuth();
  } catch (erro) {
    status.textContent = '';
    slotGoogle.dataset.loading = 'false';
    form.inert = false;
    submitBtn.disabled = submitDesabilitadoAntes;
    ronuLoginGoogleEmAndamento = false;
    ronuMostrarErroFormulario(formError, erro.message);
  } finally {
    cancelarAvisoDeEspera();
  }
}
