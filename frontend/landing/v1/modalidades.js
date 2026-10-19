// Ronu — Ilustração do topo da landing que troca entre as modalidades
//
// Sete ilustrações (as modalidades do catálogo, GET /api/modalidades) ficam
// empilhadas; a atual tem .lp-ilustracao-atual, e o CSS faz a troca só com
// opacity e transform. A cada ~3,2 s passa para a próxima.
//
// Conteúdo que se move sozinho (WCAG 2.2.2): a troca para enquanto o mouse
// está em cima da ilustração, enquanto algum ponto tem foco e com a aba
// escondida. Com prefers-reduced-motion não troca sozinha (fica o quimono),
// mas os pontos continuam funcionando.
//
// Separado do landing.js de propósito: aquele só roda sem movimento
// reduzido, e os pontos precisam funcionar nos dois casos.
(function () {
  var INTERVALO_MS = 3200;
  var NOMES = ['Jiu-jitsu', 'Muay thai', 'Boxe', 'Musculação', 'Futebol', 'Basquete', 'Natação'];

  function iniciar() {
    var area = document.getElementById('lp-modalidades');
    if (!area) return;
    var ilustracoes = Array.prototype.slice.call(area.querySelectorAll('.lp-ilustracao'));
    var pontos = Array.prototype.slice.call(area.querySelectorAll('.lp-ponto'));
    var nome = document.getElementById('lp-modalidade-nome');
    var grupoPontos = area.querySelector('.lp-pontos');
    var reduzido = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    var atual = 0;
    var mouseEmCima = false;
    var focoNosPontos = false;
    var temporizador = null;

    function mostrar(indice) {
      atual = (indice + ilustracoes.length) % ilustracoes.length;
      ilustracoes.forEach(function (svg, i) {
        var esta = i === atual;
        svg.classList.toggle('lp-ilustracao-atual', esta);
        // Só a ilustração à vista existe para o leitor de tela.
        if (esta) svg.removeAttribute('aria-hidden');
        else svg.setAttribute('aria-hidden', 'true');
      });
      pontos.forEach(function (ponto, i) {
        ponto.setAttribute('aria-pressed', String(i === atual));
      });
      nome.textContent = NOMES[atual];
    }

    function parada() {
      return reduzido || mouseEmCima || focoNosPontos || document.hidden;
    }

    // Um temporizador por vez, sempre recomeçado do zero: depois de um clique
    // ou de uma pausa, a próxima troca espera o intervalo inteiro.
    function agendar() {
      clearTimeout(temporizador);
      temporizador = null;
      if (parada()) return;
      temporizador = setTimeout(function () {
        mostrar(atual + 1);
        agendar();
      }, INTERVALO_MS);
    }

    pontos.forEach(function (ponto, i) {
      ponto.addEventListener('click', function () {
        mostrar(i);
        agendar();
      });
    });

    area.addEventListener('pointerenter', function (evento) {
      if (evento.pointerType !== 'mouse') return;
      mouseEmCima = true;
      agendar();
    });
    area.addEventListener('pointerleave', function (evento) {
      if (evento.pointerType !== 'mouse') return;
      mouseEmCima = false;
      agendar();
    });
    grupoPontos.addEventListener('focusin', function () {
      focoNosPontos = true;
      agendar();
    });
    grupoPontos.addEventListener('focusout', function (evento) {
      if (grupoPontos.contains(evento.relatedTarget)) return;
      focoNosPontos = false;
      agendar();
    });
    document.addEventListener('visibilitychange', agendar);

    mostrar(0);
    agendar();
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
