// Ronu — Chips de alergia (TACO 2c), usados na Alimentação e no passo 4 do onboarding
//
// Só o desenho e os textos comuns: cada tela decide quando gravar (a Alimentação grava a cada toque; o
// onboarding, no Continuar). Os códigos são os da API (CodigosAlergia.cs), na mesma ordem: Amendoim e
// Castanhas ficam lado a lado porque um tira o outro (RegrasAlergias), e a tela mostra isso no estado
// "coberto". O servidor é a verdade: um chip só aparece cheio quando a API confirmou.

window.RonuAlergias = (function () {
  const CHIPS = [
    ['leite', 'Leite e derivados'],
    ['gluten', 'Glúten'],
    ['ovo', 'Ovo'],
    ['amendoim', 'Amendoim'],
    ['castanhas', 'Castanhas'],
    ['soja', 'Soja'],
    ['peixe', 'Peixe'],
    ['crustaceos', 'Crustáceos e frutos do mar']
  ];
  const ROTULOS = Object.fromEntries(CHIPS);
  const CODIGOS = CHIPS.map(([codigo]) => codigo);
  // A regra cruzada, do lado da tela: só para desenhar o "sai com". Quem tira de verdade é a API.
  const PAR = { amendoim: 'castanhas', castanhas: 'amendoim' };

  // Visto desenhado (não glifo), no traço dos outros ícones.
  const VISTO = '<svg class="alg-visto" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M2.5 6.3 5 8.6l4.5-5"/></svg>';

  const rotulo = (codigo) => ROTULOS[codigo] || codigo;
  const ordenar = (codigos) => CODIGOS.filter((c) => codigos.has ? codigos.has(c) : codigos.includes(c));

  function juntar(nomes) {
    return nomes.length <= 1 ? (nomes[0] || '') : `${nomes.slice(0, -1).join(', ')} e ${nomes[nomes.length - 1]}`;
  }

  // "13/11/2026": a data do aceite, na hora local de quem vê.
  function data(iso) {
    const d = new Date(iso);
    return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' });
  }

  // Os 8 botões, uma vez só; depois, pintar() muda o estado de cada um. cruzadaId: a frase da regra
  // cruzada, citada no aria-describedby só de Amendoim e Castanhas.
  function criar(grupo, { aoTocar, cruzadaId }) {
    const botoes = new Map();
    grupo.replaceChildren(...CHIPS.map(([codigo, nome]) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'chip alg-chip';
      b.dataset.codigo = codigo;
      b.setAttribute('aria-pressed', 'false');
      b.innerHTML = VISTO;
      const texto = document.createElement('span');
      texto.textContent = nome;
      const com = document.createElement('span');
      com.className = 'alg-chip-com';
      com.hidden = true;
      b.append(texto, com);
      if (PAR[codigo] && cruzadaId) b.dataset.cruzada = cruzadaId;
      b.addEventListener('click', () => aoTocar(codigo, b));
      botoes.set(codigo, b);
      return b;
    }));
    return botoes;
  }

  // estado: { marcados, pendentes, salvando } (Sets de códigos) e desabilitado. pendenteId: a frase
  // "Falta autorizar", ligada aos chips pendentes.
  function pintar(botoes, { marcados, pendentes = new Set(), salvando = new Set(), desabilitado = false, pendenteId = null }) {
    const ligado = (c) => marcados.has(c) || pendentes.has(c) || salvando.has(c);
    botoes.forEach((b, codigo) => {
      const pressionado = ligado(codigo);
      b.setAttribute('aria-pressed', String(pressionado));
      b.disabled = desabilitado;
      if (salvando.has(codigo)) b.setAttribute('aria-busy', 'true');
      else b.removeAttribute('aria-busy');
      if (pendentes.has(codigo) && !marcados.has(codigo)) b.dataset.estado = 'pendente';
      else delete b.dataset.estado;

      // Coberto: o par já está ligado, então este também sai do cardápio. Continua clicável (D3).
      const par = PAR[codigo];
      const com = b.querySelector('.alg-chip-com');
      if (par && !pressionado && ligado(par)) {
        b.dataset.coberto = '';
        com.textContent = `sai com ${rotulo(par)}`;
        com.hidden = false;
      } else {
        delete b.dataset.coberto;
        com.textContent = '';
        com.hidden = true;
      }

      const descricoes = [b.dataset.cruzada, b.dataset.estado === 'pendente' ? pendenteId : null].filter(Boolean);
      if (descricoes.length) b.setAttribute('aria-describedby', descricoes.join(' '));
      else b.removeAttribute('aria-describedby');
    });
  }

  function fraseFaltaAutorizar(pendentes) {
    const nomes = ordenar(pendentes).map(rotulo);
    return nomes.length === 1
      ? `Falta autorizar: ${nomes[0]} só fica salvo depois que você marcar a caixa.`
      : `Falta autorizar: ${juntar(nomes)} só ficam salvos depois que você marcar a caixa.`;
  }

  // A linha que fica no lugar da caixa depois da autorização (texto, sem dado da pessoa além da data).
  function preencherAutorizado(el, aceitoEm) {
    const quando = data(aceitoEm);
    const a = document.createElement('span');
    a.textContent = quando ? `Uso autorizado em ${quando}.` : 'Uso autorizado.';
    const b = document.createElement('span');
    b.textContent = 'Desmarque todas e o Ronu apaga suas alergias.';
    el.replaceChildren(a, b);
  }

  // Lê o corpo de uma resposta de PUT/DELETE/GET /perfil/alergias, ou o erro com motivo e mensagem.
  async function ler(resposta) {
    const corpo = await resposta.json().catch(() => null);
    return { ok: resposta.ok, status: resposta.status, corpo };
  }

  const ehFaltaDeAceite = (r) => r.status === 409 && r.corpo?.motivo === 'consentimento_alergias';

  // Movimento reduzido: a sugestão "Marcar alergia" rola sem animação.
  const semMovimento = () => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

  return { CHIPS, CODIGOS, rotulo, ordenar, juntar, data, criar, pintar, fraseFaltaAutorizar, preencherAutorizado, ler, ehFaltaDeAceite, semMovimento };
})();
