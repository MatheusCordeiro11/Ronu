// Ronu — Convite para uma casa (casa-convite.html#convite=<token>), plano família, etapa 7
// Fluxo: tira o token da URL (fica só nesta aba, no sessionStorage) -> sem
// login, mostra "Entrar" e "Criar conta" e volta para cá no fim do caminho
// (S4, ver ronuConvitePendente em auth.js) -> logado, POST /casa/convites/verificar
// (não usa o convite) -> o convite válido, com a caixa de aceite preenchida
// pelo texto que veio na resposta, ou um dos 7 estados de problema, com o foco
// no título. "Entrar na casa" faz o POST /casa/convites/aceitar.

(function () {
  // O token sai da barra de endereço na hora: não fica no histórico, num print
  // da tela nem num link copiado de novo.
  const tokenDaUrl = new URLSearchParams(window.location.hash.slice(1)).get('convite');
  if (tokenDaUrl) {
    ronuGuardarConvite(tokenDaUrl);
    history.replaceState(null, '', window.location.pathname + window.location.search);
  }

  // Sem login: o cabeçalho das telas logadas (nav, avatar, abas) não faz
  // sentido. Troca antes de o cabecalho.js montar (ele procura [data-cabecalho]).
  if (!ronuUsuarioLogado()) {
    const logado = document.getElementById('cv-cabecalho');
    logado.removeAttribute('data-cabecalho');
    logado.hidden = true;
    document.getElementById('cv-cabecalho-simples').hidden = false;
    document.body.classList.remove('com-abas');
  }
})();

// Título, frase e saída de cada motivo do POST verificar/aceitar (tabela do shape, seção 3.2).
const RONU_CONVITE_PROBLEMAS = {
  invalido: {
    titulo: 'Este convite não vale mais',
    texto: 'O link foi cancelado ou não existe. Peça um link novo para quem convidou você.',
    final: true
  },
  usado: {
    titulo: 'Este convite já foi usado',
    texto: 'Cada link vale para uma pessoa só. Peça um link novo para quem convidou você.',
    final: true
  },
  expirado: {
    titulo: 'Este convite venceu',
    texto: 'Os convites valem 7 dias. Peça um link novo para quem convidou você.',
    final: true
  },
  ja_em_casa: {
    titulo: 'Você já está numa casa',
    texto: 'Cada pessoa fica numa casa só. Para entrar nesta, saia da sua antes, na aba Casa.',
    acao: { texto: 'Ir para a sua casa', href: 'casa.html' },
    final: true
  },
  // O token continua guardado: o onboarding termina no painel, que traz a pessoa de volta.
  cadastro_incompleto: {
    titulo: 'Falta terminar o seu cadastro',
    texto: 'A porção de cada um sai da própria meta, e a meta precisa da sua altura, do sexo, da data de nascimento e do objetivo. Termine o cadastro e você volta para este convite.',
    acao: { texto: 'Terminar cadastro', href: 'onboarding.html', principal: true },
    final: false
  },
  menor_de_18: {
    titulo: 'O Ronu é para maiores de 18 anos',
    texto: 'A casa reúne adultos, cada um com a própria conta. Este convite não pode ser aceito por esta conta.',
    final: true
  },
  casa_cheia: {
    titulo: 'Esta casa já tem 6 pessoas',
    texto: 'Uma casa reúne até 6 adultos. Se alguém sair, quem convidou pode mandar um link novo.',
    final: true
  }
};

document.addEventListener('DOMContentLoaded', () => {
  const $ = (id) => document.getElementById(id);
  const secoes = {
    verificando: $('cv-verificando'),
    semLogin: $('cv-sem-login'),
    valido: $('cv-valido'),
    recusado: $('cv-recusado'),
    problema: $('cv-problema')
  };
  const caixa = $('cv-aceite');
  const btnEntrar = $('cv-entrar');
  const elErroEntrar = $('cv-erro-entrar');
  let primeiraTela = true;

  // Mostra um estado e leva o foco ao título (o leitor de tela anuncia onde a
  // pessoa está). Na primeira tela depois do "Abrindo o convite..." também: a
  // página mudou de assunto.
  function mostrar(nome) {
    Object.entries(secoes).forEach(([chave, secao]) => { secao.hidden = chave !== nome; });
    const secao = secoes[nome];
    if (nome !== 'verificando') {
      if (!primeiraTela) {
        secao.dataset.entrando = 'true';
        secao.addEventListener('animationend', () => delete secao.dataset.entrando, { once: true });
      }
      primeiraTela = false;
      secao.querySelector('.cv-titulo')?.focus();
    }
    return secao;
  }

  function mostrarProblema(motivo, mensagemDaApi) {
    const p = RONU_CONVITE_PROBLEMAS[motivo];
    if (p?.final) ronuEsquecerConvite();
    $('cv-problema-titulo').textContent = p ? p.titulo : 'Não foi possível abrir o convite';
    $('cv-problema-texto').textContent = p ? p.texto : (mensagemDaApi || 'Tente de novo em instantes.');

    const acoes = $('cv-problema-acoes');
    acoes.replaceChildren();
    if (p?.acao) {
      const a = document.createElement('a');
      a.className = `botao ${p.acao.principal ? 'botao-primario' : 'botao-fantasma'}`;
      a.href = p.acao.href;
      a.textContent = p.acao.texto;
      acoes.append(a);
    }
    if (!p) {
      const tentar = document.createElement('button');
      tentar.type = 'button';
      tentar.className = 'botao botao-fantasma';
      tentar.textContent = 'Tentar de novo';
      tentar.addEventListener('click', verificar);
      acoes.append(tentar);
    }
    mostrar('problema');
  }

  // ---------- Sem login ----------

  if (!ronuUsuarioLogado()) {
    if (!ronuConvitePendente()) {
      mostrarProblema('invalido');
      return;
    }
    mostrar('semLogin');
    return;
  }

  // ---------- Logado ----------

  async function postar(caminho, corpo) {
    try {
      const resposta = await ronuFetchAutenticado(caminho, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(corpo)
      });
      return { status: resposta.status, ok: resposta.ok, corpo: await resposta.json().catch(() => null) };
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return null;
      return { status: 0, ok: false, corpo: null };
    }
  }

  async function verificar() {
    const token = ronuConvitePendente();
    if (!token) {
      mostrarProblema('invalido');
      return;
    }
    mostrar('verificando');
    const r = await postar('/casa/convites/verificar', { token });
    if (!r) return;

    if (r.ok) {
      mostrarValido(r.corpo);
      return;
    }
    if (r.corpo?.motivo && RONU_CONVITE_PROBLEMAS[r.corpo.motivo]) {
      mostrarProblema(r.corpo.motivo);
      return;
    }
    // 429, 5xx, rede: o convite continua guardado e a pessoa tenta de novo.
    mostrarProblema(null, r.status === 0
      ? 'Não foi possível falar com o Ronu agora. Verifique sua conexão e tente de novo.'
      : r.corpo?.mensagem);
  }

  function mostrarValido(dados) {
    const nome = dados.primeiroNomeDono;
    $('cv-valido-titulo').textContent = `${nome} convidou você para a casa`;
    const n = dados.membros;
    $('cv-valido-frase').textContent =
      `Na casa, o almoço e o jantar saem da mesma panela. Cada pessoa come a porção da própria meta. Hoje a casa tem ${n} ${n === 1 ? 'pessoa' : 'pessoas'}.`;
    // A API manda só o dia ("2026-11-20", já no fuso da conta). Lido do texto, e não com new Date(): a data sem hora
    // vira meia-noite UTC, que no Brasil ainda é o dia anterior.
    const dia = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dados.expiraEm || '');
    $('cv-validade').textContent = dia
      ? `Link de uso único, válido até ${dia[3]}/${dia[2]}. A sua conta já confirma 18 anos ou mais.`
      : 'Link de uso único, válido por 7 dias desde que foi gerado. A sua conta já confirma 18 anos ou mais.';
    RonuAceite.iniciar(caixa);
    RonuAceite.preencher(caixa, dados.consentimento, { tentar: verificar });
    btnEntrar.disabled = true;
    elErroEntrar.hidden = true;
    mostrar('valido');
  }

  // "Entrar na casa" só com a caixa marcada (o canvas desabilita o botão).
  caixa.addEventListener('change', () => {
    btnEntrar.disabled = !caixa.querySelector('input[type="checkbox"]').checked;
  });

  btnEntrar.addEventListener('click', async () => {
    if (btnEntrar.dataset.loading === 'true') return;
    elErroEntrar.hidden = true;
    const versao = RonuAceite.versaoMarcada(caixa, { mensagem: 'Para entrar, marque que leu o que é compartilhado.' });
    if (versao == null) return;

    ronuDefinirCarregando(btnEntrar, true, 'Entrando na casa...');
    btnEntrar.setAttribute('aria-busy', 'true');
    const cancelarEspera = ronuAvisoDeEspera(btnEntrar.querySelector('.btn-label'));
    const r = await postar('/casa/convites/aceitar', { token: ronuConvitePendente(), versaoConsentimento: versao });
    cancelarEspera();
    if (!r) return;
    ronuDefinirCarregando(btnEntrar, false);
    btnEntrar.removeAttribute('aria-busy');

    if (r.ok) {
      ronuEsquecerConvite();
      window.location.href = 'casa.html';
      return;
    }
    if (r.corpo?.motivo === 'consentimento_casa') {
      await RonuAceite.textoMudou(caixa, r.corpo.mensagem);
      btnEntrar.disabled = true;
      return;
    }
    if (r.corpo?.motivo && RONU_CONVITE_PROBLEMAS[r.corpo.motivo]) {
      mostrarProblema(r.corpo.motivo);
      return;
    }
    elErroEntrar.textContent = r.status === 0
      ? 'Não foi possível falar com o Ronu agora. Verifique sua conexão e tente de novo.'
      : (r.corpo?.mensagem || 'Não foi possível entrar na casa agora. Tente de novo.');
    elErroEntrar.hidden = false;
  });

  $('cv-recusar').addEventListener('click', () => {
    ronuEsquecerConvite();
    mostrar('recusado');
  });

  verificar();
});
