// Ronu — Gráficos que se desenham
//
// Decisão de 2026-10-16: os gráficos de dados se desenham ao carregar, de novo
// quando voltam a entrar na tela pela rolagem e quando o mouse passa por cima.
// Fora isso ficam parados: movimento contínuo é só do enfeite (grade, quimono,
// carimbo), que é CSS puro.
//
// Como marcar: o contêiner do gráfico ganha [data-anima-grupo] e cada parte
// animada ganha [data-anima="linha" | "barra-h" | "barra-v" | "anel" |
// "surge" | "pontos"]. Este script só liga e desliga a classe .em-cena no
// grupo; a animação em si fica no CSS (componentes.css).
//
// Sem este script (ou com movimento reduzido) os gráficos aparecem no estado
// final, porque o estado "antes de desenhar" só existe com .anima no <html>.
(function () {
  var reduzido = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (reduzido || !('IntersectionObserver' in window)) return;

  document.documentElement.classList.add('anima');

  var observador = new IntersectionObserver(function (entradas) {
    entradas.forEach(function (entrada) {
      // Sai de vez da tela: volta ao começo, para desenhar de novo ao voltar.
      if (entrada.isIntersecting && entrada.intersectionRatio >= 0.25) {
        entrada.target.classList.add('em-cena');
      } else if (!entrada.isIntersecting) {
        entrada.target.classList.remove('em-cena');
      }
    });
  }, { threshold: [0, 0.25] });

  // Repetir no hover: tira a classe, força o navegador a recalcular o estilo
  // e devolve a classe, o que reinicia as animações do grupo.
  function redesenhar(grupo) {
    if (grupo._ronuDesenhandoAte && Date.now() < grupo._ronuDesenhandoAte) return;
    grupo._ronuDesenhandoAte = Date.now() + 900;
    grupo.classList.remove('em-cena');
    void grupo.offsetWidth;
    grupo.classList.add('em-cena');
  }

  function registrar(grupo) {
    if (grupo._ronuAnima) return;
    grupo._ronuAnima = true;
    observador.observe(grupo);
    var alvoHover = grupo.closest('[data-anima-hover]') || grupo;
    alvoHover.addEventListener('pointerenter', function (evento) {
      if (evento.pointerType === 'mouse') redesenhar(grupo);
    });
  }

  function procurar(raiz) {
    var grupos = (raiz || document).querySelectorAll('[data-anima-grupo]');
    for (var i = 0; i < grupos.length; i++) registrar(grupos[i]);
  }

  // As telas montam os gráficos depois de buscar os dados na API: um
  // MutationObserver registra sozinho os grupos que entram depois, para
  // nenhuma tela precisar lembrar de chamar nada.
  document.addEventListener('DOMContentLoaded', function () {
    procurar();
    new MutationObserver(function (mudancas) {
      for (var i = 0; i < mudancas.length; i++) {
        var nos = mudancas[i].addedNodes;
        for (var j = 0; j < nos.length; j++) {
          if (nos[j].nodeType !== 1) continue;
          if (nos[j].hasAttribute('data-anima-grupo')) registrar(nos[j]);
          procurar(nos[j]);
        }
      }
    }).observe(document.body, { childList: true, subtree: true });
  });

  window.RonuMovimento = { procurar: procurar, redesenhar: redesenhar };
})();
