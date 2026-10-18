// Ronu — Cabeçalho e abas das telas logadas (Dieta, Progresso, Treino,
// Alimentação, Perfil)
//
// Um lugar só para o cabeçalho que as cinco telas repetem: logo, nav com a
// página atual marcada, sol/lua e avatar com as iniciais; no celular, as cinco
// abas com ícone presas no pé da tela (decisão de 2026-10-16).
//
// Uso: <header class="cabecalho" data-cabecalho="dieta"></header> e, no <body>,
// a classe .com-abas (espaço para as abas no celular). Carregar depois do
// auth.js (usa ronuUsuarioLogado e ronuIniciais).
(function () {
  var PAGINAS = [
    { chave: 'dieta', rotulo: 'Dieta', href: 'dashboard.html',
      icone: '<path d="M4 11h16M5 11a7 7 0 0 0 14 0M12 4v3"/>' },
    { chave: 'progresso', rotulo: 'Progresso', href: 'progresso.html',
      icone: '<path d="M4 18l5-6 4 3 7-9"/>' },
    { chave: 'treino', rotulo: 'Treino', href: 'treino.html',
      icone: '<path d="M6 8v8M18 8v8M3 10v4M21 10v4M6 12h12"/>' },
    { chave: 'alimentacao', rotulo: 'Alimentação', href: 'alimentacao.html',
      icone: '<path d="M7 3v7M5 3v4a2 2 0 0 0 4 0V3M7 10v11M17 21V3c-2.2 1.4-3 4-3 7v3h3"/>' },
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
      return '<a href="' + p.href + '"' + (p.chave === atual ? ' aria-current="page"' : '') + '>' + p.rotulo + '</a>';
    }).join('');

    cabecalho.innerHTML =
      '<div class="conteiner cabecalho-linha">' +
        '<div class="cabecalho-esquerda">' +
          '<a class="logo" href="dashboard.html" aria-label="Ronu, sua dieta">' +
            '<span class="logo-selo" aria-hidden="true">浪</span>' +
            '<span class="logo-nome" aria-hidden="true">RONU</span>' +
          '</a>' +
          '<nav class="nav-principal" aria-label="Principal">' + links + '</nav>' +
        '</div>' +
        '<div class="cabecalho-acoes">' +
          '<button type="button" class="botao-tema" data-alternar-tema aria-label="Mudar para o modo claro">' + ICONE_SOL + ICONE_LUA + '</button>' +
          '<a class="avatar" href="perfil.html"></a>' +
        '</div>' +
      '</div>';

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
      return '<a href="' + p.href + '"' + (p.chave === atual ? ' aria-current="page"' : '') + '>' +
        '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + p.icone + '</svg>' +
        p.rotulo + '</a>';
    }).join('');
    document.body.appendChild(abas);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', montar);
  else montar();
})();
