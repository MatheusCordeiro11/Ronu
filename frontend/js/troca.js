// Ronu — Trocar alimento (painel da dieta)
// O dashboard.js desenha as refeições e chama daqui: abrirOpcoes (o painel com
// os substitutos), abrirOriginal (o "era X" com "Voltar ao original") e
// abrirSemTroca (dieta antiga ou de outra versão da base). Os três usam o mesmo
// <dialog> (showModal prende o foco e o Esc fecha); ao fechar, o foco volta
// para quem abriu ou, depois de uma troca, para o item trocado.
//
// O servidor confere tudo de novo e devolve a dieta inteira com a revisão nova;
// a tela nunca calcula quantidade nem kcal. 409 e 422 querem dizer que a dieta
// mudou por fora (outra aba, dieta nova, base nova): a tela recarrega e avisa.

window.RonuTroca = (function () {
  const MENOS = '−';
  const NOMES_NUTRIENTE = { proteina: 'proteína', carboidrato: 'carboidrato', gordura: 'gordura' };
  const MSG_REDE = 'Não foi possível falar com o Ronu agora. Verifique sua conexão e tente de novo.';
  const MSG_LIMITE = 'Muitos pedidos de troca em pouco tempo. Espere um minuto e tente de novo.';
  const MSG_MUDOU = 'Sua dieta mudou desde que a tela foi aberta. Atualizamos a dieta: confira e tente de novo.';
  const MOTIVOS_SEM_TROCA = {
    'dieta-anterior': 'Ela foi gerada antes da troca de alimentos existir.',
    'outra-versao-da-base': 'A base de alimentos do Ronu mudou desde que ela foi gerada.'
  };
  const ICONE_FECHAR = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18"/></svg>';

  let ctx = null;
  let dialogo = null;
  let elAviso = null;
  let elAnuncio = null;
  // Quem recebe o foco quando o diálogo fecha.
  let aoFechar = null;
  // A posição da última troca, para o foco depois do "Desfazer".
  let ultimaPosicao = null;
  let desfazendo = false;

  // ---------- Formatação ----------

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto != null) el.textContent = texto;
    return el;
  }

  const inteiro = (v) => Math.round(v).toLocaleString('pt-BR');
  const decimal = (v) => Number(v).toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  const minuscula = (texto) => texto.charAt(0).toLowerCase() + texto.slice(1);
  const quantidade = (q, unidade) => `${inteiro(q)} ${unidade}`;
  const nomeDia = (diaSemana) => diaSemana.split('-')[0];

  function sinal(valor) {
    return valor > 0 ? '+' : MENOS;
  }

  function textoDif(dif, sufixo = '') {
    const r = Math.round(dif);
    return r === 0 ? 'mesmas kcal' : `${sinal(r)}${inteiro(Math.abs(r))} kcal${sufixo}`;
  }

  function textoDesvio(pct, sufixo = ' da meta') {
    const r = Math.round(pct * 10) / 10;
    return r === 0 ? 'na meta' : `${sinal(r)}${decimal(Math.abs(r))}%${sufixo}`;
  }

  function juntar(partes) {
    return partes.length === 1 ? partes[0] : `${partes.slice(0, -1).join(', ')} e ${partes[partes.length - 1]}`;
  }

  function plural(n, um, varios) {
    return `${n} ${n === 1 ? um : varios}`;
  }

  // ---------- Pedidos ----------

  // Resposta com o corpo já lido. Erro de rede vira status 0; sessão expirada
  // (o auth.js já mandou para o login) vira null, e ninguém mexe mais na tela.
  async function pedir(caminho, opcoes) {
    try {
      const resposta = await ronuFetchAutenticado(caminho, opcoes);
      const corpo = await resposta.json().catch(() => null);
      return { status: resposta.status, ok: resposta.ok, corpo };
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return null;
      return { status: 0, ok: false, corpo: null };
    }
  }

  // A dieta mudou por fora: fecha o que estiver aberto e recarrega. No 409 a
  // mensagem é da tela (o servidor pede para atualizar, e a tela já atualizou);
  // no 422 vale a do servidor, que diz o porquê (dieta antiga, base nova).
  function recarregar(r) {
    aoFechar = null;
    fecharDialogo();
    fecharAviso({ focar: false });
    ctx.recarregar(r && r.status !== 409 && r.corpo?.mensagem ? r.corpo.mensagem : MSG_MUDOU);
  }

  function mudouPorFora(r) {
    return r.status === 409 || r.status === 422 || r.status === 404;
  }

  function mensagemDeErro(r, padrao) {
    if (r.status === 0) return MSG_REDE;
    if (r.status === 429) return r.corpo?.mensagem || MSG_LIMITE;
    return padrao;
  }

  // As posições em que o alimento ou a quantidade mudou (as que a troca tocou).
  function posicoesMudadas(antes, depois) {
    const mudadas = [];
    depois.dias.forEach((dia, d) => dia.refeicoes.forEach((refeicao, r) => refeicao.alimentos.forEach((a, i) => {
      const b = antes.dias[d]?.refeicoes[r]?.alimentos[i];
      if (!b || b.alimentoId !== a.alimentoId || b.quantidade !== a.quantidade) mudadas.push(`${d}-${r}-${i}`);
    })));
    return mudadas;
  }

  // ---------- Diálogo ----------

  function abrirDialogo(classe, retorno) {
    dialogo.className = `folha ${classe}`;
    aoFechar = retorno;
    if (!dialogo.open) dialogo.showModal();
  }

  function fecharDialogo() {
    if (dialogo.open) dialogo.close();
  }

  function focarOrigem(origem) {
    return () => { if (origem?.isConnected) origem.focus(); };
  }

  // Redesenha o diálogo inteiro e devolve o foco ao mesmo controle (data-foco)
  // e a rolagem do corpo ao mesmo ponto: escolher uma opção não pode jogar o
  // foco para o topo.
  function montar(partes, focarTitulo) {
    const ativo = dialogo.contains(document.activeElement) ? document.activeElement.dataset.foco : null;
    const rolagem = dialogo.querySelector('.folha-corpo')?.scrollTop ?? 0;
    dialogo.replaceChildren(...partes.filter(Boolean));
    const corpo = dialogo.querySelector('.folha-corpo');
    if (corpo) corpo.scrollTop = rolagem;
    const alvo = (ativo && dialogo.querySelector(`[data-foco="${ativo}"]`)) || (focarTitulo || !ativo ? dialogo.querySelector('#troca-titulo') : null);
    alvo?.focus();
  }

  function cabeca(titulo, apoio) {
    const el = criar('header', 'folha-cabeca');
    const texto = criar('div', 'folha-cabeca-texto');
    const h2 = criar('h2', 'folha-titulo', titulo);
    h2.id = 'troca-titulo';
    h2.tabIndex = -1;
    h2.dataset.foco = 'titulo';
    texto.append(h2);
    (apoio || []).filter(Boolean).forEach((linha) => texto.append(criar('p', 'folha-apoio num', linha)));

    const fechar = criar('button', 'folha-fechar');
    fechar.type = 'button';
    fechar.dataset.foco = 'fechar';
    fechar.setAttribute('aria-label', 'Fechar');
    fechar.innerHTML = ICONE_FECHAR;
    fechar.addEventListener('click', fecharDialogo);
    el.append(texto, fechar);
    return el;
  }

  function botao(classe, texto, foco, aoClicar) {
    const b = criar('button', classe, texto);
    b.type = 'button';
    if (foco) b.dataset.foco = foco;
    b.addEventListener('click', aoClicar);
    return b;
  }

  // Grupo de rádios com setas (padrão ARIA): só o marcado (ou o primeiro) fica no Tab.
  function grupoRadio(grupo, aoEscolher) {
    const radios = () => Array.from(grupo.querySelectorAll('[role="radio"]'));
    const lista = radios();
    const marcado = lista.find((r) => r.getAttribute('aria-checked') === 'true') || lista[0];
    lista.forEach((r) => { r.tabIndex = r === marcado ? 0 : -1; });
    grupo.addEventListener('keydown', (evento) => {
      const passo = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[evento.key];
      if (!passo) return;
      evento.preventDefault();
      const todos = radios();
      const atual = todos.indexOf(document.activeElement);
      const proximo = todos[(atual + passo + todos.length) % todos.length];
      const foco = proximo.dataset.foco;
      aoEscolher(proximo.dataset.valor);
      // A escolha redesenha o diálogo: o foco vai para o rádio novo, já no DOM novo.
      dialogo.querySelector(`[data-foco="${foco}"]`)?.focus();
    });
  }

  function radio(classe, valor, marcado, foco, conteudo, aoEscolher) {
    const r = criar('button', classe);
    r.type = 'button';
    r.setAttribute('role', 'radio');
    r.setAttribute('aria-checked', String(marcado));
    r.dataset.valor = valor;
    r.dataset.foco = foco;
    r.append(...conteudo);
    r.addEventListener('click', () => aoEscolher(valor));
    return r;
  }

  function segmentadoEscopo(rotulo, opcoes, escolhido, aoEscolher) {
    const grupo = criar('div', 'segmentado troca-escopo');
    grupo.setAttribute('role', 'radiogroup');
    grupo.setAttribute('aria-label', rotulo);
    opcoes.forEach(([valor, titulo, dica]) => {
      grupo.append(radio('', valor, valor === escolhido, `escopo-${valor}`,
        [criar('span', 'troca-escopo-titulo', titulo), criar('span', 'troca-escopo-dica', dica)], aoEscolher));
    });
    grupoRadio(grupo, aoEscolher);
    return grupo;
  }

  function faixa(desvioPct) {
    const el = criar('span', 'troca-faixa');
    el.setAttribute('aria-hidden', 'true');
    const marca = criar('span', 'troca-faixa-marca');
    marca.style.setProperty('--pos', `${50 + Math.max(-10, Math.min(10, desvioPct)) * 5}%`);
    el.append(criar('span', 'troca-faixa-banda'), criar('span', 'troca-faixa-meta'), marca);
    return el;
  }

  // ---------- Painel das opções ----------

  function abrirOpcoes(alvo) {
    const estado = {
      alvo, dados: null, escopo: 'refeicao', escolha: null, verMais: false,
      carregando: true, erro: null, enviando: false, erroEnvio: null, primeiro: true
    };
    abrirDialogo('folha-lateral', focarOrigem(alvo.origem));
    desenharOpcoes(estado);
    carregarOpcoes(estado);
  }

  async function carregarOpcoes(estado) {
    const { dia, refeicao, item } = estado.alvo;
    estado.carregando = true;
    estado.erro = null;
    desenharOpcoes(estado);

    const r = await pedir(`/dietas/atual/trocas/opcoes?dia=${dia}&refeicao=${refeicao}&item=${item}`);
    if (!r || !dialogo.open) return;
    // 400 aqui é "alimento não encontrado": a tela estava desatualizada.
    if (mudouPorFora(r) || r.status === 400) {
      recarregar(r);
      return;
    }

    estado.carregando = false;
    if (!r.ok) {
      estado.erro = mensagemDeErro(r, 'Não foi possível carregar as opções. Tente de novo.');
    } else {
      estado.dados = r.corpo;
      if (estado.escolha && !opcoesDoEscopo(estado).some((o) => o.alimentoId === estado.escolha)) estado.escolha = null;
    }
    desenharOpcoes(estado);
  }

  function opcoesDoEscopo(estado, escopo = estado.escopo) {
    const e = estado.dados[escopo];
    return [...e.dasPreferencias, ...e.parecidos, ...e.verMais];
  }

  function opcaoEscolhida(estado) {
    return estado.escolha ? opcoesDoEscopo(estado).find((o) => o.alimentoId === estado.escolha) : null;
  }

  function textoNutriente(estado, opcao) {
    const nutriente = estado.dados.nutriente;
    if (nutriente !== 'livre' && opcao.nutrienteG != null) return `${NOMES_NUTRIENTE[nutriente]} ${decimal(opcao.nutrienteG)} g`;
    // Sem nutriente principal (hortaliças, café, chás): fica o peso ou o volume.
    if (opcao.quantidade === estado.dados.original.quantidade) return opcao.unidade === 'ml' ? 'mesmo volume' : 'mesmo peso';
    return '';
  }

  function criarOpcao(estado, opcao, aoEscolher) {
    const semana = estado.escopo === 'semana';
    const nome = criar('span', 'troca-opcao-nome', opcao.nome);
    if (opcao.outroUso) {
      const etiqueta = criar('span', 'troca-etiqueta', 'outro uso');
      etiqueta.append(criar('span', 'oculto-visual', ', costuma ir em outro tipo de refeição'));
      nome.append(etiqueta);
    }
    // À esquerda o quanto (quantidade e nutriente real); à direita as kcal e a diferença.
    const texto = criar('span', 'troca-opcao-texto');
    // Cada parte quebra inteira ("proteína 64,2 g" não perde o "g" na linha de baixo).
    const quanto = criar('span', 'troca-opcao-dica troca-opcao-quanto num');
    [quantidade(opcao.quantidade, opcao.unidade) + (semana ? ' nesta refeição' : ''), textoNutriente(estado, opcao)]
      .filter(Boolean)
      .forEach((parte, i) => quanto.append(...(i ? [' · '] : []), criar('span', null, parte)));
    texto.append(nome, quanto);

    const numeros = criar('span', 'troca-opcao-numeros num');
    numeros.append(criar('span', 'troca-opcao-kcal', `${inteiro(opcao.kcal)} kcal`),
      criar('span', 'troca-opcao-dica', textoDif(opcao.difKcal, semana ? ' na semana' : '')));

    return radio('opcao troca-opcao', opcao.alimentoId, estado.escolha === opcao.alimentoId,
      `opcao-${opcao.alimentoId}`, [texto, numeros], aoEscolher);
  }

  function textoFora(fora, foraPeloEvitar) {
    const partes = [];
    const parte = (n, um, varios) => { if (n > 0) partes.push(n === 1 ? `1 ${um}` : `${n} ${varios}`); };
    parte(fora.porcaoMaxima || 0, 'passaria da porção máxima', 'passariam da porção máxima');
    parte(fora.foraDaMeta || 0, 'deixaria o dia mais de 5% fora da meta', 'deixariam o dia mais de 5% fora da meta');
    parte(foraPeloEvitar, 'está no que você marcou para evitar', 'estão no que você marcou para evitar');
    parte(fora.jaNaRefeicao || 0, 'já está nesta refeição', 'já estão nesta refeição');
    parte((fora.refogadoComOleo || 0) + (fora.usoDiferente || 0), 'não combina com esta refeição', 'não combinam com esta refeição');
    return partes.length ? `${juntar(partes)}.` : null;
  }

  function corpoOpcoes(estado) {
    const corpo = criar('div', 'folha-corpo');
    const { alvo } = estado;

    if (alvo.alimento.original) {
      const o = alvo.alimento.original;
      corpo.append(criar('p', 'nota', `Já trocado: era ${minuscula(o.nome)}, ${quantidade(o.quantidade, o.unidade)}.`));
    }

    if (estado.carregando) {
      const carregando = criar('div', 'troca-carregando');
      carregando.setAttribute('aria-busy', 'true');
      carregando.append(criar('p', null, 'Calculando os substitutos...'), criar('div', 'carregando'));
      corpo.append(carregando);
      return corpo;
    }

    if (estado.erro) {
      const erro = criar('div', 'troca-vazio');
      erro.append(criar('p', 'aviso aviso-erro', estado.erro),
        botao('botao botao-fantasma', 'Tentar de novo', 'tentar', () => carregarOpcoes(estado)));
      corpo.append(erro);
      return corpo;
    }

    const dados = estado.dados;
    const escolher = (valor) => { estado.escolha = valor; estado.erroEnvio = null; desenharOpcoes(estado); };
    const mudarEscopo = (valor) => {
      if (valor === estado.escopo) return;
      estado.escopo = valor;
      estado.erroEnvio = null;
      if (!opcoesDoEscopo(estado).some((o) => o.alimentoId === estado.escolha)) estado.escolha = null;
      desenharOpcoes(estado);
    };

    if (dados.ocorrenciasNaSemana > 1) {
      corpo.append(segmentadoEscopo('Onde trocar', [
        ['refeicao', 'Só nesta refeição', `${alvo.nomeRefeicao} de ${minuscula(nomeDia(alvo.diaSemana))}`],
        ['semana', 'Na semana toda', `${dados.ocorrenciasNaSemana} vezes`]
      ], estado.escopo, mudarEscopo));
    } else {
      corpo.append(criar('p', 'nota', 'Este alimento aparece só nesta refeição na semana.'));
    }

    const escopo = dados[estado.escopo];
    if (escopo.mensagemSemOpcoes) {
      const vazio = criar('div', 'troca-vazio');
      vazio.append(criar('p', 'nota nota-forte', escopo.mensagemSemOpcoes));
      const outro = estado.escopo === 'semana' ? 'refeicao' : 'semana';
      const n = opcoesDoEscopo(estado, outro).length;
      if (dados.ocorrenciasNaSemana > 1 && n > 0) {
        const texto = outro === 'refeicao' ? `Ver ${plural(n, 'opção', 'opções')} só nesta refeição` : `Ver ${plural(n, 'opção', 'opções')} na semana toda`;
        vazio.append(botao('botao-texto', texto, 'outro-escopo', () => mudarEscopo(outro)));
      }
      corpo.append(vazio);
      return corpo;
    }

    const grupo = criar('div', 'troca-opcoes');
    grupo.setAttribute('role', 'radiogroup');
    grupo.setAttribute('aria-label', 'Substitutos');
    const secao = (titulo, opcoes, id) => {
      if (!opcoes.length) return;
      const s = criar('section', 'troca-secao');
      const h3 = criar('h3', 'rotulo troca-secao-titulo', titulo);
      h3.id = `troca-secao-${id}`;
      s.setAttribute('aria-labelledby', h3.id);
      s.append(h3, ...opcoes.map((o) => criarOpcao(estado, o, escolher)));
      grupo.append(s);
    };
    secao('Das suas preferências', escopo.dasPreferencias, 'preferencias');
    secao('Parecidos', escopo.parecidos, 'parecidos');
    if (estado.verMais) secao('Mais opções', escopo.verMais, 'mais');
    grupoRadio(grupo, escolher);
    corpo.append(grupo);

    if (escopo.verMais.length && !estado.verMais) {
      const n = escopo.verMais.length;
      const ver = botao('botao-texto troca-ver-mais', `Ver mais ${plural(n, 'opção', 'opções')}`, 'ver-mais', () => {
        estado.verMais = true;
        desenharOpcoes(estado);
        // O foco vai para a primeira opção que apareceu.
        dialogo.querySelector('#troca-secao-mais')?.parentElement.querySelector('[role="radio"]')?.focus();
      });
      ver.setAttribute('aria-expanded', 'false');
      corpo.append(ver);
    }

    const fora = textoFora(escopo.fora, dados.foraPeloEvitar);
    if (fora) {
      const detalhes = criar('details', 'troca-fora');
      const resumo = criar('summary', null, 'Por que outras opções não aparecem');
      resumo.dataset.foco = 'fora';
      detalhes.append(resumo, criar('p', null, `Ficaram de fora: ${fora}`));
      detalhes.open = !!estado.foraAberto;
      detalhes.addEventListener('toggle', () => { estado.foraAberto = detalhes.open; });
      corpo.append(detalhes);
    }

    // Na semana, o efeito em cada dia afetado, cada um contra a sua meta.
    const opcao = opcaoEscolhida(estado);
    if (opcao && estado.escopo === 'semana') {
      const s = criar('section', 'troca-secao');
      const h3 = criar('h3', 'rotulo troca-secao-titulo', 'Efeito em cada dia');
      h3.id = 'troca-secao-dias';
      s.setAttribute('aria-labelledby', h3.id);
      const lista = criar('ul', 'troca-dias');
      opcao.dias.forEach((d) => {
        const li = criar('li', 'troca-dia num');
        li.append(
          criar('span', 'troca-dia-nome', nomeDia(d.diaSemana).slice(0, 3)),
          criar('span', null, `${inteiro(d.totalAntes)} → ${inteiro(d.totalDepois)} kcal`),
          criar('span', 'troca-dia-pct', textoDesvio(d.desvioPct, '')),
          faixa(d.desvioPct)
        );
        lista.append(li);
      });
      s.append(h3, lista);
      corpo.append(s);
    }

    return corpo;
  }

  function rodapeOpcoes(estado) {
    if (estado.carregando || estado.erro || estado.dados[estado.escopo].mensagemSemOpcoes) return null;
    const rodape = criar('footer', 'folha-rodape');
    const opcao = opcaoEscolhida(estado);

    const previa = criar('div', 'troca-previa');
    if (!opcao) {
      previa.append(criar('p', 'troca-previa-texto', 'Escolha um substituto para ver o total do dia.'));
    } else if (estado.escopo === 'refeicao') {
      const d = opcao.dias[0];
      const texto = criar('p', 'troca-previa-texto num');
      const total = criar('span', null, `${nomeDia(d.diaSemana)}: ${inteiro(d.totalAntes)} → `);
      total.append(criar('strong', null, `${inteiro(d.totalDepois)} kcal`));
      texto.append(total, criar('span', null, textoDesvio(d.desvioPct)));
      const escala = criar('div', 'troca-escala');
      escala.setAttribute('aria-hidden', 'true');
      escala.append(criar('span', null, `${MENOS}5%`), criar('span', null, 'meta'), criar('span', null, '+5%'));
      previa.append(texto, faixa(d.desvioPct), escala);
    } else {
      const n = opcao.dias.length;
      const dentro = opcao.dias.every((d) => Math.abs(d.desvioPct) <= 5);
      const texto = criar('p', 'troca-previa-texto num');
      const resumo = criar('span');
      resumo.append(criar('strong', null, plural(n, 'dia afetado', 'dias afetados')),
        dentro ? (n === 1 ? ', a até 5% da meta' : ', todos a até 5% da meta') : ', nenhum mais longe da meta do que estava');
      texto.append(resumo, criar('span', null, textoDif(opcao.difKcal, ' na semana')));
      previa.append(texto);
    }
    rodape.append(previa);

    if (estado.erroEnvio) {
      const erro = criar('p', 'aviso aviso-erro', estado.erroEnvio);
      erro.setAttribute('role', 'alert');
      rodape.append(erro);
    }

    const texto = estado.enviando ? 'Trocando...' : opcao ? `Trocar por ${minuscula(opcao.nome)}` : 'Escolha um substituto';
    const trocar = botao('botao botao-primario botao-bloco', texto, 'trocar', () => trocarAlimento(estado));
    trocar.disabled = !opcao;
    if (estado.enviando) trocar.setAttribute('aria-busy', 'true');
    rodape.append(trocar);
    return rodape;
  }

  function desenharOpcoes(estado) {
    const { alvo } = estado;
    const a = alvo.alimento;
    const linhas = [`${alvo.nomeRefeicao} de ${minuscula(nomeDia(alvo.diaSemana))}`, quantidade(a.quantidade, a.unidade)];
    const original = estado.dados?.original;
    if (original) {
      linhas[1] += ` · ${inteiro(original.kcal)} kcal`;
      if (original.nutrienteG != null) linhas[1] += ` · ${NOMES_NUTRIENTE[estado.dados.nutriente]} ${decimal(original.nutrienteG)} g`;
    }
    montar([cabeca(`Trocar ${minuscula(a.nome)}`, linhas), corpoOpcoes(estado), rodapeOpcoes(estado)], estado.primeiro);
    estado.primeiro = false;
  }

  async function trocarAlimento(estado) {
    const opcao = opcaoEscolhida(estado);
    if (!opcao || estado.enviando) return;
    const atual = ctx.dietaAtual();
    const { dia, refeicao, item, alimento } = estado.alvo;
    estado.enviando = true;
    estado.erroEnvio = null;
    desenharOpcoes(estado);

    const r = await pedir('/dietas/atual/trocas', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        dietaId: atual.dietaId, revisao: atual.revisao, dia, refeicao, item,
        alimentoIdAtual: alimento.alimentoId, alimentoIdNovo: opcao.alimentoId, escopo: estado.escopo
      })
    });
    if (!r) return;
    estado.enviando = false;

    if (r.ok) {
      const mudadas = posicoesMudadas(atual.dieta, r.corpo.dieta);
      const onde = estado.escopo === 'semana'
        ? `na semana toda (${plural(mudadas.length, 'refeição', 'refeições')})`
        : 'só nesta refeição';
      concluir(r.corpo, mudadas, { dia, refeicao, item },
        `${alimento.nome} trocado por ${minuscula(opcao.nome)} ${onde}.`);
      return;
    }
    if (mudouPorFora(r)) {
      recarregar(r);
      return;
    }
    // 400: o substituto deixou de servir (preferências mudaram em outra aba). As opções vêm de novo.
    if (r.status === 400) {
      estado.erroEnvio = r.corpo?.mensagem || 'Este substituto não serve mais aqui. Escolha outro.';
      carregarOpcoes(estado);
      return;
    }
    estado.erroEnvio = mensagemDeErro(r, 'Não foi possível trocar agora. Tente de novo.');
    desenharOpcoes(estado);
  }

  // Troca, volta ao original: a dieta nova entra na tela, o diálogo fecha com o
  // foco no item e o aviso diz o que mudou (e o que caiu da lista de compras).
  function concluir(resposta, mudadas, posicao, texto) {
    ultimaPosicao = posicao;
    ctx.aplicar(resposta, mudadas);
    aoFechar = () => ctx.focarItem(posicao);
    fecharDialogo();
    mostrarAviso({ texto, marcacoes: resposta.marcacoesRemovidas, desfazer: true });
  }

  // ---------- "Era X" e "Voltar ao original" ----------

  // Na semana, voltam juntos os itens com o mesmo alimento que vieram do mesmo original.
  function vezesNaSemana(dieta, alimento) {
    let n = 0;
    dieta.dias.forEach((dia) => dia.refeicoes.forEach((refeicao) => refeicao.alimentos.forEach((a) => {
      if (a.alimentoId === alimento.alimentoId && a.original?.alimentoId === alimento.original.alimentoId) n++;
    })));
    return n;
  }

  function abrirOriginal(alvo) {
    const estado = { alvo, escopo: 'refeicao', enviando: false, erro: null, primeiro: true };
    abrirDialogo('folha-pequena', focarOrigem(alvo.origem));
    desenharOriginal(estado);
  }

  function desenharOriginal(estado) {
    const { alimento } = estado.alvo;
    const original = alimento.original;
    const vezes = vezesNaSemana(ctx.dietaAtual().dieta, alimento);

    const corpo = criar('div', 'folha-corpo');
    const texto = criar('div', 'troca-original');
    const era = criar('p');
    era.append('Era ', criar('strong', null, minuscula(original.nome)), `, ${quantidade(original.quantidade, original.unidade)}.`);
    texto.append(era, criar('p', 'texto-2', `Agora: ${minuscula(alimento.nome)}, ${quantidade(alimento.quantidade, alimento.unidade)}.`));
    corpo.append(texto);
    if (vezes > 1) {
      corpo.append(segmentadoEscopo('Onde voltar', [
        ['refeicao', 'Só neste item', 'Esta refeição'],
        ['semana', 'Na semana toda', `${vezes} vezes`]
      ], estado.escopo, (valor) => { estado.escopo = valor; estado.erro = null; desenharOriginal(estado); }));
    }

    const rodape = criar('footer', 'folha-rodape');
    if (estado.erro) {
      const erro = criar('p', 'aviso aviso-erro', estado.erro);
      erro.setAttribute('role', 'alert');
      rodape.append(erro);
    }
    const voltar = botao('botao botao-primario botao-bloco', estado.enviando ? 'Voltando...' : 'Voltar ao original', 'voltar',
      () => voltarAoOriginal(estado));
    if (estado.enviando) voltar.setAttribute('aria-busy', 'true');
    rodape.append(voltar);

    montar([cabeca('Antes da troca'), corpo, rodape], estado.primeiro);
    estado.primeiro = false;
  }

  async function voltarAoOriginal(estado) {
    if (estado.enviando) return;
    const atual = ctx.dietaAtual();
    const { dia, refeicao, item, alimento } = estado.alvo;
    estado.enviando = true;
    estado.erro = null;
    desenharOriginal(estado);

    const consulta = new URLSearchParams({ dietaId: atual.dietaId, revisao: atual.revisao, dia, refeicao, item, escopo: estado.escopo });
    const r = await pedir(`/dietas/atual/trocas?${consulta}`, { method: 'DELETE' });
    if (!r) return;
    estado.enviando = false;

    if (r.ok) {
      const mudadas = posicoesMudadas(atual.dieta, r.corpo.dieta);
      const onde = estado.escopo === 'semana'
        ? `na semana toda (${plural(mudadas.length, 'refeição', 'refeições')})`
        : 'só nesta refeição';
      concluir(r.corpo, mudadas, { dia, refeicao, item },
        `${alimento.original.nome} voltou ao lugar de ${minuscula(alimento.nome)} ${onde}.`);
      return;
    }
    // 400 aqui é "não foi trocado": a tela estava desatualizada.
    if (mudouPorFora(r) || r.status === 400) {
      recarregar(r);
      return;
    }
    estado.erro = mensagemDeErro(r, 'Não foi possível voltar ao original agora. Tente de novo.');
    desenharOriginal(estado);
  }

  // ---------- Dieta sem troca ----------

  function abrirSemTroca(motivo, origem) {
    abrirDialogo('folha-pequena', focarOrigem(origem));
    const corpo = criar('div', 'folha-corpo');
    const texto = criar('div', 'troca-original');
    texto.append(
      criar('p', null, MOTIVOS_SEM_TROCA[motivo] || 'Ela não tem as informações que a troca precisa.'),
      criar('p', null, 'Gere uma dieta nova para trocar alimentos. As trocas valem para a dieta atual.')
    );
    const dica = document.querySelector('[data-dica-gerar]')?.textContent;
    if (dica) texto.append(criar('p', 'texto-2', `${dica}.`));
    corpo.append(texto);

    const rodape = criar('footer', 'folha-rodape');
    rodape.append(
      botao('botao botao-primario botao-bloco', 'Gerar dieta nova', 'gerar', () => {
        aoFechar = null;
        fecharDialogo();
        ctx.gerar();
      }),
      botao('botao-texto', 'Agora não', 'agora-nao', fecharDialogo)
    );
    montar([cabeca('Esta dieta não tem troca de alimentos'), corpo, rodape], true);
  }

  // ---------- Aviso com "Desfazer" ----------

  // Uma troca nova substitui o aviso anterior. O aviso não some sozinho: fica
  // até a pessoa fechar, desfazer ou gerar outra dieta.
  function mostrarAviso({ texto, marcacoes, desfazer, erro }) {
    const textos = criar('div', 'troca-aviso-textos');
    textos.append(criar('p', null, texto));
    let compras = '';
    if (marcacoes > 0) {
      compras = `Lista de compras: ${marcacoes === 1
        ? '1 item marcado como "já tenho" voltou para a lista.'
        : `${marcacoes} itens marcados como "já tenho" voltaram para a lista.`}`;
      textos.append(criar('p', 'troca-aviso-compras', compras));
    }
    if (erro) textos.append(criar('p', 'troca-aviso-compras', erro));

    const partes = [criar('span', 'troca-aviso-traco'), textos];
    if (desfazer) partes.push(botao('botao-texto', 'Desfazer', 'desfazer', desfazerUltima));
    const fechar = criar('button', 'folha-fechar');
    fechar.type = 'button';
    fechar.setAttribute('aria-label', 'Fechar aviso');
    fechar.innerHTML = ICONE_FECHAR;
    fechar.addEventListener('click', () => fecharAviso({ focar: true }));
    partes.push(fechar);

    elAviso.replaceChildren(...partes);
    elAviso.hidden = false;
    anunciar(erro || [texto, compras].filter(Boolean).join(' '));
  }

  function fecharAviso({ focar = false } = {}) {
    if (!elAviso || elAviso.hidden) return;
    const tinhaFoco = elAviso.contains(document.activeElement);
    elAviso.hidden = true;
    elAviso.replaceChildren();
    if (focar && tinhaFoco && ultimaPosicao) ctx.focarItem(ultimaPosicao);
  }

  // Limpa antes de escrever, para o leitor de tela anunciar mesmo um texto repetido.
  function anunciar(texto) {
    elAnuncio.textContent = '';
    setTimeout(() => { elAnuncio.textContent = texto; }, 60);
  }

  async function desfazerUltima() {
    if (desfazendo) return;
    const atual = ctx.dietaAtual();
    const botaoDesfazer = elAviso.querySelector('[data-foco="desfazer"]');
    desfazendo = true;
    botaoDesfazer?.setAttribute('aria-busy', 'true');

    const consulta = new URLSearchParams({ dietaId: atual.dietaId, revisao: atual.revisao });
    const r = await pedir(`/dietas/atual/trocas/ultima?${consulta}`, { method: 'DELETE' });
    desfazendo = false;
    if (!r) return;

    if (r.ok) {
      ctx.aplicar(r.corpo, []);
      mostrarAviso({ texto: 'Troca desfeita.', marcacoes: r.corpo.marcacoesRemovidas, desfazer: false });
      if (ultimaPosicao) ctx.focarItem(ultimaPosicao);
      return;
    }
    if (mudouPorFora(r)) {
      recarregar(r);
      return;
    }
    botaoDesfazer?.removeAttribute('aria-busy');
    const texto = elAviso.querySelector('.troca-aviso-textos p')?.textContent || '';
    mostrarAviso({ texto, desfazer: true, erro: mensagemDeErro(r, 'Não foi possível desfazer agora. Tente de novo.') });
    elAviso.querySelector('[data-foco="desfazer"]')?.focus();
  }

  // ---------- Início ----------

  function configurar(contexto) {
    ctx = contexto;
    dialogo = document.getElementById('troca-dialogo');
    elAviso = document.getElementById('troca-aviso');
    elAnuncio = document.getElementById('troca-anuncio');

    dialogo.addEventListener('close', () => {
      const retorno = aoFechar;
      aoFechar = null;
      dialogo.replaceChildren();
      retorno?.();
    });
    // Clique no fundo escurecido fecha (o próprio <dialog> só recebe o clique fora do conteúdo).
    dialogo.addEventListener('click', (evento) => {
      if (evento.target === dialogo) fecharDialogo();
    });
  }

  return { configurar, abrirOpcoes, abrirOriginal, abrirSemTroca, fecharAviso };
})();
