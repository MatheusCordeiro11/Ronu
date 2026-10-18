// Ronu — Dashboard (área do atleta)
// Fluxo: guarda de sessão -> checagem de cadastro completo (pulada se a marca
// de cadastro completo já existe; senão GETs simples, nunca chama a IA só pra
// validar) -> carrega o histórico de dietas (o item [0] já é a dieta atual,
// não precisa de uma chamada separada a /dietas/atual) -> renderiza.
//
// Os cards da lateral (objetivo, progresso, treino, alimentação) vêm de GETs
// próprios, em paralelo e depois da dieta: se um falhar, só aquele card some,
// a dieta continua na tela.

document.addEventListener('DOMContentLoaded', async () => {
  const usuario = ronuUsuarioLogado();
  if (!usuario) {
    window.location.href = 'login.html';
    return;
  }

  const $ = (id) => document.getElementById(id);

  const elLoading = $('dashboard-loading');
  const elLoadError = $('dashboard-load-error');
  const elEmpty = $('dashboard-empty');
  const elContent = $('dashboard-content');
  const elGerarError = $('dashboard-gerar-error');
  const elAcoes = $('db-acoes');
  const elGerarFim = $('db-gerar-fim');

  const elVersionSwitch = $('version-switch');
  const elDayRow = $('day-row');
  const elDayPanel = $('day-panel');
  const elAjuste = $('meta-ajuste');
  const elAjusteMensagem = $('meta-ajuste-mensagem');
  const elAjusteDetalhe = $('meta-ajuste-detalhe');

  const btnGerar = $('btn-gerar');
  const btnGerarVazio = $('btn-gerar-vazio');
  const btnGerarFim = $('btn-gerar-fim');
  const btnTentarNovamente = $('btn-tentar-novamente');

  // Mesmas regras do DietasController: 3 gerações por hora, janela móvel.
  const LIMITE_GERACOES_POR_HORA = 3;
  const JANELA_GERACOES_MS = 60 * 60 * 1000;

  let historico = [];
  let versaoSelecionada = 0;
  let diaSelecionado = 0;
  // Celular: as refeições além das 2 primeiras ficam recolhidas até a pessoa
  // abrir; aberto uma vez, continua aberto ao trocar de dia.
  let refeicoesAbertas = false;
  const REFEICOES_VISIVEIS_CELULAR = 2;

  // ---------- Formatação ----------

  function formatarNumero(valor) {
    return Math.round(valor).toLocaleString('pt-BR');
  }

  function formatarData(isoString) {
    return new Date(isoString).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
  }

  function formatarDecimal(valor, casas = 1) {
    return Number(valor).toLocaleString('pt-BR', { maximumFractionDigits: casas });
  }

  // "Segunda-feira" -> "Seg" (aba) e "segunda" (frases).
  function abreviarDia(diaSemana) {
    return diaSemana.slice(0, 3);
  }

  function nomeCurtoDia(diaSemana) {
    return diaSemana.split('-')[0].toLowerCase();
  }

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto != null) el.textContent = texto;
    return el;
  }

  // ---------- Saudação ----------

  function saudacao() {
    const hora = new Date().getHours();
    const periodo = hora < 5 ? 'Boa noite' : hora < 12 ? 'Bom dia' : hora < 18 ? 'Boa tarde' : 'Boa noite';
    const primeiroNome = (usuario.nome || '').trim().split(/\s+/)[0];
    $('db-saudacao').textContent = primeiroNome ? `${periodo}, ${primeiroNome}.` : `${periodo}.`;
  }

  saudacao();

  // ---------- Estados de topo (mutuamente exclusivos) ----------

  function mostrarSomente(elementoVisivel) {
    [elLoading, elLoadError, elEmpty, elContent].forEach((el) => {
      el.hidden = el !== elementoVisivel;
    });
    // A ação de gerar e o seletor de versão só fazem sentido com dieta na tela
    // (o estado vazio tem o próprio botão).
    elAcoes.hidden = elementoVisivel !== elContent;
    elGerarFim.hidden = elementoVisivel !== elContent;
  }

  // ---------- Dia de treino ou descanso ----------

  // Dietas com composicaoMeta dizem direto se o dia teve sessão. Nas antigas,
  // o dia de treino é o que tem meta acima da menor da semana (o treino é a
  // única parte que muda entre os dias).
  function diaDeTreino(dieta, indice) {
    const dia = dieta.dias[indice];
    if (dia.composicaoMeta) return dia.composicaoMeta.treino.length > 0;
    const menor = Math.min(...dieta.dias.map((d) => d.metaCalculada.calorias));
    return dia.metaCalculada.calorias > menor + 50;
  }

  // ---------- Render: meta do dia ----------

  // A meta varia por dia (dia de treino vs. dia de descanso), então lê sempre
  // do dia selecionado (dietaResponse.dieta.dias[diaSelecionado]).
  function renderizarMeta(dietaResponse) {
    const dieta = dietaResponse.dieta;
    const dia = dieta.dias[diaSelecionado];
    const meta = dia.metaCalculada;
    const total = dia.totalDoDia;

    $('db-meta-rotulo').textContent = `Meta de ${nomeCurtoDia(dia.diaSemana)}`;
    $('db-meta-kcal').textContent = formatarNumero(meta.calorias);
    $('db-anel-tipo').textContent = diaDeTreino(dieta, diaSelecionado) ? 'Treino' : 'Descanso';
    $('db-anel-dia').textContent = abreviarDia(dia.diaSemana);

    // O anel mostra quanto do cardápio montado cobre a meta do dia.
    const cobertura = meta.calorias > 0 ? total.calorias / meta.calorias : 0;
    $('db-anel').style.setProperty('--anel-fim', String(1 - Math.min(1, cobertura)));
    $('db-meta-cobertura').textContent =
      `Cardápio do dia: ${formatarNumero(total.calorias)} kcal, ${Math.round(cobertura * 100)}% da meta`;

    // Barras: a parte das kcal da meta que vem de cada macro.
    const macros = [
      ['db-p', meta.proteinasG, 4],
      ['db-c', meta.carboidratosG, 4],
      ['db-g', meta.gordurasG, 9]
    ];
    macros.forEach(([id, gramas, kcalPorGrama]) => {
      $(id).textContent = `${formatarNumero(gramas)} g`;
      const fracao = meta.calorias > 0 ? Math.min(1, (gramas * kcalPorGrama) / meta.calorias) : 0;
      $(`${id}-barra`).style.width = `${(fracao * 100).toFixed(1)}%`;
    });

    $('db-total').innerHTML = '';
    const forte = criar('strong', null, `${formatarNumero(total.calorias)} kcal`);
    $('db-total').append(
      'Total do dia: ', forte,
      ` · P ${formatarNumero(total.proteinasG)} g · C ${formatarNumero(total.carboidratosG)} g · G ${formatarNumero(total.gordurasG)} g`
    );
  }

  // ---------- Render: ajuste pelo progresso (meta adaptativa) ----------

  // O ajuste é um registro de como ESTA dieta foi feita (vale para a semana
  // toda, não muda com o dia), por isso os textos falam "até esta dieta ser
  // gerada", nunca "agora". Nada de kcal: em dias com MetaElevadaPeloPiso a
  // diferença não dá para derivar.

  // Abaixo de 50 g/semana o texto trata como "praticamente estável" / "no
  // ritmo". Só afeta a redação — a classificação da causa usa o sinal exato.
  const TOLERANCIA_RITMO_KG = 0.05;

  function formatarPercentualAjuste(percentual) {
    return `${(Math.abs(percentual) * 100).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
  }

  function formatarPesoSemana(kg) {
    const absoluto = Math.abs(kg);
    if (absoluto >= 1) {
      return `${absoluto.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} kg`;
    }
    return `${Math.round(absoluto * 100) * 10} g`;
  }

  function frasePesoReal(ritmo) {
    if (Math.abs(ritmo) < TOLERANCIA_RITMO_KG) return 'ficou praticamente estável';
    const verbo = ritmo < 0 ? 'caiu' : 'subiu';
    return `${verbo} em média cerca de ${formatarPesoSemana(ritmo)} por semana`;
  }

  // O sinal do ritmo esperado diz o objetivo da dieta (a dieta não guarda o
  // objetivo): negativo = perder, positivo = ganhar, zero = manter.
  function fraseEsperado(ritmo) {
    if (ritmo === 0) return 'ficar estável';
    const verbo = ritmo < 0 ? 'cair' : 'subir';
    return `${verbo} cerca de ${formatarPesoSemana(ritmo)} por semana`;
  }

  function criarParagrafo(...partes) {
    const p = document.createElement('p');
    p.append(...partes);
    return p;
  }

  const RODAPE_AJUSTE = 'O ajuste é gradual, de no máximo 5% por vez, e é refeito do zero a cada nova dieta.';

  // Ajuste aplicado (para menos ou para mais). O motivo vem do sinal do
  // fatorBruto = (ingestão − 1) − (real − esperado)·k, com k > 0. Se a meta
  // caiu mas o peso NÃO está acima do esperado (real ≤ esperado), só a
  // ingestão < 1 explica — a pessoa contou que comeu menos. O inverso vale
  // para a meta que subiu. Nos outros casos o peso explica, e a aderência
  // pode ter somado ou amenizado (o DTO não diz quanto).
  function detalheAjusteAplicado(ajuste, paraMenos) {
    const real = ajuste.ritmoRealKgSemana;
    const esperado = ajuste.ritmoEsperadoKgSemana;
    const percentual = formatarPercentualAjuste(ajuste.percentual);
    const maiorMenor = paraMenos ? 'menor' : 'maior';
    const resultado = `então a meta desta dieta ficou ${percentual} ${maiorMenor}`;

    if (real == null || esperado == null) {
      return [
        criarParagrafo(`Pelo seu histórico de peso, seu gasto parece um pouco ${maiorMenor} do que o Ronu estimou pelo seu corpo e pelo seu treino, ${resultado}.`),
        criarParagrafo(RODAPE_AJUSTE)
      ];
    }

    const diferenca = real - esperado;
    const abertura = `Nas pesagens até esta dieta ser gerada, seu peso ${frasePesoReal(real)}`;
    const explicadoPelaAderencia = paraMenos ? diferenca <= 0 : diferenca >= 0;

    let texto;
    if (!explicadoPelaAderencia) {
      texto = `${abertura}. Para o seu objetivo, o esperado era ${fraseEsperado(esperado)}. `
        + `Considerando esse ritmo (e suas respostas sobre seguir a dieta, quando houver), `
        + `seu gasto parece um pouco ${maiorMenor} do que o Ronu estimou pelo seu corpo e pelo seu treino, ${resultado}.`;
    } else {
      const comendo = `você contou que vem comendo um pouco ${paraMenos ? 'menos' : 'mais'} do que a dieta pede`;
      const conclusao = `seu gasto parece um pouco ${maiorMenor} do que o Ronu estimou, ${resultado}, mais perto do que você realmente come.`;
      texto = Math.abs(diferenca) < TOLERANCIA_RITMO_KG
        ? `${abertura}, no ritmo esperado para o seu objetivo. Mas ${comendo}. Pelo seu peso e pelo que você come, ${conclusao}`
        : `${abertura}; para o seu objetivo, o esperado era ${fraseEsperado(esperado)}. `
          + `O que explica o ajuste é que ${comendo}: pelo seu peso e pelo que você come, ${conclusao}`;
    }

    return [criarParagrafo(texto), criarParagrafo(RODAPE_AJUSTE)];
  }

  // Dentro da zona morta (|fatorBruto| < 1%). Com o peso a 50 g ou mais do
  // esperado, só a ingestão relatada compensa a diferença — mas o sentido
  // dela não é dito, porque depende da meta base, que o DTO não traz.
  function detalheDentroDoEsperado(ajuste) {
    const real = ajuste.ritmoRealKgSemana;
    const esperado = ajuste.ritmoEsperadoKgSemana;
    if (real == null || esperado == null) {
      return [criarParagrafo('Pelo seu histórico de peso, seu progresso está no ritmo, então a meta não mudou.')];
    }

    const base = `Nas pesagens até esta dieta ser gerada, seu peso ${frasePesoReal(real)}; `
      + `para o seu objetivo, o esperado era ${fraseEsperado(esperado)}.`;
    const fecho = Math.abs(real - esperado) < TOLERANCIA_RITMO_KG
      ? ' Está no ritmo, então a meta não mudou. Diferenças pequenas não mexem na meta, para ela não oscilar à toa.'
      : ' A diferença é explicada pelo que você contou sobre seguir a dieta, então a meta não mudou.';
    return [criarParagrafo(base + fecho)];
  }

  // Sem dado suficiente: menos de 5 pesagens, ou elas não cobrem 14 dias
  // (dentro dos últimos 28, no objetivo atual, sem "não deu pra seguir").
  // PontosUsados não diz quantos dias faltam, só quantas pesagens contaram.
  function detalheHistoricoInsuficiente(ajuste) {
    const n = ajuste.pontosUsados ?? 0;
    let situacao;
    if (n === 0) situacao = 'ainda não havia pesagens nesse período';
    else if (n === 1) situacao = 'havia 1 pesagem nesse período';
    else if (n < 5) situacao = `havia ${n} pesagens nesse período`;
    else situacao = `havia ${n} pesagens, mas elas ainda não cobriam 2 semanas`;

    const link = document.createElement('a');
    link.href = 'progresso.html';
    link.textContent = 'Ver progresso';

    return [
      criarParagrafo(
        'O Ronu ajusta a meta pelo seu progresso quando tem pelo menos 5 pesagens cobrindo 2 semanas ou mais, '
        + `nas últimas 4 semanas e com o mesmo objetivo. Quando esta dieta foi gerada, ${situacao}.`
      ),
      criarParagrafo(
        'Pesagens marcadas como “não deu pra seguir” não entram na conta. Continue registrando seu peso em ',
        link,
        '.'
      )
    ];
  }

  // Devolve { aplicado, mensagem, detalhe } ou null (dieta anterior à meta
  // adaptativa, ou um motivo que esta tela ainda não conhece).
  function montarAjuste(ajuste) {
    if (!ajuste) return null;

    switch (ajuste.motivo) {
      case 'PesoAcimaDoEsperado':
      case 'PesoAbaixoDoEsperado': {
        const paraMenos = ajuste.motivo === 'PesoAcimaDoEsperado';
        const valor = document.createElement('strong');
        valor.className = 'num';
        valor.textContent = `${formatarPercentualAjuste(ajuste.percentual)} ${paraMenos ? 'menor' : 'maior'}`;
        return {
          aplicado: true,
          mensagem: ['Meta ajustada pelo seu progresso: ', valor, '.'],
          detalhe: detalheAjusteAplicado(ajuste, paraMenos)
        };
      }
      case 'DentroDoEsperado':
        return {
          aplicado: false,
          mensagem: ['Meta mantida: seu progresso está dentro do esperado.'],
          detalhe: detalheDentroDoEsperado(ajuste)
        };
      case 'HistoricoInsuficiente':
        return {
          aplicado: false,
          mensagem: ['Ajuste pelo seu progresso ainda não ativo.'],
          detalhe: detalheHistoricoInsuficiente(ajuste)
        };
      default:
        return null;
    }
  }

  function renderizarAjuste(dietaResponse) {
    const conteudo = montarAjuste(dietaResponse.dieta.ajusteAdaptativo);
    elAjuste.hidden = !conteudo;
    if (!conteudo) return;

    elAjuste.dataset.estado = conteudo.aplicado ? 'aplicado' : 'neutro';
    elAjusteMensagem.replaceChildren(...conteudo.mensagem);
    elAjusteDetalhe.replaceChildren(...conteudo.detalhe);
  }

  // ---------- Render: seletor de versão (atual / histórico) ----------

  function renderizarVersionSwitch() {
    elVersionSwitch.innerHTML = '';
    elVersionSwitch.hidden = historico.length <= 1;

    historico.forEach((dietaResponse, indice) => {
      const botao = document.createElement('button');
      botao.type = 'button';
      botao.setAttribute('role', 'tab');
      botao.setAttribute('aria-selected', String(indice === versaoSelecionada));
      botao.textContent = indice === 0 ? 'Atual' : `Anterior · ${formatarData(dietaResponse.dataGeracao)}`;

      botao.addEventListener('click', () => {
        versaoSelecionada = indice;
        diaSelecionado = 0;
        renderizarVersaoSelecionada();
      });

      elVersionSwitch.appendChild(botao);
    });
  }

  // ---------- Render: fileira de dias ----------

  // Abas com setas do teclado (padrão de tablist): só a aba marcada fica no
  // Tab; as setas andam entre os dias e já trocam o dia.
  function selecionarDia(dietaResponse, indice, focar) {
    diaSelecionado = indice;
    Array.from(elDayRow.children).forEach((filho, i) => {
      filho.setAttribute('aria-selected', String(i === diaSelecionado));
      filho.tabIndex = i === diaSelecionado ? 0 : -1;
    });
    if (focar) elDayRow.children[indice].focus();
    renderizarDia(dietaResponse);
  }

  function renderizarDayRow(dietaResponse) {
    const dieta = dietaResponse.dieta;
    elDayRow.innerHTML = '';
    const maior = Math.max(...dieta.dias.map((d) => d.metaCalculada.calorias));

    dieta.dias.forEach((dia, indice) => {
      const treino = diaDeTreino(dieta, indice);
      const botao = criar('button', 'db-dia');
      botao.type = 'button';
      botao.id = `db-dia-${indice}`;
      botao.setAttribute('role', 'tab');
      botao.setAttribute('aria-selected', String(indice === diaSelecionado));
      botao.setAttribute('aria-controls', 'day-panel');
      botao.tabIndex = indice === diaSelecionado ? 0 : -1;
      botao.dataset.tipo = treino ? 'treino' : 'descanso';
      botao.setAttribute('aria-label',
        `${dia.diaSemana}, ${treino ? 'dia de treino' : 'descanso'}, meta de ${formatarNumero(dia.metaCalculada.calorias)} kcal`);

      const topo = criar('span', 'db-dia-nome');
      topo.append(criar('span', `db-marca${treino ? ' db-marca-treino' : ''}`), abreviarDia(dia.diaSemana));

      const kcal = criar('span', 'db-dia-kcal num', formatarNumero(dia.metaCalculada.calorias));

      const trilho = criar('span', 'db-dia-trilho');
      const barra = criar('span', 'db-dia-barra');
      barra.dataset.anima = 'barra-v';
      barra.style.height = `${Math.max(12, (dia.metaCalculada.calorias / maior) * 100).toFixed(1)}%`;
      barra.style.setProperty('--atraso', `${indice * 90}ms`);
      trilho.appendChild(barra);

      botao.append(topo, kcal, trilho);
      botao.addEventListener('click', () => selecionarDia(dietaResponse, indice, false));
      elDayRow.appendChild(botao);
    });

    elDayRow.onkeydown = (evento) => {
      const passo = { ArrowRight: 1, ArrowLeft: -1 }[evento.key];
      const total = dieta.dias.length;
      let alvo = null;
      if (passo) alvo = (diaSelecionado + passo + total) % total;
      else if (evento.key === 'Home') alvo = 0;
      else if (evento.key === 'End') alvo = total - 1;
      if (alvo == null) return;
      evento.preventDefault();
      selecionarDia(dietaResponse, alvo, true);
    };
  }

  // ---------- Render: refeições do dia ----------

  function criarRefeicao(refeicao, indice) {
    const card = criar('article', 'cartao db-refeicao ergue entra');
    card.style.setProperty('--atraso', `${indice * 70}ms`);

    // Horário antes do nome, como numa agenda. Dietas geradas antes do campo
    // existir vêm com horario null — aí fica só o nome.
    const titulo = criar('div', 'db-refeicao-titulo');
    if (refeicao.horario) {
      const horario = criar('time', 'db-refeicao-hora num', refeicao.horario);
      horario.dateTime = refeicao.horario;
      titulo.appendChild(horario);
    }
    titulo.appendChild(criar('h3', 'db-refeicao-nome', refeicao.nome));

    const lista = criar('ul', 'db-alimentos');
    refeicao.alimentos.forEach((alimento) => {
      const item = criar('li');
      item.append(
        criar('span', null, alimento.nome),
        criar('span', 'db-alimento-qtd num', `${formatarNumero(alimento.quantidade)} ${alimento.unidade}`)
      );
      lista.appendChild(item);
    });

    const m = refeicao.macros;
    const totais = criar('div', 'db-refeicao-totais num');
    const kcal = criar('span', 'db-refeicao-kcal', formatarNumero(m.calorias));
    kcal.append(' ', criar('span', 'numero-unidade', 'kcal'));
    totais.append(
      kcal,
      criar('span', 'apoio', `P ${formatarNumero(m.proteinasG)} · C ${formatarNumero(m.carboidratosG)} · G ${formatarNumero(m.gordurasG)}`)
    );

    card.append(titulo, lista, totais);
    return card;
  }

  function renderizarRefeicoes(dietaResponse) {
    const dia = dietaResponse.dieta.dias[diaSelecionado];
    elDayPanel.setAttribute('aria-labelledby', `db-dia-${diaSelecionado}`);
    const cards = dia.refeicoes.map(criarRefeicao);
    const extras = dia.refeicoes.slice(REFEICOES_VISIVEIS_CELULAR);

    // "+ 3 refeições: almoço, pré-treino e jantar" (canvas DashboardMobile).
    // O CSS só recolhe abaixo de 720 px; no desktop o botão nem aparece.
    elDayPanel.classList.toggle('db-refeicoes-abertas', refeicoesAbertas);
    if (extras.length > 0 && !refeicoesAbertas) {
      cards.slice(REFEICOES_VISIVEIS_CELULAR).forEach((card) => card.classList.add('db-refeicao-extra'));
      const nomes = extras.map((r) => r.nome.charAt(0).toLowerCase() + r.nome.slice(1));
      const lista = nomes.length > 1 ? `${nomes.slice(0, -1).join(', ')} e ${nomes[nomes.length - 1]}` : nomes[0];
      const botao = criar('button', 'db-mais-refeicoes',
        `+ ${extras.length} ${extras.length === 1 ? 'refeição' : 'refeições'}: ${lista}`);
      botao.type = 'button';
      botao.addEventListener('click', () => {
        refeicoesAbertas = true;
        elDayPanel.classList.add('db-refeicoes-abertas');
        botao.remove();
        // O foco vai para a primeira refeição que apareceu, não se perde.
        const primeira = cards[REFEICOES_VISIVEIS_CELULAR];
        primeira.tabIndex = -1;
        primeira.focus();
      });
      cards.push(botao);
    }
    elDayPanel.replaceChildren(...cards);
  }

  // ---------- Render: como a meta do dia foi calculada ----------

  // Cascata com as partes gravadas na geração (composicaoMeta), que somam
  // exatamente a meta. Cada barra começa onde a anterior parou; as negativas
  // (objetivo, ajuste) andam para trás. Dietas antigas não têm o campo, e o
  // card some.
  function renderizarConta(dietaResponse) {
    const card = $('db-card-conta');
    const dia = dietaResponse.dieta.dias[diaSelecionado];
    const c = dia.composicaoMeta;
    card.hidden = !c;
    if (!c) return;

    $('db-conta-titulo').textContent = `Como sua meta de ${nomeCurtoDia(dia.diaSemana)} foi calculada`;

    const passos = [
      ['Gasto em repouso', c.gastoRepouso],
      ['Dia a dia fora do treino (×1,4)', c.atividadeForaDoTreino],
      ...c.treino.map((t) => [`${t.nome}, ${formatarDecimal(t.horas, 2)} h`, t.kcal])
    ];
    if (c.objetivo !== 0) {
      passos.push([`Objetivo: ${c.objetivo < 0 ? 'perder peso' : 'ganhar peso'}`, c.objetivo]);
    }
    if (c.ajusteProgresso !== 0) {
      const ajuste = dietaResponse.dieta.ajusteAdaptativo;
      const percentual = ajuste && ajuste.percentual
        ? ` (${c.ajusteProgresso < 0 ? '−' : '+'}${formatarPercentualAjuste(ajuste.percentual)})`
        : '';
      passos.push([`Ajuste pelo progresso${percentual}`, c.ajusteProgresso]);
    }
    if (c.pisoCarboidrato !== 0) {
      passos.push(['Mínimo de carboidrato', c.pisoCarboidrato]);
    }

    const topo = passos.reduce((soma, [, v]) => soma + Math.max(0, v), 0) || 1;
    let acumulado = 0;
    const linhas = passos.map(([rotulo, valor], i) => {
      let inicio;
      if (valor >= 0) { inicio = acumulado; acumulado += valor; } else { acumulado += valor; inicio = acumulado; }
      const sinal = i === 0 ? '' : valor >= 0 ? '+' : '−';
      return criarLinhaConta(rotulo, `${sinal}${formatarNumero(Math.abs(valor))}`, inicio / topo, Math.abs(valor) / topo,
        valor < 0 ? 'negativa' : '', i * 140);
    });
    linhas.push(criarLinhaConta('Meta do dia', `${formatarNumero(c.meta)} kcal`, 0, c.meta / topo, 'total', passos.length * 140));

    $('db-conta').replaceChildren(...linhas);
  }

  function criarLinhaConta(rotulo, valor, inicio, largura, tipo, atraso) {
    const linha = criar('div', `db-conta-linha${tipo ? ` db-conta-${tipo}` : ''}`);
    const texto = criar('div', 'db-conta-texto');
    texto.append(criar('span', null, rotulo), criar('span', 'num', valor));
    const trilho = criar('div', 'trilho trilho-g');
    trilho.setAttribute('aria-hidden', 'true');
    const barra = criar('span', 'preenche');
    barra.dataset.anima = 'barra-h';
    barra.style.left = `${(inicio * 100).toFixed(2)}%`;
    barra.style.width = `${(Math.min(1, largura) * 100).toFixed(2)}%`;
    barra.style.setProperty('--atraso', `${atraso}ms`);
    trilho.appendChild(barra);
    linha.append(texto, trilho);
    return linha;
  }

  // ---------- Orquestração ----------

  function renderizarDia(dietaResponse) {
    renderizarMeta(dietaResponse);
    renderizarRefeicoes(dietaResponse);
    renderizarConta(dietaResponse);
    // Trocar de dia redesenha as barras da meta e da conta com os valores novos.
    if (window.RonuMovimento) {
      document.querySelectorAll('.db-meta [data-anima-grupo], #db-conta').forEach((g) => {
        if (g.classList.contains('em-cena')) window.RonuMovimento.redesenhar(g);
      });
    }
  }

  function renderizarVersaoSelecionada() {
    const dietaResponse = historico[versaoSelecionada];
    renderizarAjuste(dietaResponse);
    renderizarVersionSwitch();
    renderizarDayRow(dietaResponse);
    renderizarDia(dietaResponse);
  }

  function renderizarDicaGerar() {
    const agora = Date.now();
    const naJanela = historico
      .map((d) => new Date(d.dataGeracao).getTime())
      .filter((t) => agora - t < JANELA_GERACOES_MS);
    const restantes = Math.max(0, LIMITE_GERACOES_POR_HORA - naJanela.length);

    let texto;
    if (restantes === 0) {
      const libera = new Date(Math.min(...naJanela) + JANELA_GERACOES_MS);
      texto = `Limite da hora atingido. Libera às ${libera.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}`;
    } else if (restantes === LIMITE_GERACOES_POR_HORA) {
      texto = `Até ${LIMITE_GERACOES_POR_HORA} dietas por hora`;
    } else {
      texto = `Ainda dá para gerar ${restantes} nesta hora`;
    }
    // A dica aparece junto do "Gerar" do topo e do fim (celular).
    document.querySelectorAll('[data-dica-gerar]').forEach((el) => { el.textContent = texto; });
  }

  function renderizarTudo() {
    if (historico.length === 0) {
      mostrarSomente(elEmpty);
      return;
    }

    renderizarVersaoSelecionada();
    renderizarDicaGerar();
    mostrarSomente(elContent);
  }

  async function carregarHistorico() {
    const resposta = await ronuFetchAutenticado('/dietas/historico');
    if (!resposta.ok) throw new Error('Não foi possível carregar suas dietas.');
    historico = await resposta.json();
  }

  // ---------- Lateral: objetivo, progresso, treino, alimentação ----------

  async function buscarJson(caminho, opcoes) {
    const resposta = await ronuFetchAutenticado(caminho, opcoes);
    if (!resposta.ok) throw new Error(`Falha em ${caminho}`);
    return resposta.json();
  }

  const NOMES_OBJETIVO = { 'perder peso': 'Perder peso', 'manter peso': 'Manter peso', 'ganhar peso': 'Ganhar peso' };
  const NOMES_ORCAMENTO = { economico: 'Econômico', moderado: 'Moderado', sem_restricao: 'Sem restrição' };
  const LETRAS_DIAS = ['S', 'T', 'Q', 'Q', 'S', 'S', 'D'];
  const NOMES_DIAS = ['segunda', 'terça', 'quarta', 'quinta', 'sexta', 'sábado', 'domingo'];

  function renderizarObjetivo(objetivo, simulacao) {
    if (!objetivo) return;
    $('db-objetivo').textContent = NOMES_OBJETIVO[objetivo.objetivo] || objetivo.objetivo;
    let texto = '';
    if (simulacao) {
      const ritmo = simulacao.ritmoEsperadoKgSemana;
      texto = ritmo === 0
        ? 'Meta igual ao seu gasto, para o peso ficar estável. '
        : `Ritmo esperado: cerca de ${formatarPesoSemana(ritmo)} por semana. `;
    }
    $('db-objetivo-texto').textContent = `${texto}Trocar o objetivo reinicia o ajuste pelo progresso.`;
    $('db-card-objetivo').hidden = false;
  }

  function quandoFoi(data) {
    const inicioDoDia = (d) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
    const dias = Math.round((inicioDoDia(new Date()) - inicioDoDia(data)) / 86400000);
    if (dias <= 0) return 'hoje';
    if (dias === 1) return 'ontem';
    if (dias < 7) return `há ${dias} dias`;
    return `em ${formatarData(data.toISOString())}`;
  }

  // Últimas 4 semanas: a linha é o peso de tendência, os pontos são as
  // pesagens do dia. Com uma pesagem só, não há linha para desenhar.
  function renderizarProgresso(tendencia) {
    if (!tendencia || tendencia.length === 0) return;
    const pontos = tendencia
      .map((p) => ({ data: new Date(p.data), bruto: Number(p.pesoBruto), tendencia: Number(p.pesoTendencia) }))
      .sort((a, b) => a.data - b.data);
    const ultimo = pontos[pontos.length - 1];
    const inicioJanela = ultimo.data.getTime() - 28 * 86400000;
    const janela = pontos.filter((p) => p.data.getTime() >= inicioJanela);

    $('db-peso').textContent = formatarDecimal(ultimo.tendencia);
    $('db-ultima-pesagem').textContent = `Última pesagem: ${quandoFoi(ultimo.data)}`;

    const variacao = ultimo.tendencia - janela[0].tendencia;
    const semanas = Math.max(1, Math.round((ultimo.data - janela[0].data) / (7 * 86400000)));
    $('db-peso-variacao').textContent = janela.length < 2
      ? 'Primeira pesagem'
      : `${variacao > 0 ? '+' : variacao < 0 ? '−' : '±'}${formatarDecimal(Math.abs(variacao))} kg em ${semanas} ${semanas === 1 ? 'semana' : 'semanas'}`;

    const grafico = $('db-mini-grafico');
    const elLinha = $('db-mini-linha');
    const elPontos = $('db-mini-pontos');
    elPontos.innerHTML = '';
    grafico.querySelectorAll('.db-mini-atual').forEach((el) => el.remove());
    if (janela.length < 2) {
      // <svg> não tem a propriedade .hidden do HTML: vai pelo atributo.
      grafico.setAttribute('hidden', '');
    } else {
      const valores = janela.flatMap((p) => [p.bruto, p.tendencia]);
      const min = Math.min(...valores);
      const max = Math.max(...valores);
      const faixa = max - min || 1;
      const t0 = janela[0].data.getTime();
      const dt = ultimo.data.getTime() - t0 || 1;
      const x = (p) => 6 + ((p.data.getTime() - t0) / dt) * 288;
      const y = (v) => 8 + (1 - (v - min) / faixa) * 54;

      elLinha.setAttribute('d', janela.map((p, i) => `${i ? 'L' : 'M'}${x(p).toFixed(1)} ${y(p.tendencia).toFixed(1)}`).join(' '));
      const ns = 'http://www.w3.org/2000/svg';
      janela.forEach((p) => {
        const c = document.createElementNS(ns, 'circle');
        c.setAttribute('cx', x(p).toFixed(1));
        c.setAttribute('cy', y(p.bruto).toFixed(1));
        c.setAttribute('r', '2.5');
        elPontos.appendChild(c);
      });
      const atual = document.createElementNS(ns, 'circle');
      atual.setAttribute('class', 'db-mini-atual');
      atual.setAttribute('cx', x(ultimo).toFixed(1));
      atual.setAttribute('cy', y(ultimo.tendencia).toFixed(1));
      atual.setAttribute('r', '4');
      grafico.appendChild(atual);

      const direcao = variacao < -0.05 ? 'caindo' : variacao > 0.05 ? 'subindo' : 'estável';
      grafico.setAttribute('aria-label', `Peso de tendência ${direcao} nas últimas ${semanas} semanas, de ${formatarDecimal(janela[0].tendencia)} para ${formatarDecimal(ultimo.tendencia)} kg`);
      grafico.removeAttribute('hidden');
    }

    $('db-card-progresso').hidden = false;
  }

  function renderizarTreino(modalidades, simulacao) {
    if (!modalidades) return;
    const lista = $('db-treinos');
    lista.innerHTML = '';

    if (modalidades.length === 0) {
      lista.appendChild(criar('p', 'apoio', 'Nenhuma modalidade cadastrada ainda.'));
    }

    modalidades.forEach((um) => {
      const bloco = criar('div', 'db-treino');
      const cabeca = criar('div', 'db-treino-cabeca');
      cabeca.append(
        criar('span', 'db-treino-nome', um.modalidade.nome),
        criar('span', 'apoio num', `${formatarDecimal(um.duracaoMediaHoras, 2)} h por sessão`)
      );

      const dias = criar('div', 'dias db-treino-dias');
      const nomesDias = [];
      LETRAS_DIAS.forEach((letra, i) => {
        const treina = um.diasSemana.includes(i + 1);
        if (treina) nomesDias.push(NOMES_DIAS[i]);
        const dia = criar('span', treina ? 'dia-treino' : null, letra);
        dia.setAttribute('aria-hidden', 'true');
        dias.appendChild(dia);
      });
      const diasLeitor = criar('span', 'oculto-visual', `Treina ${nomesDias.join(', ')}.`);

      bloco.append(cabeca, dias, diasLeitor);

      const sim = simulacao && simulacao.modalidades.find((m) => m.modalidadeId === um.modalidade.id);
      if (sim) {
        bloco.appendChild(criar('span', 'apoio num',
          `Cerca de ${formatarNumero(sim.kcalSessao)} kcal por sessão, pelo Compêndio de Atividades Físicas.`));
      }
      lista.appendChild(bloco);
    });

    $('db-card-treino').hidden = false;
  }

  function renderizarAlimentacao(perfil, preferencias) {
    if (!perfil && !preferencias) return;
    const chips = $('db-comidas');
    chips.innerHTML = '';

    const lista = preferencias || [];
    // Primeiro o que a pessoa gosta, depois o que evita; no máximo 8 chips.
    const ordenadas = [...lista.filter((p) => p.tipo !== 'evitar'), ...lista.filter((p) => p.tipo === 'evitar')];
    const LIMITE = 8;
    ordenadas.slice(0, LIMITE).forEach((p) => {
      const evitar = p.tipo === 'evitar';
      chips.appendChild(criar('span', `chip chip-pequeno${evitar ? ' chip-evitar' : ''}`,
        evitar ? `evitar: ${p.alimento}` : p.alimento));
    });
    if (ordenadas.length > LIMITE) {
      chips.appendChild(criar('span', 'chip chip-pequeno db-chip-mais', `+${ordenadas.length - LIMITE}`));
    }
    chips.hidden = ordenadas.length === 0;

    $('db-orcamento').textContent = (perfil && NOMES_ORCAMENTO[perfil.orcamentoSemanal]) || 'Não informado';
    $('db-rotina').textContent = (perfil && perfil.rotinaDiaria) || 'Não informada';
    $('db-card-alimentacao').hidden = false;
  }

  async function carregarLateral() {
    const pedidos = await Promise.allSettled([
      buscarJson('/objetivos/atual'),
      buscarJson('/objetivos/tendencia'),
      buscarJson('/usuarios/modalidades'),
      buscarJson('/perfil'),
      buscarJson('/preferencias-alimentares'),
      // Corpo vazio: a simulação usa os dados salvos (peso de tendência,
      // objetivo e modalidades atuais).
      buscarJson('/metas/simulacao', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: '{}'
      })
    ]);
    const [objetivo, tendencia, modalidades, perfil, preferencias, simulacao] =
      pedidos.map((r) => (r.status === 'fulfilled' ? r.value : null));

    // Cada card falha sozinho: um erro ao desenhar um não derruba os outros.
    const tentar = (fn) => { try { fn(); } catch (erro) { /* card fica escondido */ } };
    tentar(() => renderizarObjetivo(objetivo, simulacao));
    tentar(() => renderizarProgresso(tendencia));
    tentar(() => renderizarTreino(modalidades, simulacao));
    tentar(() => renderizarAlimentacao(perfil, preferencias));
  }

  // ---------- Gerar ----------

  async function gerarDieta(botao) {
    // O estado de carregamento só bloqueia o mouse (pointer-events: none) —
    // Enter/Espaço com o botão em foco disparariam outra geração no meio da
    // atual, por isso a ativação é ignorada aqui enquanto ele carrega.
    // Há dois "Gerar" na tela com dieta (topo e fim): um carregando já basta.
    if ([btnGerar, btnGerarFim, btnGerarVazio].some((b) => b.dataset.loading === 'true')) return;

    ronuOcultarErroFormulario(elGerarError);
    ronuDefinirCarregando(botao, true, 'Montando sua dieta da semana...');
    botao.setAttribute('aria-busy', 'true');

    try {
      const resposta = await ronuFetchAutenticado('/dietas/gerar', { method: 'POST' });

      if (!resposta.ok) {
        const corpo = await resposta.json().catch(() => null);
        throw new Error(corpo?.mensagem || 'Não foi possível gerar sua dieta. Tente novamente.');
      }

      await carregarHistorico();
      versaoSelecionada = 0;
      diaSelecionado = 0;
      renderizarTudo();
    } catch (erro) {
      ronuMostrarErroFormulario(elGerarError, erro.message);
      elGerarError.scrollIntoView({ block: 'nearest' });
    } finally {
      ronuDefinirCarregando(botao, false);
      botao.removeAttribute('aria-busy');
    }
  }

  btnGerar.addEventListener('click', () => gerarDieta(btnGerar));
  btnGerarVazio.addEventListener('click', () => gerarDieta(btnGerarVazio));
  btnGerarFim.addEventListener('click', () => gerarDieta(btnGerarFim));

  // ---------- Carga inicial ----------

  async function iniciar() {
    mostrarSomente(elLoading);

    // Cadastro já visto completo neste navegador (marca gravada pela checagem
    // pós-login ou por uma abertura anterior): vai direto ao histórico. Sem a
    // marca (acesso direto, sessão antiga), faz a checagem completa — GETs
    // simples, nunca POST /dietas/gerar, que gastaria cota da Gemini à toa.
    try {
      if (!ronuCadastroJaCompleto() && !(await ronuChecarCadastroCompleto())) {
        window.location.href = 'onboarding.html';
        return;
      }
    } catch (erro) {
      // Em caso de 401, ronuFetchAutenticado já redirecionou pro login
      // sozinho — não sobrescrever esse redirecionamento com outro (mesmo
      // cuidado já tomado em onboarding.js). Qualquer outro erro (rede fora
      // do ar) mostra o estado de erro desta própria tela.
      if (erro.message !== 'Sessão expirada.') {
        mostrarSomente(elLoadError);
      }
      return;
    }

    try {
      await carregarHistorico();
      renderizarTudo();
    } catch (erro) {
      mostrarSomente(elLoadError);
      return;
    }

    carregarLateral();
  }

  btnTentarNovamente.addEventListener('click', iniciar);

  await iniciar();
});
