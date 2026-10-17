// Ronu — Modo de cor (Dojo Noturno escuro / Washi claro)
//
// Carregado no <head>, sem defer e antes do CSS da tela: o data-tema precisa
// estar no <html> antes da primeira pintura, senão a página pisca no modo
// errado. Por isso o script é pequeno e não depende de nada.
//
// Três escolhas: "escuro", "claro" ou "aparelho" (padrão). Só as duas
// primeiras são gravadas no localStorage; "aparelho" apaga a chave e volta a
// seguir o prefers-color-scheme, inclusive quando o aparelho troca sozinho
// (modo automático ao anoitecer, por exemplo).
//
// Quem precisa reagir à troca (o botão oficial do Google, que é desenhado
// num tema fixo) ouve o evento "ronu:tema" no document.
(function () {
  var CHAVE = 'ronu-tema';
  var raiz = document.documentElement;
  var midiaClaro = window.matchMedia ? window.matchMedia('(prefers-color-scheme: light)') : null;

  // O localStorage pode lançar (aba anônima, dados bloqueados): nesse caso
  // a página só segue o aparelho, sem lembrar a escolha.
  function lerEscolha() {
    try {
      var valor = window.localStorage.getItem(CHAVE);
      return valor === 'escuro' || valor === 'claro' ? valor : 'aparelho';
    } catch (erro) {
      return 'aparelho';
    }
  }

  function gravarEscolha(escolha) {
    try {
      if (escolha === 'aparelho') window.localStorage.removeItem(CHAVE);
      else window.localStorage.setItem(CHAVE, escolha);
    } catch (erro) {
      // Sem armazenamento a escolha vale só até fechar a página.
    }
  }

  function resolver(escolha) {
    if (escolha === 'escuro' || escolha === 'claro') return escolha;
    return midiaClaro && midiaClaro.matches ? 'claro' : 'escuro';
  }

  var escolhaAtual = lerEscolha();

  function aplicar() {
    var modo = resolver(escolhaAtual);
    if (modo === 'claro') raiz.setAttribute('data-tema', 'claro');
    else raiz.removeAttribute('data-tema');
    atualizarBotoes(modo);
    return modo;
  }

  // Botões sol/lua ([data-alternar-tema]): o rótulo diz a ação, não o estado,
  // e o ícone mostra o modo para onde o clique leva.
  function atualizarBotoes(modo) {
    var botoes = document.querySelectorAll('[data-alternar-tema]');
    for (var i = 0; i < botoes.length; i++) {
      botoes[i].setAttribute('aria-label', modo === 'claro' ? 'Mudar para o modo escuro' : 'Mudar para o modo claro');
      botoes[i].setAttribute('data-modo', modo);
    }
    var opcoes = document.querySelectorAll('[data-escolha-tema]');
    for (var j = 0; j < opcoes.length; j++) {
      var marcado = opcoes[j].getAttribute('data-escolha-tema') === escolhaAtual;
      opcoes[j].setAttribute('aria-checked', marcado ? 'true' : 'false');
      opcoes[j].tabIndex = marcado ? 0 : -1;
    }
  }

  function avisar(modo) {
    var evento;
    try {
      evento = new CustomEvent('ronu:tema', { detail: { modo: modo, escolha: escolhaAtual } });
    } catch (erro) {
      evento = document.createEvent('CustomEvent');
      evento.initCustomEvent('ronu:tema', false, false, { modo: modo, escolha: escolhaAtual });
    }
    document.dispatchEvent(evento);
  }

  // A troca feita pelo usuário cruza as cores com uma View Transition, quando
  // o navegador tem. Sem ela (ou com movimento reduzido), troca seco.
  function trocar(escolha) {
    var antes = resolver(escolhaAtual);
    escolhaAtual = escolha;
    gravarEscolha(escolha);
    var depois = resolver(escolha);

    var reduzido = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (antes !== depois && document.startViewTransition && !reduzido) {
      document.startViewTransition(aplicar);
    } else {
      aplicar();
    }
    avisar(depois);
  }

  aplicar();

  if (midiaClaro) {
    var aoMudarAparelho = function () {
      if (escolhaAtual !== 'aparelho') return;
      avisar(aplicar());
    };
    if (midiaClaro.addEventListener) midiaClaro.addEventListener('change', aoMudarAparelho);
    else if (midiaClaro.addListener) midiaClaro.addListener(aoMudarAparelho);
  }

  // Delegação no document: funciona para botões que entram depois na página.
  document.addEventListener('click', function (evento) {
    var alvo = evento.target.closest ? evento.target.closest('[data-alternar-tema], [data-escolha-tema]') : null;
    if (!alvo) return;
    if (alvo.hasAttribute('data-alternar-tema')) {
      trocar(resolver(escolhaAtual) === 'claro' ? 'escuro' : 'claro');
    } else {
      trocar(alvo.getAttribute('data-escolha-tema'));
    }
  });

  // Setas do teclado no grupo de opções (padrão de radiogroup).
  document.addEventListener('keydown', function (evento) {
    var alvo = evento.target;
    if (!alvo.hasAttribute || !alvo.hasAttribute('data-escolha-tema')) return;
    var passo = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[evento.key];
    if (!passo) return;
    var grupo = Array.prototype.slice.call(alvo.closest('[role="radiogroup"]').querySelectorAll('[data-escolha-tema]'));
    var proximo = grupo[(grupo.indexOf(alvo) + passo + grupo.length) % grupo.length];
    evento.preventDefault();
    proximo.focus();
    trocar(proximo.getAttribute('data-escolha-tema'));
  });

  document.addEventListener('DOMContentLoaded', function () {
    atualizarBotoes(resolver(escolhaAtual));
  });

  window.RonuTema = {
    escolha: function () { return escolhaAtual; },
    modo: function () { return resolver(escolhaAtual); },
    definir: trocar
  };
})();
