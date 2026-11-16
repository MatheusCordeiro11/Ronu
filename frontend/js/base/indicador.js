// Ronu — Indicador deslizante dos segmentados e da nav (redesign de nov/2026)
//
// Uma pílula desliza até a opção escolhida de cada .segmentado e até o item da
// página atual na .nav-principal; na nav, ela também segue o mouse e volta ao
// item atual quando o mouse sai. É só enfeite: sem este script, o CSS já marca
// a escolha no próprio botão (a classe .com-ind só entra aqui).
//
// A pílula é o ::before do próprio grupo, posicionado por variáveis CSS
// (--ind-x, --ind-y, --ind-w, --ind-h). Assim ela não é um filho do grupo: o JS
// das telas que percorre ou troca os botões (Casa, onboarding) não a vê.
//
// Segmentados criados depois (pelo JS das telas ou de uma folha) são achados
// por um MutationObserver. Com movimento reduzido, a pílula pula sem deslizar
// (o base.css zera as transições).

(function () {
  'use strict';

  var SELETOR = '.segmentado, .nav-principal';
  var prontos = new WeakSet();

  function escolhido(grupo) {
    if (grupo.classList.contains('nav-principal')) {
      return grupo.querySelector('a[aria-current="page"]');
    }
    return grupo.querySelector(':scope > [aria-checked="true"], :scope > [aria-selected="true"]');
  }

  function mover(grupo, alvo, semAnimar) {
    if (!alvo || !alvo.offsetWidth) {
      grupo.classList.remove('ind-visivel');
      return;
    }
    if (semAnimar) grupo.classList.add('ind-sem-animar');
    grupo.style.setProperty('--ind-x', alvo.offsetLeft + 'px');
    grupo.style.setProperty('--ind-y', alvo.offsetTop + 'px');
    grupo.style.setProperty('--ind-w', alvo.offsetWidth + 'px');
    grupo.style.setProperty('--ind-h', alvo.offsetHeight + 'px');
    grupo.classList.add('ind-visivel');
    if (semAnimar) {
      void grupo.offsetWidth;
      grupo.classList.remove('ind-sem-animar');
    }
  }

  function preparar(grupo) {
    if (prontos.has(grupo)) return;
    prontos.add(grupo);
    grupo.classList.add('com-ind');
    var primeira = true;
    function atualizar() {
      mover(grupo, escolhido(grupo), primeira);
      primeira = false;
    }
    atualizar();

    // A escolha mudou (clique, teclado ou o JS da tela, que pode trocar os botões).
    new MutationObserver(function () {
      mover(grupo, escolhido(grupo));
    }).observe(grupo, { subtree: true, childList: true, attributes: true, attributeFilter: ['aria-checked', 'aria-selected', 'aria-current', 'hidden'] });

    // Ficou visível, mudou de largura ou a fonte carregou.
    if ('ResizeObserver' in window) {
      new ResizeObserver(function () {
        mover(grupo, escolhido(grupo), true);
      }).observe(grupo);
    }

    if (grupo.classList.contains('nav-principal')) {
      grupo.addEventListener('mouseover', function (e) {
        var link = e.target.closest('a');
        if (link && grupo.contains(link)) mover(grupo, link);
      });
      grupo.addEventListener('mouseleave', function () { mover(grupo, escolhido(grupo)); });
    }
  }

  function procurar(raiz) {
    if (raiz.matches && raiz.matches(SELETOR)) preparar(raiz);
    if (raiz.querySelectorAll) raiz.querySelectorAll(SELETOR).forEach(preparar);
  }

  function iniciar() {
    procurar(document);
    new MutationObserver(function (mudancas) {
      mudancas.forEach(function (m) {
        m.addedNodes.forEach(function (no) {
          if (no.nodeType === 1) procurar(no);
        });
      });
    }).observe(document.body, { childList: true, subtree: true });
    if (document.fonts && document.fonts.ready) {
      document.fonts.ready.then(function () {
        document.querySelectorAll(SELETOR).forEach(function (g) { mover(g, escolhido(g), true); });
      });
    }
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
