// Ronu — landing (redesign de nov/2026), copiada do protótipo aprovado.
(function () {
  var raiz = document.documentElement;
  var reduzido = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var fmt = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 });
  var fmt1 = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  var lim = function (v) { return Math.max(0, Math.min(1, v)); };
  var suave = function (t) { return 1 - Math.pow(1 - t, 3); };
  var $ = function (id) { return document.getElementById(id); };
  var CHECK = '<span class="check"><svg viewBox="0 0 12 12"><path d="M2.5 6.2 5 8.5 9.5 3.5"/></svg></span>';

  /* número que anima até o valor novo */
  function conta(el, alvo, ms, casas) {
    if (!el) return;
    var f = casas ? fmt1 : fmt;
    var escreve = function (v) { el.textContent = f.format(casas ? v : Math.round(v)); };
    var de = el._v == null ? alvo : el._v;
    if (reduzido || de === alvo) { el._v = alvo; escreve(alvo); return; }
    var t0 = performance.now(), id = {}; el._a = id;
    (function passo(t) {
      if (el._a !== id) return;
      var k = suave(lim((t - t0) / (ms || 700)));
      el._v = de + (alvo - de) * k; escreve(el._v);
      if (k < 1) requestAnimationFrame(passo);
    })(t0);
  }
  function visivel(el, cb) {
    if (!('IntersectionObserver' in window)) { cb(true); return; }
    new IntersectionObserver(function (es) { cb(es[0].isIntersecting); }, { threshold: 0.15 }).observe(el);
  }

  /* tema: o botão tem data-alternar-tema e quem troca é o js/base/tema.js */

  /* menu encolhe */
  var menu = $('menu');
  var aoRolarMenu = function () { menu.classList.toggle('rolou', window.scrollY > 40); };
  addEventListener('scroll', aoRolarMenu, { passive: true }); aoRolarMenu();

  /* ---------- título do hero: a última palavra se digita sozinha ---------- */
  (function () {
    var el = $('hero-palavra');
    var cursor = document.querySelector('.hero h1 .cursor');
    if (!el || !cursor || reduzido) return;
    // No celular só entram palavras que cabem na linha de "hoje tem" (senão o
    // título pula de duas para três linhas a cada troca); na tela larga, as longas também.
    var PALAVRAS = window.matchMedia('(min-width: 720px)').matches
      ? ['treino', 'luta', 'jiu-jitsu', 'boxe', 'dança', 'muay thai', 'judô', 'corrida', 'karatê', 'yoga']
      : ['treino', 'luta', 'boxe', 'dança', 'judô', 'karatê', 'yoga', 'vôlei', 'futsal'];
    var i = 0;
    var espera = function (ms) { return new Promise(function (r) { setTimeout(r, ms); }); };
    // Aba escondida: os timers do navegador ficam lentos e a animação embola;
    // espera a aba voltar antes do próximo passo.
    var visivel = function () {
      if (!document.hidden) return Promise.resolve();
      return new Promise(function (r) {
        document.addEventListener('visibilitychange', function f() {
          if (!document.hidden) { document.removeEventListener('visibilitychange', f); r(); }
        });
      });
    };
    function apaga() {
      cursor.classList.add('digitando');
      return (function passo() {
        if (!el.textContent.length) return espera(320);
        el.textContent = el.textContent.slice(0, -1);
        return espera(55).then(passo);
      })();
    }
    function digita(palavra) {
      var n = 0;
      return (function passo() {
        if (n >= palavra.length) { cursor.classList.remove('digitando'); return Promise.resolve(); }
        n += 1; el.textContent = palavra.slice(0, n);
        return espera(85 + Math.random() * 60).then(passo);
      })();
    }
    function ciclo() {
      return espera(2200).then(visivel).then(apaga).then(function () {
        i = (i + 1) % PALAVRAS.length;
        return digita(PALAVRAS[i]);
      }).then(ciclo);
    }
    // Começa depois da entrada do título.
    espera(1200).then(ciclo);
  })();

  /* ---------- mockup do hero: a semana passa sozinha ---------- */
  var NOMES_DIA = ['Segunda-feira', 'Terça-feira', 'Quarta-feira', 'Quinta-feira', 'Sexta-feira', 'Sábado', 'Domingo'];
  var TREINO = [true, false, true, false, true, false, false];
  var C_A = [['07:00', 'Cuscuz com ovo e café', 620], ['12:30', 'Arroz, feijão e frango grelhado', 890], ['17:30', 'Banana com aveia antes do treino', 410], ['21:30', 'Aipim cozido com carne moída', 931]];
  var C_B = [['07:00', 'Pão integral com ovos mexidos', 590], ['12:30', 'Arroz, feijão e carne de panela', 920], ['17:30', 'Iogurte com granola antes do treino', 430], ['21:30', 'Macarrão com frango e legumes', 911]];
  var C_C = [['07:00', 'Tapioca com queijo e café', 560], ['12:30', 'Arroz, lentilha e peixe assado', 870], ['17:30', 'Vitamina de banana antes do treino', 480], ['21:30', 'Escondidinho de aipim com frango', 941]];
  var D_A = [['07:00', 'Pão francês com ovo mexido', 480], ['12:30', 'Arroz, feijão e peixe assado', 760], ['16:00', 'Iogurte com fruta', 260], ['20:00', 'Omelete com salada', 535]];
  var D_B = [['07:00', 'Cuscuz com queijo e café', 470], ['12:30', 'Arroz, feijão e frango', 780], ['16:00', 'Fruta com castanhas', 250], ['20:00', 'Sopa de legumes com carne', 535]];
  var CARDAPIO = [C_A, D_A, C_B, D_B, C_C, D_A, D_B];
  var MACROS_T = [160, 360, 85], MACROS_D = [160, 195, 72], MAX_M = [260, 425, 212];
  var mk = { dia: 0, feitas: 0, folga: 0, visivel: true };
  var diasBt = Array.prototype.slice.call(document.querySelectorAll('#mk-dias button'));
  var refBox = $('mk-refeicoes'), refCard = $('mk-ref-card');

  function trocaTexto(el, texto) {
    if (el.textContent === texto) return;
    if (reduzido) { el.textContent = texto; return; }
    el.classList.add('saindo');
    setTimeout(function () { el.textContent = texto; el.classList.remove('saindo'); }, 220);
  }
  function linhas(c) {
    return c.map(function (r, i) {
      return '<div class="refeicao" style="--i:' + i + '">' + CHECK + '<span class="hora num">' + r[0] + '</span><span class="nome">' + r[1] + '</span><span class="kcal num">' + fmt.format(r[2]) + ' kcal</span></div>';
    }).join('');
  }
  function consumo() {
    var c = CARDAPIO[mk.dia], total = 0, feito = 0;
    c.forEach(function (r, i) { total += r[2]; if (i < mk.feitas) feito += r[2]; });
    refBox.querySelectorAll('.refeicao').forEach(function (l, i) { l.classList.toggle('feita', i < mk.feitas); });
    conta($('mk-consumido'), feito, 600);
    $('mk-consumo').style.width = (feito / total * 100) + '%';
  }
  function mostraDia(d) {
    mk.dia = d; mk.feitas = 0; mk.folga = 0;
    var t = TREINO[d], meta = t ? 2851 : 2035, m = t ? MACROS_T : MACROS_D;
    diasBt.forEach(function (b, i) { b.classList.toggle('hoje', i === d); b.setAttribute('aria-pressed', i === d); });
    trocaTexto($('mk-dia'), NOMES_DIA[d] + ' · ' + (t ? 'dia de treino' : 'dia de descanso'));
    trocaTexto($('mk-meta-obs'), t ? 'Treino de jiu-jitsu, 1 h 30' : 'Sem treino hoje');
    $('mk-meta-obs').classList.toggle('acento', t);
    trocaTexto($('mk-treino-obs'), t ? 'Calculado pela modalidade' : 'Dia de descanso');
    conta($('mk-meta'), meta, 800); conta($('mk-treino'), t ? 816 : 0, 800);
    $('mk-total').textContent = fmt.format(meta);
    ['mk-card-meta', 'mk-card-treino'].forEach(function (id) { var c = $(id); c.classList.add('pulsa'); setTimeout(function () { c.classList.remove('pulsa'); }, 700); });
    ['p', 'c', 'g'].forEach(function (k, i) { $('mk-' + k).textContent = m[i] + ' g'; $('mk-' + k + 'b').style.width = (m[i] / MAX_M[i] * 100) + '%'; });
    if (reduzido) { refBox.innerHTML = linhas(CARDAPIO[d]); consumo(); return; }
    refCard.classList.add('trocando');
    setTimeout(function () {
      refBox.innerHTML = linhas(CARDAPIO[d]); consumo();
      requestAnimationFrame(function () { requestAnimationFrame(function () { refCard.classList.remove('trocando'); }); });
    }, 300);
  }
  diasBt.forEach(function (b, i) { b.addEventListener('click', function () { mostraDia(i); }); });
  refBox.innerHTML = linhas(C_A);
  ['mk-meta', 'mk-treino'].forEach(function (id) { $(id)._v = 0; });
  $('mk-peso')._v = 0;
  setTimeout(function () { conta($('mk-meta'), 2851, 1400); conta($('mk-treino'), 816, 1400); conta($('mk-peso'), 79.5, 1400, 1); }, reduzido ? 0 : 700);
  visivel($('moldura'), function (v) { mk.visivel = v; });
  if (!reduzido) setInterval(function () {
    if (!mk.visivel || document.hidden) return;
    if (mk.feitas < 4) { mk.feitas++; consumo(); }
    else if (++mk.folga > 1) mostraDia((mk.dia + 1) % 7);
  }, 1150);

  /* faísca do peso no card */
  (function () {
    var tr = [80.7, 80.6, 80.6, 80.4, 80.3, 80.2, 80.0, 79.9, 79.8, 79.7, 79.6, 79.5];
    var pts = tr.map(function (v, i) { return [i / (tr.length - 1) * 84, 4 + (80.8 - v) / 1.4 * 24]; });
    var d = pts.map(function (p, i) { return (i ? 'L' : 'M') + p[0].toFixed(1) + ' ' + p[1].toFixed(1); }).join(' ');
    $('faisca-linha').setAttribute('d', d);
    $('faisca-area').setAttribute('d', d + ' L84 32 L0 32 Z');
    $('faisca-ponto').setAttribute('cy', pts[pts.length - 1][1].toFixed(1));
  })();

  /* ---------- celular: as refeições vão sendo marcadas ---------- */
  var celLis = Array.prototype.slice.call(document.querySelectorAll('#refeicoes-celular li'));
  var celKcal = [620, 890, 410, 931], cel = { feitas: 0, folga: 0, visivel: false };
  function atualizaCel() {
    var feito = 0;
    celLis.forEach(function (li, i) { li.classList.toggle('feita', i < cel.feitas); if (i < cel.feitas) feito += celKcal[i]; });
    $('cel-anel').style.strokeDashoffset = 1 - feito / 2851;
    $('cel-pct').textContent = Math.round(feito / 2851 * 100) + '%';
    $('cel-consumido').textContent = fmt.format(feito) + ' kcal comidas hoje';
  }
  atualizaCel();
  visivel($('celular'), function (v) { cel.visivel = v; });
  if (!reduzido) setInterval(function () {
    if (!cel.visivel || document.hidden) return;
    if (cel.feitas < 4) cel.feitas++;
    else if (++cel.folga > 2) { cel.feitas = 0; cel.folga = 0; }
    atualizaCel();
  }, 1300);

  /* celular inclina seguindo o ponteiro */
  (function () {
    var area = $('visual-celular'), alvo = { x: 0, y: 0 }, atual = { x: 0, y: 0 }, laco = false;
    if (reduzido || !matchMedia('(pointer: fine)').matches) return;
    function passo() {
      atual.x += (alvo.x - atual.x) * 0.1; atual.y += (alvo.y - atual.y) * 0.1;
      $('celular').style.setProperty('--ry', atual.x.toFixed(2) + 'deg');
      $('celular').style.setProperty('--rx', atual.y.toFixed(2) + 'deg');
      if (Math.abs(alvo.x - atual.x) + Math.abs(alvo.y - atual.y) > 0.02) requestAnimationFrame(passo); else laco = false;
    }
    function liga() { if (!laco) { laco = true; requestAnimationFrame(passo); } }
    area.addEventListener('pointermove', function (e) {
      var r = area.getBoundingClientRect();
      alvo.x = ((e.clientX - r.left) / r.width - 0.5) * 22;
      alvo.y = -((e.clientY - r.top) / r.height - 0.5) * 16; liga();
    });
    area.addEventListener('pointerleave', function () { alvo.x = 0; alvo.y = 0; liga(); });
  })();

  /* nome regional trocando */
  var nomes = ['Aipim', 'Mandioca', 'Macaxeira'], iNome = 0;
  var reg = $('regional'), chips = document.querySelectorAll('#chips-regionais .chip');
  function trocaNome(i) {
    iNome = i; reg.classList.add('troca');
    setTimeout(function () { reg.textContent = nomes[i]; reg.classList.remove('troca'); }, reduzido ? 0 : 250);
    chips.forEach(function (c, j) { c.classList.toggle('ativo', j === i); });
  }
  chips.forEach(function (c, j) { c.style.cursor = 'pointer'; c.addEventListener('click', function () { trocaNome(j); }); });
  if (!reduzido) setInterval(function () { trocaNome((iNome + 1) % nomes.length); }, 2600);

  /* ---------- gráfico da balança ---------- */
  var PERIODOS = {
    '4s': { pes: [80.9, 80.4, 80.6, 80.1, 80.5, 80.2, 79.9, 80.1, 79.7, 79.9, 79.5, 79.7, 79.3, 79.4], eixo: ['sem 1', 'sem 2', 'sem 3', 'sem 4'], nome: function (i) { return 'pesagem do dia ' + (i * 2 + 1); }, fim: 'em 4 semanas', ajuste: [80.7, 79.5] },
    '3m': { pes: [82.6, 82.3, 82.4, 81.9, 81.6, 81.7, 81.2, 80.9, 80.6, 80.4, 79.9, 79.6], eixo: ['ago', 'set', 'out', 'nov'], nome: function (i) { return 'semana ' + (i + 1); }, fim: 'em 3 meses', ajuste: [82.5, 79.5] },
    '6m': { pes: [84.1, 83.8, 83.2, 83.3, 82.6, 82.1, 81.9, 81.3, 80.9, 80.6, 80.2, 79.9, 79.6], eixo: ['jun', 'ago', 'set', 'nov'], nome: function (i) { return 'quinzena ' + (i + 1); }, fim: 'em 6 meses', ajuste: [84, 79.5] }
  };
  var G = { x0: 4, x1: 432, y0: 14, y1: 172 }, gAtual = null, gP = 1;
  function tendencia(p) { var t = [p[0] + 0.15], a = 0.3; for (var i = 1; i < p.length; i++) t.push(t[i - 1] + a * (p[i] - t[i - 1])); return t; }
  function curva(pts) {
    var d = 'M' + pts[0][0].toFixed(1) + ' ' + pts[0][1].toFixed(1);
    for (var i = 0; i < pts.length - 1; i++) {
      var p0 = pts[i - 1] || pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || p2;
      d += ' C' + (p1[0] + (p2[0] - p0[0]) / 6).toFixed(1) + ' ' + (p1[1] + (p2[1] - p0[1]) / 6).toFixed(1) + ' ' + (p2[0] - (p3[0] - p1[0]) / 6).toFixed(1) + ' ' + (p2[1] - (p3[1] - p1[1]) / 6).toFixed(1) + ' ' + p2[0].toFixed(1) + ' ' + p2[1].toFixed(1);
    }
    return d;
  }
  function poeD(el, d) { el.setAttribute('d', d); if (!reduzido) el.style.d = 'path("' + d + '")'; }
  function desenhaGrafico(chave) {
    var P = PERIODOS[chave], tr = tendencia(P.pes), n = P.pes.length;
    if (P.ajuste) { var a0 = tr[0], a1 = tr[n - 1]; tr = tr.map(function (v) { return P.ajuste[0] + (v - a0) / (a1 - a0) * (P.ajuste[1] - P.ajuste[0]); }); }
    var todos = P.pes.concat(tr), mn = Math.floor(Math.min.apply(null, todos) * 2) / 2 - 0.5, mx = Math.ceil(Math.max.apply(null, todos) * 2) / 2 + 0.5;
    var X = function (i) { return G.x0 + i / (n - 1) * (G.x1 - G.x0); }, Y = function (v) { return G.y0 + (mx - v) / (mx - mn) * (G.y1 - G.y0); };
    gAtual = { P: P, tr: tr, X: X, Y: Y, n: n };
    var grade = '';
    [mx, (mx + mn) / 2, mn].forEach(function (v) { var y = Y(v).toFixed(1); grade += '<line class="grade-linha" x1="0" x2="' + G.x1 + '" y1="' + y + '" y2="' + y + '"/><text class="eixo" x="444" y="' + (+y + 4) + '">' + fmt1.format(v) + '</text>'; });
    P.eixo.forEach(function (r, i) { grade += '<text class="eixo" x="' + (G.x0 + i / (P.eixo.length - 1) * (G.x1 - G.x0)).toFixed(1) + '" y="200" text-anchor="' + (i === 0 ? 'start' : i === P.eixo.length - 1 ? 'end' : 'middle') + '">' + r + '</text>'; });
    $('grade').innerHTML = grade;
    var pts = tr.map(function (v, i) { return [X(i), Y(v)]; }), d = curva(pts);
    poeD($('tendencia'), d);
    poeD($('area'), d + ' L' + G.x1 + ' ' + G.y1 + ' L' + G.x0 + ' ' + G.y1 + ' Z');
    $('pontos').innerHTML = P.pes.map(function (v, i) { return '<circle class="ponto" r="3.4" cx="' + X(i).toFixed(1) + '" cy="' + Y(v).toFixed(1) + '"/>'; }).join('');
    var fx = X(n - 1), fy = Y(tr[n - 1]);
    ['fim', 'pulso'].forEach(function (id) { $(id).setAttribute('cx', fx.toFixed(1)); $(id).setAttribute('cy', fy.toFixed(1)); });
    conta($('peso-tendencia'), Math.round(tr[n - 1] * 10) / 10, 600, 1);
    $('peso-delta').textContent = '↓ ' + fmt1.format(tr[0] - tr[n - 1]) + ' kg ' + P.fim;
    dicaEm(n - 1, true); revelaGrafico(gP);
  }
  function dicaEm(i, tendenciaFim) {
    var g = gAtual, dica = $('dica'), svg = $('grafico'), esc = svg.getBoundingClientRect().width / 480 || 1;
    var v = tendenciaFim ? g.tr[i] : g.P.pes[i], y = tendenciaFim ? g.Y(g.tr[i]) : g.Y(g.P.pes[i]);
    dica.style.left = (g.X(i) * esc) + 'px'; dica.style.top = (y * esc) + 'px';
    $('dica-v').textContent = fmt1.format(v) + ' kg';
    $('dica-q').textContent = tendenciaFim ? 'tendência hoje' : g.P.nome(i);
    $('mira').setAttribute('x1', g.X(i)); $('mira').setAttribute('x2', g.X(i));
    $('pontos').querySelectorAll('.ponto').forEach(function (c, j) { c.classList.toggle('ativo', !tendenciaFim && j === i); });
  }
  function revelaGrafico(p) {
    gP = p;
    $('revela-rect').setAttribute('width', (504 * p).toFixed(1));
    var pts = $('pontos').querySelectorAll('.ponto');
    pts.forEach(function (c, i) { c.classList.toggle('on', p > i / pts.length * 0.8); });
    $('dica').style.opacity = p > 0.85 ? 1 : 0;
    $('fim').style.opacity = $('pulso').style.opacity = p > 0.85 ? '' : 0;
    $('selo-ajuste').classList.toggle('espera', p < 0.92);
    $('meta-nova').textContent = fmt.format(2851 - 86 * suave(lim((p - 0.75) / 0.25)));
  }
  (function () {
    var area = $('grafico-area'), svg = $('grafico');
    svg.addEventListener('pointermove', function (e) {
      if (!gAtual) return;
      var r = svg.getBoundingClientRect(), x = (e.clientX - r.left) / r.width * 480;
      var i = Math.max(0, Math.min(gAtual.n - 1, Math.round((x - G.x0) / (G.x1 - G.x0) * (gAtual.n - 1))));
      area.classList.add('mirando'); dicaEm(i, false);
    });
    svg.addEventListener('pointerleave', function () { area.classList.remove('mirando'); if (gAtual) dicaEm(gAtual.n - 1, true); });
    document.querySelectorAll('#periodos button').forEach(function (b) {
      b.addEventListener('click', function () {
        document.querySelectorAll('#periodos button').forEach(function (x) { x.setAttribute('aria-pressed', x === b); });
        desenhaGrafico(b.dataset.periodo);
      });
    });
    addEventListener('resize', function () { if (gAtual) dicaEm(gAtual.n - 1, true); });
  })();
  desenhaGrafico('4s');

  /* ---------- simulador ---------- */
  var sim = { objetivo: 'perder', met: 7.8, nome: 'Jiu-jitsu', h: 1.5, kg: 80, dias: [true, false, true, false, true, false, false] };
  var abas = $('abas'), inPeso = $('sim-peso');
  function grupo(id, aoEscolher) {
    var bts = document.querySelectorAll('#' + id + ' button');
    bts.forEach(function (b, i) { b.addEventListener('click', function () { bts.forEach(function (x) { x.setAttribute('aria-pressed', x === b); }); aoEscolher(b, i); atualizaSim(); }); });
  }
  function sinal(v) { return (v < 0 ? '− ' : '+ ') + fmt.format(Math.abs(Math.round(v))); }
  function atualizaSim() {
    var kg = sim.kg, tmb = 10 * kg + 6.25 * 178 - 5 * 30 + 5, dia = tmb * 0.4, treino = (sim.met - 1) * kg * sim.h;
    var ritmo = sim.objetivo === 'perder' ? -0.005 : sim.objetivo === 'ganhar' ? 0.0025 : 0, obj = ritmo * kg * 7700 / 7;
    var tDia = tmb + dia + treino + obj, dDia = tmb + dia + obj, nT = sim.dias.filter(Boolean).length;
    $('sim-peso-valor').textContent = kg + ' kg'; inPeso.value = kg;
    conta($('sim-total'), Math.round(tDia), 600); conta($('sim-desc'), Math.round(dDia), 600);
    conta($('sim-media'), Math.round((nT * tDia + (7 - nT) * dDia) / 7), 600);
    $('sim-n-dias').textContent = nT + (nT === 1 ? ' por semana' : ' por semana');
    var maxV = Math.max(tDia, dDia);
    document.querySelectorAll('#sim-barras .sim-barra').forEach(function (b, i) {
      var v = sim.dias[i] ? tDia : dDia;
      b.classList.toggle('treino', sim.dias[i]);
      b.querySelector('.col').style.setProperty('--h', (v / maxV * 100) + '%');
      b.querySelector('b').textContent = fmt1.format(v / 1000).replace(',0', '') + 'k';
    });
    $('p0').textContent = fmt.format(Math.round(tmb)); $('p1').textContent = sinal(dia); $('p2').textContent = sinal(treino);
    $('p2-nome').textContent = 'Treino de ' + sim.nome.toLowerCase();
    $('p3').textContent = obj === 0 ? '0' : sinal(obj);
    $('p3-nome').textContent = sim.objetivo === 'perder' ? 'Objetivo: perder peso' : sim.objetivo === 'ganhar' ? 'Objetivo: ganhar peso' : 'Objetivo: manter o peso';
    var soma = tmb + dia + treino;
    $('b0').style.flexBasis = (tmb / soma * 100) + '%'; $('b1').style.flexBasis = (dia / soma * 100) + '%'; $('b2').style.flexBasis = (treino / soma * 100) + '%';
  }
  abas.addEventListener('click', function (e) {
    var b = e.target.closest('button'); if (!b) return;
    sim.objetivo = b.dataset.objetivo;
    abas.querySelectorAll('button').forEach(function (x, i) { x.setAttribute('aria-pressed', x === b); if (x === b) abas.style.setProperty('--aba', i); });
    atualizaSim();
  });
  grupo('sim-mods', function (b) { sim.met = +b.dataset.met; sim.nome = b.textContent; });
  grupo('sim-dur', function (b) { sim.h = +b.dataset.h; });
  document.querySelectorAll('#sim-dias button').forEach(function (b, i) {
    b.addEventListener('click', function () { sim.dias[i] = !sim.dias[i]; b.setAttribute('aria-pressed', sim.dias[i]); atualizaSim(); });
  });
  inPeso.addEventListener('input', function () { sim.kg = +inPeso.value; atualizaSim(); });
  document.querySelectorAll('.passo').forEach(function (b) {
    b.addEventListener('click', function () { sim.kg = Math.max(50, Math.min(120, sim.kg + +b.dataset.passo)); atualizaSim(); });
  });
  atualizaSim();

  /* dúvidas: uma aberta por vez */
  var perguntas = document.querySelectorAll('.pergunta');
  perguntas.forEach(function (p) {
    p.querySelector('button').addEventListener('click', function () {
      var abrir = !p.classList.contains('aberta');
      perguntas.forEach(function (o) { o.classList.remove('aberta'); o.querySelector('button').setAttribute('aria-expanded', 'false'); });
      if (abrir) { p.classList.add('aberta'); this.setAttribute('aria-expanded', 'true'); }
    });
  });

  /* faixa de modalidades escolhe o número grande (revisão de 2026-11-16):
     mesma conta do app, (MET − 1) × 80 kg × 1,5 h. Passar o mouse já escolhe;
     toque e teclado escolhem no clique. Toque ou foco pausam a faixa por 6s. */
  var letreiro = document.querySelector('.letreiro');
  var botoesMod = letreiro ? letreiro.querySelectorAll('button[data-met]') : [];
  var gastoAtual = 816, fatorRolagem = 1, escolheu = false, animaGasto = null, pausaFaixa = null;
  function escreveGasto() { $('conta-maos').textContent = fmt.format(Math.round(gastoAtual * fatorRolagem)); }
  function escolheMod(nome, met) {
    var alvo = Math.round((met - 1) * 80 * 1.5);
    botoesMod.forEach(function (b) { b.setAttribute('aria-pressed', b.textContent === nome ? 'true' : 'false'); });
    if ($('nome-maos').textContent === nome.toLowerCase()) return;
    fatorRolagem = 1; escolheu = true; /* depois da escolha, a contagem da rolagem não mexe mais no número */
    $('nome-maos').textContent = nome.toLowerCase();
    $('aviso-maos').textContent = fmt.format(alvo) + ' kcal por treino de ' + nome.toLowerCase() + '.';
    if (animaGasto) cancelAnimationFrame(animaGasto);
    if (reduzido) { gastoAtual = alvo; escreveGasto(); return; }
    var de = gastoAtual, t0 = performance.now();
    (function passo(t) {
      var k = suave(lim((t - t0) / 600));
      gastoAtual = de + (alvo - de) * k; escreveGasto();
      animaGasto = k < 1 ? requestAnimationFrame(passo) : null;
    })(t0);
  }
  function pausa() {
    letreiro.classList.add('pausa');
    clearTimeout(pausaFaixa);
    pausaFaixa = setTimeout(function () { letreiro.classList.remove('pausa'); }, 6000);
  }
  botoesMod.forEach(function (b) {
    var met = +b.dataset.met;
    b.addEventListener('click', function () { escolheMod(b.textContent, met); });
    b.addEventListener('pointerenter', function (e) { if (e.pointerType === 'mouse') escolheMod(b.textContent, met); });
  });
  if (letreiro) {
    letreiro.addEventListener('pointerdown', function (e) { if (e.pointerType !== 'mouse') pausa(); });
    letreiro.addEventListener('focusin', pausa);
  }

  if (reduzido) return;
  raiz.classList.add('js');

  /* revelar ao entrar na tela: só o que está abaixo da dobra agora */
  var alturaTela = innerHeight;
  var aRevelar = Array.prototype.filter.call(document.querySelectorAll('.revela'), function (el) { return el.getBoundingClientRect().top > alturaTela * 0.92; });
  aRevelar.forEach(function (el) { el.classList.add('espera'); });
  celLis.forEach(function (el) { el.classList.add('espera'); });
  var obs = new IntersectionObserver(function (entradas) {
    entradas.forEach(function (e) {
      if (!e.isIntersecting) return;
      e.target.classList.remove('espera');
      if (e.target.id === 'celular') celLis.forEach(function (li) { li.classList.remove('espera'); });
      obs.unobserve(e.target);
    });
  }, { rootMargin: '0px 0px -12% 0px' });
  aRevelar.forEach(function (el) { obs.observe(el); });
  obs.observe($('celular'));

  /* cenas presas à rolagem: cada uma escreve --p (0 a 1), com suavização */
  var cenas = [
    { el: $('moldura'), de: function (t, vh) { return t - vh * 0.98; }, ate: function (t, vh) { return t - vh * 0.35; } },
    { el: document.querySelector('.numero-grande'), de: function (t, vh) { return t - vh; }, ate: function (t, vh) { return t - vh * 0.22; }, apos: function (p) {
        if (escolheu) return; fatorRolagem = suave(lim((p - 0.25) / 0.75)); escreveGasto(); } },
    { el: $('celular'), de: function (t, vh) { return t - vh * 1.05; }, ate: function (t, vh) { return t - vh * 0.3; } },
    { el: $('card-grafico'), de: function (t, vh) { return t - vh * 0.85; }, ate: function (t, vh) { return t - vh * 0.25; }, apos: revelaGrafico },
    { el: $('final'), de: function (t, vh) { return t - vh; }, ate: function (t, vh) { return t - vh * 0.3; } }
  ];
  var palco = $('palco-mockup');
  cenas.forEach(function (c) { c.p = 0; c.alvo = 0; });
  var rodando = false, sAlvo = 0, sAtual = 0;
  function mede() {
    var vh = innerHeight, y = scrollY;
    cenas.forEach(function (c) {
      var topo = c.el.getBoundingClientRect().top + y;
      var a = c.de(topo, vh), b = c.ate(topo, vh);
      c.alvo = lim((y - a) / Math.max(1, b - a));
    });
    sAlvo = Math.min(1.6, y / vh);
  }
  function quadro() {
    var mexendo = false;
    cenas.forEach(function (c) {
      var d = c.alvo - c.p;
      c.p = Math.abs(d) < 0.001 ? c.alvo : c.p + d * 0.12;
      if (c.p !== c.alvo) mexendo = true;
      c.el.style.setProperty('--p', c.p.toFixed(4));
      if (c.apos) c.apos(c.p);
    });
    var ds = sAlvo - sAtual; sAtual = Math.abs(ds) < 0.001 ? sAlvo : sAtual + ds * 0.12;
    if (sAtual !== sAlvo) mexendo = true;
    palco.style.setProperty('--s', sAtual.toFixed(4));
    if (mexendo) requestAnimationFrame(quadro); else rodando = false;
  }
  function aoRolar() { mede(); if (!rodando) { rodando = true; requestAnimationFrame(quadro); } }
  addEventListener('scroll', aoRolar, { passive: true });
  addEventListener('resize', aoRolar);
  aoRolar();
})();
