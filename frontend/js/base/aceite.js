// Ronu — Caixa de consentimento: a geral (criar-conta.html e autorizacao.html) e a das alergias
// (alimentacao.html e primeiros-passos.html, TACO 2c), escolhida por data-aceite-tipo na caixa.
// O título, o texto e a versão vêm sempre da API: escritos no HTML, o texto
// da tela e o que a API cobra só ficariam iguais por sorte. O cadastro lê a
// rota pública (ainda não há conta); o autorizacao.html lê a rota logada,
// que também diz se a pessoa já aceitou. A tela envia a versão que mostrou.
//
// A caixa no HTML:
//   <div class="aceite" data-aceite [data-aceite-tipo="alergias"] aria-busy="true">
//     <label class="aceite-marcar"><input type="checkbox" disabled ...><span data-aceite-titulo></span></label>
//     <div class="aceite-texto" data-aceite-texto></div>
//     <span class="campo-erro" data-aceite-erro hidden></span>
//   </div>

window.RonuAceite = (function () {
  // Cada tipo tem o próprio texto na API; o título reserva só aparece se o texto não carregar.
  const TITULO_RESERVA = { geral: 'Autorização dos dados de saúde', alergias: 'Autorização das alergias', casa: 'Autorização da casa' };
  const MSG_CARREGAR = 'Não foi possível carregar o texto da autorização.';
  const MSG_SEM_MARCAR = 'Para continuar, autorize o uso dos seus dados de saúde.';

  const tipoDa = (caixa) => caixa.dataset.aceiteTipo || 'geral';

  function partes(caixa) {
    return {
      caixa,
      marcar: caixa.querySelector('input[type="checkbox"]'),
      titulo: caixa.querySelector('[data-aceite-titulo]'),
      texto: caixa.querySelector('[data-aceite-texto]'),
      erro: caixa.querySelector('[data-aceite-erro]')
    };
  }

  // O texto vem com as linhas separadas por \n (TextosConsentimento): cada uma vira um parágrafo, só com
  // textContent. O Perfil (perfil.js) monta o texto aceito do mesmo jeito.
  function paragrafos(texto) {
    return texto.split('\n').map((linha) => {
      const el = document.createElement('p');
      el.textContent = linha;
      return el;
    });
  }

  function mostrarErro(p, mensagem, { comTentar = null } = {}) {
    p.erro.replaceChildren(mensagem);
    if (comTentar) {
      const tentar = document.createElement('button');
      tentar.type = 'button';
      tentar.className = 'link-botao';
      tentar.textContent = 'Tentar de novo';
      tentar.addEventListener('click', comTentar);
      p.erro.append(' ', tentar);
    }
    p.erro.hidden = false;
  }

  function limparErro(p) {
    p.erro.hidden = true;
    p.erro.replaceChildren();
    p.caixa.removeAttribute('aria-invalid');
  }

  // Busca o texto e preenche a caixa. logado: usa a rota com login (que traz o
  // aceite da pessoa); senão, a pública. Devolve o corpo da API, ou null se não
  // carregou (a caixa já mostra o erro com "Tentar de novo").
  async function carregar(caixa, { logado = false } = {}) {
    const p = partes(caixa);
    const tipo = tipoDa(caixa);
    limparErro(p);
    caixa.setAttribute('aria-busy', 'true');
    p.marcar.disabled = true;
    p.marcar.checked = false;
    delete caixa.dataset.versao;

    let corpo = null;
    try {
      const resposta = logado
        ? await ronuFetchAutenticado(`/consentimentos/${tipo}`)
        : await fetch(`${RONU_CONFIG.API_BASE}/consentimentos/vigente/${tipo}`);
      corpo = resposta.ok ? await resposta.json().catch(() => null) : null;
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return null;
    }

    return preencher(caixa, corpo, { tentar: () => carregar(caixa, { logado }) });
  }

  // Preenche a caixa com um texto que já veio da API por outra rota (o convite da casa traz o texto vigente na
  // resposta do POST /casa/convites/verificar). tentar: o que o "Tentar de novo" faz se o corpo não servir.
  function preencher(caixa, corpo, { tentar = null } = {}) {
    const p = partes(caixa);
    const tipo = tipoDa(caixa);
    limparErro(p);
    p.marcar.checked = false;
    const versao = corpo?.versaoVigente ?? corpo?.versao;
    if (!corpo || !versao || !corpo.titulo || !corpo.texto) {
      p.titulo.textContent = TITULO_RESERVA[tipo] || TITULO_RESERVA.geral;
      p.texto.replaceChildren();
      p.marcar.disabled = true;
      delete caixa.dataset.versao;
      mostrarErro(p, MSG_CARREGAR, tentar ? { comTentar: tentar } : {});
      caixa.removeAttribute('aria-busy');
      return null;
    }

    p.titulo.textContent = corpo.titulo;
    p.texto.replaceChildren(...paragrafos(corpo.texto));
    caixa.dataset.versao = String(versao);
    p.marcar.disabled = false;
    caixa.removeAttribute('aria-busy');
    caixa.dispatchEvent(new CustomEvent('aceite:carregado', { detail: corpo }));
    return corpo;
  }

  // Confere antes de enviar. Devolve a versão marcada, ou null (e mostra o
  // erro e leva o foco à caixa). mensagem: o erro da caixa desmarcada, quando
  // a tela tem outra saída além de marcar (as alergias: desmarcar os chips).
  function versaoMarcada(caixa, { mensagem = MSG_SEM_MARCAR } = {}) {
    const p = partes(caixa);
    if (!caixa.dataset.versao) {
      mostrarErro(p, MSG_CARREGAR, { comTentar: () => carregar(caixa, { logado: caixa.dataset.logado === 'true' }) });
      p.marcar.focus();
      return null;
    }
    if (!p.marcar.checked) {
      caixa.setAttribute('aria-invalid', 'true');
      mostrarErro(p, mensagem);
      p.marcar.focus();
      return null;
    }
    limparErro(p);
    return Number(caixa.dataset.versao);
  }

  // O texto mudou entre abrir a tela e enviar (409): recarrega, desmarca e diz por quê.
  async function textoMudou(caixa, mensagem) {
    const logado = caixa.dataset.logado === 'true';
    await carregar(caixa, { logado });
    const p = partes(caixa);
    caixa.setAttribute('aria-invalid', 'true');
    mostrarErro(p, mensagem || 'O texto da autorização mudou. Leia de novo e marque para continuar.');
    p.marcar.focus();
  }

  function iniciar(caixa) {
    const p = partes(caixa);
    p.marcar.addEventListener('change', () => {
      if (p.marcar.checked) limparErro(p);
    });
  }

  return { carregar, preencher, versaoMarcada, textoMudou, iniciar };
})();
