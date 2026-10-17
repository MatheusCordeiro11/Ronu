// Ronu — Landing com rolagem guiada
//
// A rolagem de cada seção vira um alvo de 0 a 1; um laço por quadro
// (requestAnimationFrame) persegue esse alvo com suavização, para os
// "degraus" da roda do mouse virarem um movimento contínuo. O valor vai para
// a variável --p da seção, e o landing.css calcula a cena a partir dela.
// Este script só escreve --p, a etiqueta de cada cena e os números que contam.
// O laço para sozinho quando todas as cenas assentam.
//
// A classe .rolagem-guiada é posta no <html> por um script curto no <head>
// (antes da primeira pintura), e só sem prefers-reduced-motion. Sem ela, nada
// aqui roda e as seções ficam no estado final (--p: 1 no CSS).
//
// Números de exemplo pela fórmula real (CalculadoraManutencao, VersaoFormula 2),
// para homem de 30 anos, 80 kg e 1,78 m, jiu-jitsu (MET 7,8) por 1,5 h,
// perder peso — o mesmo perfil dos testes de SimulacaoMetaTests:
//   gasto parado 1.768 (TMB 1.767,5) + dia a dia 707 (TMB × 0,4)
//   + treino 816 ((7,8 − 1) × 80 × 1,5) − objetivo 440 (5,5 × 80)
//   = 2.851 no dia de treino e 2.035 no descanso; com ajuste de −3%, 2.765.
(function () {
  var raiz = document.documentElement;
  if (!raiz.classList.contains('rolagem-guiada')) return;

  var EXEMPLO = {
    repouso: 1768,
    diaADia: 707,
    treino: 816,
    objetivo: -440,
    metaTreino: 2851,
    metaDescanso: 2035,
    metaAjustada: 2765
  };

  var numero = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 });
  var fmt = function (v) { return numero.format(Math.round(v)); };
  var fase = function (p, a, b) { return Math.min(1, Math.max(0, (p - a) / (b - a))); };
  var relogio = function (p) { var s = Math.round(p * 15); return '00:' + (s < 10 ? '0' : '') + s; };

  var partesMeta = [
    ['gasto parado', EXEMPLO.repouso],
    ['dia a dia', EXEMPLO.diaADia],
    ['treino de jiu-jitsu', EXEMPLO.treino],
    ['objetivo: perder peso', EXEMPLO.objetivo]
  ];

  // O que cada cena escreve, a partir do p suavizado.
  var textos = {
    treino: function (p) {
      return {
        etiqueta: relogio(p) + ' · ' + (p < 0.3 ? 'modalidade' : p < 0.62 ? 'dias' : 'kcal da sessão'),
        treino: fmt(EXEMPLO.treino * fase(p, 0.7, 0.95))
      };
    },
    meta: function (p) {
      var t = { etiqueta: relogio(p) + ' · ' + partesMeta[Math.min(3, Math.floor(p * 4 * 0.999))][0] };
      var total = 0;
      partesMeta.forEach(function (parte, i) {
        var s = fase(p, 0.04 + i * 0.22, 0.2 + i * 0.22);
        var v = parte[1];
        total += v * s;
        t['meta-' + i] = (v < 0 ? '− ' : i ? '+ ' : '') + fmt(Math.abs(v) * s);
      });
      t['meta-total'] = fmt(total);
      return t;
    },
    semana: function (p) {
      var kcal = p < 0.4 ? EXEMPLO.metaTreino
        : p < 0.7 ? EXEMPLO.metaTreino + (EXEMPLO.metaDescanso - EXEMPLO.metaTreino) * fase(p, 0.4, 0.48)
        : EXEMPLO.metaDescanso + (EXEMPLO.metaTreino - EXEMPLO.metaDescanso) * fase(p, 0.7, 0.78);
      return {
        etiqueta: relogio(p) + ' · ' + (p < 0.4 ? 'semana gerada' : p < 0.7 ? 'dia de descanso' : 'nome regional'),
        semana: fmt(kcal),
        // Nome do Rio de Janeiro (regra 9 do prompt), na metade do destaque.
        regional: p < 0.84 ? 'Mandioca' : 'Aipim'
      };
    },
    balanca: function (p) {
      return {
        etiqueta: relogio(p) + ' · ' + (p < 0.34 ? 'pesagens' : p < 0.67 ? 'aderência' : 'meta ajustada'),
        balanca: fmt(EXEMPLO.metaTreino + (EXEMPLO.metaAjustada - EXEMPLO.metaTreino) * fase(p, 0.78, 0.95))
      };
    }
  };

  var topo = document.querySelector('.lp-topo');
  var secoes = Array.prototype.map.call(document.querySelectorAll('.lp-secao'), function (el) {
    var alvos = { etiqueta: el.querySelector('[data-etiqueta]') };
    Array.prototype.forEach.call(el.querySelectorAll('[data-contador]'), function (no) {
      alvos[no.getAttribute('data-contador')] = no;
    });
    return { el: el, palco: el.querySelector('.lp-palco'), textos: textos[el.getAttribute('data-secao')], alvos: alvos, alvo: 0, atual: 0 };
  });

  function medir() {
    var alturaTopo = topo ? topo.offsetHeight : 0;
    secoes.forEach(function (s) {
      var r = s.el.getBoundingClientRect();
      var curso = Math.max(1, s.el.offsetHeight - s.palco.offsetHeight);
      s.alvo = Math.min(1, Math.max(0, (alturaTopo - r.top) / curso));
    });
  }

  function aplicar(s) {
    s.el.style.setProperty('--p', s.atual.toFixed(4));
    var t = s.textos(s.atual);
    Object.keys(t).forEach(function (chave) {
      var no = s.alvos[chave];
      if (no && no.textContent !== t[chave]) no.textContent = t[chave];
    });
  }

  var rodando = false;
  var antes = 0;

  function quadro(agora) {
    // O horário que o rAF entrega pode ser anterior ao performance.now() do
    // acordar(): sem o piso em 0, o fator passaria de 1 e --p sairia de 0–1.
    var dt = Math.max(0, Math.min(64, agora - antes));
    antes = agora;
    var fator = 1 - Math.pow(0.86, dt / 16.7);
    var mexendo = false;

    secoes.forEach(function (s) {
      var d = s.alvo - s.atual;
      if (d === 0) return;
      s.atual = Math.abs(d) > 0.0004 ? Math.min(1, Math.max(0, s.atual + d * fator)) : s.alvo;
      if (s.atual !== s.alvo) mexendo = true;
      aplicar(s);
    });

    if (mexendo) {
      requestAnimationFrame(quadro);
    } else {
      rodando = false;
    }
  }

  function acordar() {
    medir();
    if (rodando) return;
    rodando = true;
    antes = performance.now();
    requestAnimationFrame(quadro);
  }

  // Primeiro estado sem suavização: a página pode abrir no meio (recarga,
  // link com #sec-meta), e a cena tem de estar certa já no primeiro quadro.
  medir();
  secoes.forEach(function (s) { s.atual = s.alvo; aplicar(s); });

  window.addEventListener('scroll', acordar, { passive: true });
  window.addEventListener('resize', acordar);
})();
