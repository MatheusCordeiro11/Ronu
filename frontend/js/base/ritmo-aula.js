// Ronu — "Ritmo da aula" (intensidade do treino), compartilhado entre Treino e onboarding.
//
// Só existe nas modalidades com os dois blocos da aula no catálogo (metBlocoLeve e metBlocoIntenso, do
// GET /api/modalidades): as 10 lutas e o treino funcional. As chaves da API são as mesmas em todas
// ("tecnica", "normal", "sparring"); os rótulos saem da categoria: Lutas com técnica/sparring, as outras com
// força/circuito. O seletor é o .segmentado neutro (js/base/indicador.js põe a pílula deslizante), com a forma
// curta dos rótulos quando o bloco fica estreito (container query em componentes.css) e o nome completo no
// aria-label. A dica embaixo descreve a aula do nível, sem adjetivo de esforço: a pessoa escolhe pela aula.
(function () {
  'use strict';

  var PADRAO = 'normal';
  var NIVEIS = ['tecnica', 'normal', 'sparring'];

  // Fração do tempo no bloco intenso por nível (IntensidadeTreino no backend), só para o texto da conta.
  var FRACAO = { tecnica: 0.3, normal: 0.5, sparring: 0.7 };

  var GRUPOS = {
    luta: {
      largo: { tecnica: 'Mais técnica', normal: 'Normal', sparring: 'Muito sparring' },
      curto: { tecnica: 'Técnica', normal: 'Normal', sparring: 'Sparring' },
      dica: {
        tecnica: 'A maior parte da aula é técnica e drill, com pouco rola ou sparring.',
        normal: 'Metade técnica, metade rola ou sparring.',
        sparring: 'A maior parte da aula é rola, sparring ou luta.'
      },
      leve: 'técnica',
      intenso: 'sparring'
    },
    forca: {
      largo: { tecnica: 'Mais força', normal: 'Normal', sparring: 'Mais circuito' },
      curto: { tecnica: 'Força', normal: 'Normal', sparring: 'Circuito' },
      dica: {
        tecnica: 'A maior parte é série de força, com descanso entre as séries.',
        normal: 'Metade força, metade circuito.',
        sparring: 'A maior parte é circuito, quase sem descanso.'
      },
      leve: 'força',
      intenso: 'circuito'
    }
  };

  var contador = 0;

  function temRitmo(modalidade) {
    return !!modalidade && modalidade.metBlocoLeve != null && modalidade.metBlocoIntenso != null;
  }

  function grupo(modalidade) {
    return modalidade && modalidade.categoria === 'Lutas' ? GRUPOS.luta : GRUPOS.forca;
  }

  function valido(nivel) {
    return NIVEIS.indexOf(nivel) >= 0 ? nivel : PADRAO;
  }

  // "muito sparring" para o resumo do card e do painel; nulo em "normal" (não aparece).
  function resumo(modalidade, nivel) {
    if (!temRitmo(modalidade) || !nivel || nivel === PADRAO) return null;
    return grupo(modalidade).largo[valido(nivel)].toLowerCase();
  }

  // O mesmo, só com o nome (a composição salva da dieta não traz a categoria). Ritmo diferente de "normal" só
  // existe nas lutas e no treino funcional, o único com blocos fora das lutas.
  function resumoPorNome(nome, nivel) {
    if (!nivel || nivel === PADRAO || NIVEIS.indexOf(nivel) < 0) return null;
    var g = nome === 'Treino funcional' ? GRUPOS.forca : GRUPOS.luta;
    return g.largo[nivel].toLowerCase();
  }

  function decimal(valor) {
    return Number(valor).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 2 });
  }

  // "Intensidade 8,8 = 70% sparring (10,3) + 30% técnica (5,3), pelo Compêndio de Atividades Físicas."
  function composicao(modalidade, nivel, metEfetivo) {
    if (!temRitmo(modalidade)) return null;
    var g = grupo(modalidade);
    var f = FRACAO[valido(nivel)];
    var partes = f === 0.5
      ? 'metade ' + g.intenso + ' (' + decimal(modalidade.metBlocoIntenso) + '), metade ' + g.leve +
        ' (' + decimal(modalidade.metBlocoLeve) + ')'
      : Math.round(f * 100) + '% ' + g.intenso + ' (' + decimal(modalidade.metBlocoIntenso) + ') + ' +
        Math.round((1 - f) * 100) + '% ' + g.leve + ' (' + decimal(modalidade.metBlocoLeve) + ')';
    return 'Intensidade ' + decimal(metEfetivo) + ' = ' + partes + ', pelo Compêndio de Atividades Físicas.';
  }

  // Bloco "Ritmo da aula": rótulo, segmentado com três opções e a dica. aoEscolher(nivel) só é chamado quando
  // o nível muda. Devolve { el, definir(nivel) } para a tela atualizar sem recriar.
  function criar(modalidade, nivelInicial, aoEscolher, classeRotulo) {
    var g = grupo(modalidade);
    var id = 'ritmo-' + (++contador);
    var atual = valido(nivelInicial);

    var bloco = document.createElement('div');
    bloco.className = 'ritmo';

    var rotulo = document.createElement('span');
    rotulo.className = classeRotulo || 'campo-rotulo';
    rotulo.id = id + '-rotulo';
    rotulo.textContent = 'Ritmo da aula';

    var seg = document.createElement('div');
    seg.className = 'segmentado ritmo-seg';
    seg.setAttribute('role', 'radiogroup');
    seg.setAttribute('aria-labelledby', rotulo.id);
    seg.setAttribute('aria-describedby', id + '-dica');

    var dica = document.createElement('p');
    dica.className = 'ritmo-dica';
    dica.id = id + '-dica';

    var botoes = NIVEIS.map(function (nivel) {
      var b = document.createElement('button');
      b.type = 'button';
      b.dataset.nivel = nivel;
      b.setAttribute('role', 'radio');
      b.setAttribute('aria-label', g.largo[nivel]);
      var largo = document.createElement('span');
      largo.className = 'ritmo-largo';
      largo.textContent = g.largo[nivel];
      var curto = document.createElement('span');
      curto.className = 'ritmo-curto';
      curto.textContent = g.curto[nivel];
      largo.setAttribute('aria-hidden', 'true');
      curto.setAttribute('aria-hidden', 'true');
      b.append(largo, curto);
      b.addEventListener('click', function () { escolher(nivel, false); });
      seg.appendChild(b);
      return b;
    });

    // Setas andam entre as opções (padrão de radiogroup), com o foco na escolhida.
    seg.addEventListener('keydown', function (evento) {
      var passo = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[evento.key];
      if (!passo) return;
      evento.preventDefault();
      var i = (NIVEIS.indexOf(atual) + passo + NIVEIS.length) % NIVEIS.length;
      escolher(NIVEIS[i], true);
    });

    function pintar() {
      botoes.forEach(function (b) {
        var marcado = b.dataset.nivel === atual;
        b.setAttribute('aria-checked', String(marcado));
        b.tabIndex = marcado ? 0 : -1;
      });
      dica.textContent = g.dica[atual];
    }

    function escolher(nivel, focar) {
      var mudou = nivel !== atual;
      atual = nivel;
      pintar();
      if (focar) botoes[NIVEIS.indexOf(nivel)].focus();
      if (mudou && aoEscolher) aoEscolher(nivel);
    }

    pintar();
    bloco.append(rotulo, seg, dica);
    return {
      el: bloco,
      definir: function (nivel) { atual = valido(nivel); pintar(); }
    };
  }

  window.RonuRitmoAula = {
    PADRAO: PADRAO,
    temRitmo: temRitmo,
    valido: valido,
    resumo: resumo,
    resumoPorNome: resumoPorNome,
    composicao: composicao,
    criar: criar
  };
})();
