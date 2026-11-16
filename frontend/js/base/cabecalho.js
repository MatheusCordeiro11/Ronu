// Ronu — Cabeçalho e abas das telas logadas (Dieta, Progresso, Treino,
// Alimentação, Casa, Perfil)
//
// Um lugar só para o cabeçalho que as telas repetem: logo, nav com a página
// atual marcada, sol/lua e avatar com as iniciais; no celular, as abas com
// ícone presas no pé da tela (decisão de 2026-10-16).
//
// Plano família (etapa 7): a aba Casa entra entre Alimentação e Perfil, com o
// contador de pedidos abertos (só o dono recebe a contagem da API). No celular
// são 6 abas, e "Alimentação" vira "Comida" só nelas (decisão S1): com 6
// colunas em 360 px, a palavra inteira não cabe no rótulo de 11 px.
//
// Uso: <header class="cabecalho" data-cabecalho="dieta"></header> e, no <body>,
// a classe .com-abas (espaço para as abas no celular). Carregar depois do
// auth.js (usa ronuUsuarioLogado e ronuIniciais). A tela Casa já lê o GET /casa
// e passa a contagem por RonuCabecalho.pedidos(n), com data-casa-contador="pagina"
// no <header> para o cabeçalho não pedir de novo.
(function () {
  var PAGINAS = [
    { chave: 'dieta', rotulo: 'Dieta', href: 'minha-dieta.html',
      icone: '<path d="M4 11h16M5 11a7 7 0 0 0 14 0M12 4v3"/>' },
    { chave: 'progresso', rotulo: 'Progresso', href: 'progresso.html',
      icone: '<path d="M4 18l5-6 4 3 7-9"/>' },
    { chave: 'treino', rotulo: 'Treino', href: 'treino.html',
      icone: '<path d="M6 8v8M18 8v8M3 10v4M21 10v4M6 12h12"/>' },
    { chave: 'alimentacao', rotulo: 'Alimentação', rotuloAba: 'Comida', href: 'alimentacao.html',
      icone: '<path d="M7 3v7M5 3v4a2 2 0 0 0 4 0V3M7 10v11M17 21V3c-2.2 1.4-3 4-3 7v3h3"/>' },
    { chave: 'casa', rotulo: 'Casa', href: 'casa.html',
      icone: '<path d="M4 11.5 12 4.5l8 7"/><path d="M6.5 10v10h11V10"/><path d="M10 20v-5h4v5"/>' },
    { chave: 'perfil', rotulo: 'Perfil', href: 'perfil.html',
      icone: '<circle cx="12" cy="8" r="4"/><path d="M4 21c1.5-4 4.5-6 8-6s6.5 2 8 6"/>' }
  ];

  var ICONE_SOL = '<svg class="icone-sol" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2.5v2M12 19.5v2M4.6 4.6l1.4 1.4M18 18l1.4 1.4M2.5 12h2M19.5 12h2M4.6 19.4L6 18M18 6l1.4-1.4"/></svg>';
  var ICONE_LUA = '<svg class="icone-lua" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round" aria-hidden="true"><path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z"/></svg>';

  function montar() {
    var cabecalho = document.querySelector('[data-cabecalho]');
    if (!cabecalho) return;
    var atual = cabecalho.getAttribute('data-cabecalho');
    var usuario = typeof ronuUsuarioLogado === 'function' ? ronuUsuarioLogado() : null;
    var nome = (usuario && usuario.nome) || '';
    var iniciais = nome && typeof ronuIniciais === 'function' ? ronuIniciais(nome) : '';

    var links = PAGINAS.map(function (p) {
      return '<a href="' + p.href + '" data-pagina="' + p.chave + '"' + (p.chave === atual ? ' aria-current="page"' : '') + '>' +
        '<span class="nav-rotulo">' + p.rotulo + '</span></a>';
    }).join('');

    cabecalho.innerHTML =
      '<div class="conteiner"><div class="cabecalho-linha">' +
        '<div class="cabecalho-esquerda">' +
          '<a class="logo" href="minha-dieta.html" aria-label="Ronu, sua dieta">' +
            '<svg class="logo-marca" viewBox="0 0 40 40" aria-hidden="true" focusable="false"><defs><clipPath id="logo-recorte"><path d="M2.5 20H37.5C37.5 30.2 29.7 37.5 20 37.5C10.3 37.5 2.5 30.2 2.5 20Z"/></clipPath></defs><path d="M6.2 20.2C5.6 15.6 8.6 12.6 12.2 13.2C13.2 9.4 17.2 7.6 20 8.8C22.8 7.6 26.8 9.4 27.8 13.2C31.4 12.6 34.4 15.6 33.8 20.2Z" fill="var(--logo-arroz)" stroke="var(--texto)" stroke-width="2" stroke-linejoin="round"/><path d="M2.5 20H37.5C37.5 30.2 29.7 37.5 20 37.5C10.3 37.5 2.5 30.2 2.5 20Z" fill="var(--acento)"/><g clip-path="url(#logo-recorte)"><rect x="0" y="24.2" width="40" height="5" fill="var(--logo-faixa)"/><path d="M18 30 L13.6 37.6 L17 38.4 L19.8 30.6Z M22 30 L26.4 37.6 L23 38.4 L20.2 30.6Z" fill="var(--logo-faixa)"/></g><rect x="16.6" y="22.6" width="6.8" height="8.2" rx="1.6" fill="var(--logo-faixa)"/></svg>' +
            '<span class="logo-nome" aria-hidden="true">ronu</span>' +
          '</a>' +
          '<nav class="nav-principal" aria-label="Principal">' + links + '</nav>' +
        '</div>' +
        '<div class="cabecalho-acoes">' +
          '<button type="button" class="botao-tema" data-alternar-tema aria-label="Mudar para o modo claro">' + ICONE_SOL + ICONE_LUA + '</button>' +
          '<a class="avatar" href="perfil.html"></a>' +
        '</div>' +
      '</div></div>';

    var avatar = cabecalho.querySelector('.avatar');
    avatar.textContent = iniciais;
    avatar.setAttribute('aria-label', nome ? 'Seu perfil, ' + nome : 'Seu perfil');
    if (atual === 'perfil') avatar.setAttribute('aria-current', 'page');
    // O sol/lua nasceu depois do tema.js: pede para ele acertar ícone e rótulo.
    if (window.RonuTema && window.RonuTema.atualizar) window.RonuTema.atualizar();

    // Abas do celular: um <nav> próprio no fim do <body> (o CSS só mostra
    // abaixo de 720 px; a nav do topo some nessa largura).
    var abas = document.createElement('nav');
    abas.className = 'abas';
    abas.setAttribute('aria-label', 'Principal');
    abas.innerHTML = PAGINAS.map(function (p) {
      return '<a href="' + p.href + '" data-pagina="' + p.chave + '"' + (p.chave === atual ? ' aria-current="page"' : '') + '>' +
        '<span class="abas-icone"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + p.icone + '</svg></span>' +
        '<span class="nav-rotulo">' + (p.rotuloAba || p.rotulo) + '</span></a>';
    }).join('');
    document.body.appendChild(abas);

    if (usuario && cabecalho.getAttribute('data-casa-contador') !== 'pagina') buscarPedidos();
  }

  // O contador vai no link da Casa da nav do topo e da aba do celular. O número
  // é texto de verdade (aria-hidden só na pílula), e o link ganha "Casa, 2 pedidos".
  function pedidos(n) {
    var quantos = Number(n) || 0;
    var links = document.querySelectorAll('a[data-pagina="casa"]');
    for (var i = 0; i < links.length; i++) {
      var link = links[i];
      var antigo = link.querySelector('.nav-contador');
      if (antigo) antigo.remove();
      if (quantos > 0) {
        var pilula = document.createElement('span');
        pilula.className = 'nav-contador num';
        pilula.setAttribute('aria-hidden', 'true');
        pilula.textContent = quantos > 9 ? '9+' : String(quantos);
        // Na aba do celular a pílula fica sobre o ícone; na nav do topo, depois do nome.
        var icone = link.querySelector('.abas-icone');
        (icone || link).appendChild(pilula);
        link.setAttribute('aria-label', 'Casa, ' + quantos + (quantos === 1 ? ' pedido' : ' pedidos'));
      } else {
        link.removeAttribute('aria-label');
      }
    }
  }

  // Só o dono recebe pedidosAbertos (o membro vê nulo, de propósito). Sem casa
  // (404), sem o aceite da casa (409) ou sem rede, o contador só não aparece: o
  // cabeçalho nunca redireciona nem mostra erro, então usa o fetch direto, e não
  // o ronuFetchAutenticado (que mandaria ao login ou ao consentimento).
  function buscarPedidos() {
    if (typeof RONU_CONFIG === 'undefined' || typeof RONU_TOKEN_KEY === 'undefined') return;
    var token;
    try { token = localStorage.getItem(RONU_TOKEN_KEY); } catch (erro) { return; }
    if (!token) return;
    fetch(RONU_CONFIG.API_BASE + '/casa', { headers: { Authorization: 'Bearer ' + token } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (casa) { if (casa && casa.souDono) pedidos(casa.pedidosAbertos); })
      .catch(function () { /* sem contador */ });
  }

  window.RonuCabecalho = { pedidos: pedidos };

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', montar);
  else montar();
})();
