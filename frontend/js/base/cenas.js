// Ronu — Cenas do painel das telas de entrada (redesign de nov/2026)
//
// O painel à direita (.entrada-cena) alterna cenas: a órbita com os ícones 3D
// das modalidades, o dia com as refeições sendo marcadas e o gráfico de peso.
// É enfeite: o painel inteiro é aria-hidden e some no celular. Pausa quando o
// mouse está sobre ele e quando a aba não está visível. Com movimento reduzido,
// fica parado na primeira cena, com as refeições já marcadas.
//
// Os números são o exemplo de sempre, pela fórmula do app (homem de 80 kg,
// sessão de 1 h 30): jiu-jitsu e muay thai 816 kcal, boxe 696, musculação 300,
// natação 576; meta do dia de treino 2.851 kcal; tendência 79,5 kg.

(function () {
  'use strict';

  function iniciar() {
    var painel = document.querySelector('.entrada-cena');
    if (!painel) return;
    var cenas = painel.querySelectorAll('.cena');
    var pontos = painel.querySelectorAll('.cena-pontos i');
    var chips = painel.querySelectorAll('.cena-chip');
    var imgs = painel.querySelectorAll('.cena-centro img');
    var centro = painel.querySelector('.cena-centro');
    var linhas = painel.querySelectorAll('.cena-linha');
    var reduzido = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var atual = 0, mod = 0, marcadas = 0, tique = 0, pausa = false;

    function marcar() {
      linhas.forEach(function (l, i) { l.classList.toggle('feita', i < marcadas); });
    }

    function mostrarMod(i) {
      if (!chips.length) return;
      mod = i;
      chips.forEach(function (c, j) { c.classList.toggle('forte', j === i); });
      imgs.forEach(function (im, j) { im.classList.toggle('ativo', j === i); });
      if (centro && !reduzido) {
        centro.classList.remove('pulsa');
        void centro.offsetWidth;
        centro.classList.add('pulsa');
      }
    }

    var titulo = painel.querySelector('.cena-titulo');
    var texto = painel.querySelector('.cena-texto');

    // A frase da cena (login e cadastro): a parte depois da vírgula, ou a
    // última palavra, vai no acento, como no título das telas.
    function legenda(cena) {
      var t = cena.getAttribute('data-titulo');
      if (!t || !titulo) return;
      var corte = t.indexOf(',') >= 0 ? t.indexOf(',') + 1 : t.lastIndexOf(' ');
      var destaque = document.createElement('span');
      destaque.textContent = t.slice(corte).trim();
      titulo.replaceChildren(t.slice(0, corte) + ' ', destaque);
      if (texto) texto.textContent = cena.getAttribute('data-texto') || '';
    }

    function mostrarCena(i) {
      atual = i;
      cenas.forEach(function (c, j) { c.classList.toggle('ativa', j === i); });
      pontos.forEach(function (p, j) { p.classList.toggle('ativo', j === i); });
      if (cenas[i]) legenda(cenas[i]);
      marcadas = 0;
      marcar();
    }

    mostrarCena(0);
    mostrarMod(0);
    if (reduzido) {
      marcadas = linhas.length;
      marcar();
      return;
    }

    painel.addEventListener('mouseenter', function () { pausa = true; });
    painel.addEventListener('mouseleave', function () { pausa = false; });
    window.setInterval(function () {
      if (document.hidden || pausa) return;
      var cena = cenas[atual];
      if (cena && cena.querySelector('.cena-linha') && marcadas < linhas.length) {
        marcadas++;
        marcar();
      }
      if (cena && cena.querySelector('.cena-orbita') && tique % 2 === 1) mostrarMod((mod + 1) % chips.length);
      tique++;
      var duracao = cena && cena.querySelector('.cena-orbita') ? 11 : 6;
      if (tique >= duracao) {
        tique = 0;
        mostrarCena((atual + 1) % cenas.length);
      }
    }, 1000);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
