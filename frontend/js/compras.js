// Ronu — Lista de compras (compras.html)
// Fluxo: guarda de sessão -> GET /dietas/atual/lista-compras e GET /perfil (só
// para o fuso da UF) em paralelo -> renderiza. Marcar "já tenho" atualiza a tela
// na hora e manda o PUT; se o PUT falhar, a marcação volta atrás e um aviso
// diz o motivo (sem conexão, a dieta mudou, ou outro erro).
//
// A lista vem pronta da API (quantidade arredondada, texto, nome regional,
// passos da conta): esta tela só organiza, marca e compartilha.

document.addEventListener('DOMContentLoaded', () => {
  if (!ronuUsuarioLogado()) {
    window.location.href = 'login.html';
    return;
  }

  const $ = (id) => document.getElementById(id);

  const elData = $('lc-data');
  const elAcoes = $('lc-acoes');
  const elCarregando = $('lc-carregando');
  const elErro = $('lc-erro');
  const elSemDieta = $('lc-sem-dieta');
  const elAntiga = $('lc-antiga');
  const elConteudo = $('lc-conteudo');
  const elSecoes = $('lc-secoes');
  const elTudoMarcado = $('lc-tudo-marcado');
  const elAvisoOleo = $('lc-aviso-oleo');
  const elAviso = $('lc-aviso');
  const elAvisoTexto = $('lc-aviso-texto');
  const btnAtualizar = $('lc-atualizar');
  const btnEsconder = $('lc-esconder');
  const btnAbrirCompartilhar = $('lc-abrir-compartilhar');
  const elCompartilhar = $('lc-compartilhar');
  const elTextoCompartilhar = $('lc-texto-compartilhar');
  const elLongoDemais = $('lc-longo-demais');
  const btnEnviar = $('lc-enviar');
  const linkWhatsapp = $('lc-whatsapp');
  const btnCopiar = $('lc-copiar');
  const elCopiado = $('lc-copiado');
  const elAnuncio = $('lc-anuncio');

  // Acima disso, alguns navegadores cortam a URL do wa.me (acentos viram %C3%A1).
  const LIMITE_URL_WHATSAPP = 2000;
  const CHAVE_ESCONDER = 'ronu:compras:esconder';

  let lista = null;        // a resposta do GET
  let dataGeracaoTexto = '';
  let esconder = lerEsconder();
  let detalheAberto = null;  // chave do item com o detalhe aberto (um por vez)
  const salvando = new Set(); // chaves com PUT em andamento

  // ---------- Preferência "esconder" (só deste aparelho) ----------
  // O localStorage pode não existir (aba privada, bloqueio de dados): a tela
  // funciona do mesmo jeito, só não lembra a escolha.

  function lerEsconder() {
    try {
      return localStorage.getItem(CHAVE_ESCONDER) === '1';
    } catch {
      return false;
    }
  }

  function gravarEsconder(valor) {
    try {
      localStorage.setItem(CHAVE_ESCONDER, valor ? '1' : '0');
    } catch {
      // Sem armazenamento: vale só até sair da página.
    }
  }

  // ---------- Data no fuso da UF ----------
  // Espelha Services/FusoHorarioEstado.cs: a mesma regra de "dia" do resto do
  // app. Sem UF (ou UF desconhecida), Brasília, como no backend.
  const FUSO_POR_UF = {
    AC: 'America/Rio_Branco',
    AM: 'America/Manaus', RR: 'America/Boa_Vista', RO: 'America/Porto_Velho',
    MT: 'America/Cuiaba', MS: 'America/Campo_Grande',
    PA: 'America/Belem', AP: 'America/Belem', TO: 'America/Araguaina',
    MA: 'America/Fortaleza', PI: 'America/Fortaleza', CE: 'America/Fortaleza',
    RN: 'America/Fortaleza', PB: 'America/Fortaleza', PE: 'America/Recife',
    AL: 'America/Maceio', SE: 'America/Maceio', BA: 'America/Bahia'
  };

  function fusoDaUf(uf) {
    return FUSO_POR_UF[(uf || '').trim().toUpperCase()] || 'America/Sao_Paulo';
  }

  function formatarDia(iso, uf) {
    return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', timeZone: fusoDaUf(uf) })
      .format(new Date(iso));
  }

  // ---------- Números ----------

  function formatarNumero(valor, casas = 2) {
    return Number(valor).toLocaleString('pt-BR', { maximumFractionDigits: casas });
  }

  // 930 g, 1,4 kg, 350 ml, 1,4 L: a mesma regra do texto da API.
  function formatarMedida(valor, medida) {
    if (medida === 'ml') return valor >= 1000 ? `${formatarNumero(valor / 1000)} L` : `${formatarNumero(valor)} ml`;
    return valor >= 1000 ? `${formatarNumero(valor / 1000)} kg` : `${formatarNumero(valor)} g`;
  }

  const UNIDADES = {
    g: ['g', 'g'], ml: ['ml', 'ml'], fatias: ['fatia', 'fatias'], saches: ['sachê', 'sachês']
  };

  // A unidade de um item contável sai do texto da API ("7 bananas" -> "bananas").
  function unidadeDoItem(item, quantidade) {
    if (item.unidade === 'unidades') return item.texto.split(' ').slice(1).join(' ');
    const [singular, plural] = UNIDADES[item.unidade] || [item.unidade, item.unidade];
    return quantidade === 1 ? singular : plural;
  }

  // ---------- Elementos ----------

  function criar(tag, classe, texto) {
    const el = document.createElement(tag);
    if (classe) el.className = classe;
    if (texto !== undefined) el.textContent = texto;
    return el;
  }

  function svg(caminho, viewBox = '0 0 16 16', largura = '1.6') {
    const el = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    el.setAttribute('viewBox', viewBox);
    el.setAttribute('fill', 'none');
    el.setAttribute('stroke', 'currentColor');
    el.setAttribute('stroke-width', largura);
    el.setAttribute('stroke-linecap', 'round');
    el.setAttribute('stroke-linejoin', 'round');
    el.setAttribute('aria-hidden', 'true');
    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    path.setAttribute('d', caminho);
    el.appendChild(path);
    return el;
  }

  // ---------- Linha de apoio do item (S2) ----------

  const PREPAROS = ['cozido', 'cozida', 'grelhado', 'grelhada', 'assado', 'assada', 'refogado', 'refogada', 'frito', 'frita'];

  function linhaDeApoio(item) {
    const origens = item.detalhe.origens;
    if (item.pronto) return 'quantidade do prato pronto';
    if (origens.length > 1) return `de ${origens.length} itens da dieta`;

    const origem = origens[0];
    const quantidade = formatarMedida(origem.quantidadeNaDieta, origem.medida);
    const converte = origem.passos.some((p) => ['par-cru-cozido', 'receita', 'derivacao'].includes(p.tipo));
    if (!converte) return `${quantidade} na semana`;

    const ultima = origem.nome.split(' ').pop().toLowerCase();
    return `para ${quantidade} ${PREPAROS.includes(ultima) ? ultima : 'pronto'}`;
  }

  // ---------- Detalhe ----------

  function montarDetalhe(item) {
    const det = criar('div', 'lc-detalhe');
    det.id = `lc-det-${item.chave}`;

    item.detalhe.origens.forEach((origem) => {
      const bloco = criar('div', 'lc-origem');
      const convertido = Number(origem.convertido);
      const titulo = item.detalhe.origens.length > 1
        ? `${origem.nome}: ${formatarMedida(origem.quantidadeNaDieta, origem.medida)} na dieta, ${formatarNumero(convertido)} ${unidadeDoItem(item, convertido)}`
        : `Na dieta: ${formatarMedida(origem.quantidadeNaDieta, origem.medida)} de ${origem.nome.toLowerCase()} na semana.`;
      bloco.appendChild(criar('span', 'lc-origem-titulo', titulo));

      if (origem.passos.length) {
        const passos = criar('ol', 'lc-passos');
        origem.passos.forEach((passo) => passos.appendChild(criar('li', null, passo.descricao)));
        bloco.appendChild(passos);
      } else {
        bloco.appendChild(criar('span', null, item.pronto
          ? 'Sem fonte para converter em ingrediente cru: a quantidade é a do prato pronto.'
          : 'Já está como se compra: só soma a semana.'));
      }
      det.appendChild(bloco);
    });

    const exato = Number(item.detalhe.quantidadeExata);
    det.appendChild(criar('span', 'lc-resultado',
      `Exato: ${formatarNumero(exato)} ${unidadeDoItem(item, exato)} → ${item.texto}, sempre para cima.`));

    item.detalhe.observacoes.forEach((obs) => det.appendChild(criar('p', 'nota', obs)));
    return det;
  }

  // ---------- Render ----------

  function itensVisiveis(secao) {
    return secao.itens.filter((i) => !esconder || !i.tenho);
  }

  function resumoSecao(secao) {
    const faltam = secao.itens.filter((i) => !i.tenho).length;
    if (faltam === 0) return 'tudo em casa';
    return faltam === 1 ? '1 a comprar' : `${faltam} a comprar`;
  }

  function montarItem(item) {
    const li = criar('li', 'lc-item');
    li.dataset.chave = item.chave;
    li.dataset.marcado = String(item.tenho);

    const linha = criar('div', 'lc-item-linha');

    const check = criar('button', 'lc-check');
    check.type = 'button';
    check.setAttribute('aria-pressed', String(item.tenho));
    check.setAttribute('aria-label', `Já tenho: ${item.nome}`);
    if (salvando.has(item.chave)) check.setAttribute('aria-busy', 'true');
    const caixa = criar('span', 'lc-check-caixa');
    caixa.appendChild(svg('m3 8.5 3.2 3L13 4.5', '0 0 16 16', '2'));
    check.appendChild(caixa);
    check.addEventListener('click', () => alternarTenho(item));

    const abrir = criar('button', 'lc-item-abrir');
    abrir.type = 'button';
    const aberto = detalheAberto === item.chave;
    abrir.setAttribute('aria-expanded', String(aberto));
    abrir.setAttribute('aria-controls', `lc-det-${item.chave}`);

    const texto = criar('span', 'lc-item-texto');
    texto.appendChild(criar('span', 'lc-item-nome', item.nome));
    texto.appendChild(criar('span', 'lc-item-apoio', linhaDeApoio(item)));
    if (item.nota) texto.appendChild(criar('span', 'nota lc-item-nota', item.nota));

    const qtd = criar('span', 'lc-item-qtd');
    qtd.appendChild(criar('span', 'numero lc-qtd', item.texto));
    if (item.pronto) qtd.appendChild(criar('span', 'lc-pronto', 'pronto'));

    const seta = svg('m4 6 4 4 4-4');
    seta.classList.add('lc-seta');

    abrir.append(texto, qtd, seta);
    abrir.addEventListener('click', () => {
      detalheAberto = detalheAberto === item.chave ? null : item.chave;
      renderizarLista();
      document.querySelector(`[data-chave="${item.chave}"] .lc-item-abrir`)?.focus();
    });

    linha.append(check, abrir);
    li.appendChild(linha);

    const det = montarDetalhe(item);
    det.hidden = !aberto;
    li.appendChild(det);
    return li;
  }

  function renderizarLista() {
    const fragmento = document.createDocumentFragment();
    let algumVisivel = false;

    lista.secoes.forEach((secao) => {
      const el = criar('section', 'lc-secao');
      const idTitulo = `lc-secao-${secao.chave}`;
      el.setAttribute('aria-labelledby', idTitulo);

      const cabeca = criar('div', 'lc-secao-cabeca');
      const h2 = criar('h2', null, secao.nome);
      h2.id = idTitulo;
      cabeca.append(h2, criar('span', 'apoio', resumoSecao(secao)));
      el.appendChild(cabeca);

      const visiveis = itensVisiveis(secao);
      if (visiveis.length) {
        algumVisivel = true;
        const ul = criar('ul', 'lc-itens');
        visiveis.forEach((item) => ul.appendChild(montarItem(item)));
        el.appendChild(ul);
      }
      fragmento.appendChild(el);
    });

    elSecoes.replaceChildren(fragmento);
    elTudoMarcado.hidden = algumVisivel;
    renderizarAndamento();
    if (!elCompartilhar.hidden) atualizarCompartilhar();
  }

  function todosItens() {
    return lista.secoes.flatMap((s) => s.itens);
  }

  function renderizarAndamento() {
    const itens = todosItens();
    const feitos = itens.filter((i) => i.tenho).length;
    const faltam = itens.length - feitos;
    const texto = faltam === 0 ? 'Tudo em casa' : faltam === 1 ? 'Falta 1 item' : `Faltam ${faltam} itens`;

    $('lc-faltam').textContent = texto;
    $('lc-feitos').textContent = feitos;
    $('lc-total').textContent = itens.length;
    $('lc-preenche').style.width = `${itens.length ? Math.round((feitos / itens.length) * 100) : 0}%`;
    elAnuncio.textContent = texto;
  }

  function renderizarEsconder() {
    btnEsconder.setAttribute('aria-pressed', String(esconder));
    btnEsconder.textContent = esconder ? 'Mostrar marcados' : 'Esconder marcados';
  }

  function mostrarSomente(visivel) {
    [elCarregando, elErro, elSemDieta, elAntiga, elConteudo].forEach((el) => {
      el.hidden = el !== visivel;
    });
    elAcoes.hidden = visivel !== elConteudo;
  }

  // ---------- Avisos ----------

  function mostrarAviso(texto, { comAtualizar = false } = {}) {
    elAvisoTexto.textContent = texto;
    btnAtualizar.hidden = !comAtualizar;
    elAviso.hidden = false;
  }

  function esconderAviso() {
    elAviso.hidden = true;
  }

  // ---------- Marcar "já tenho" ----------

  // Com "esconder" ligado, o item marcado some: o foco vai para a caixa do
  // item seguinte (ou do anterior), para não se perder no topo da página.
  function vizinhoParaFoco(chave) {
    const caixas = Array.from(document.querySelectorAll('.lc-check'));
    const indice = caixas.findIndex((c) => c.closest('.lc-item').dataset.chave === chave);
    const vizinho = caixas[indice + 1] || caixas[indice - 1];
    return vizinho ? vizinho.closest('.lc-item').dataset.chave : null;
  }

  function focarCaixa(chave) {
    const alvo = chave && document.querySelector(`[data-chave="${chave}"] .lc-check`);
    if (alvo) alvo.focus();
    else if (!elTudoMarcado.hidden) elTudoMarcado.focus();
    else btnEsconder.focus();
  }

  async function alternarTenho(item) {
    if (salvando.has(item.chave)) return;

    const novo = !item.tenho;
    const vaiSumir = esconder && novo;
    const proximo = vaiSumir ? vizinhoParaFoco(item.chave) : item.chave;

    item.tenho = novo;
    salvando.add(item.chave);
    renderizarLista();
    focarCaixa(proximo);

    let resposta;
    try {
      resposta = await ronuFetchAutenticado(`/dietas/atual/lista-compras/itens/${encodeURIComponent(item.chave)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dietaId: lista.dietaId, tenho: novo })
      });
    } catch (erro) {
      // O fetch só rejeita em falha de rede (ou na sessão expirada, que já saiu da página).
      desfazer(item, `Sem conexão: ${item.nome} não foi salvo. Tente de novo.`);
      return;
    }

    salvando.delete(item.chave);
    if (resposta.ok) {
      esconderAviso();
      renderizarLista();
      return;
    }
    if (resposta.status === 409) {
      desfazer(item, 'Sua dieta mudou desde que você abriu a lista. Atualize para ver a lista nova.', true);
      return;
    }
    desfazer(item, `Não foi possível salvar ${item.nome}. Tente de novo.`);
  }

  function desfazer(item, mensagem, comAtualizar = false) {
    salvando.delete(item.chave);
    item.tenho = !item.tenho;
    renderizarLista();
    mostrarAviso(mensagem, { comAtualizar });
  }

  // ---------- Compartilhar ----------

  // Só o que falta, sem dado de saúde (S7).
  function textoParaCompartilhar() {
    const linhas = [`Lista de compras · dieta de ${dataGeracaoTexto}`];
    let algum = false;
    lista.secoes.forEach((secao) => {
      const faltam = secao.itens.filter((i) => !i.tenho);
      if (!faltam.length) return;
      algum = true;
      linhas.push('', secao.nome.toUpperCase());
      faltam.forEach((i) => {
        let linha = `• ${i.nome}: ${i.texto}`;
        if (i.pronto) linha += ' (pronto)';
        if (i.nota) linha += ` (${i.nota.split(' · ')[0]})`;
        linhas.push(linha);
      });
    });
    if (!algum) linhas.push('', 'Nada a comprar: tudo marcado como já tenho.');
    return linhas.join('\n');
  }

  function atualizarCompartilhar() {
    const texto = textoParaCompartilhar();
    elTextoCompartilhar.textContent = texto;

    const temShare = typeof navigator.share === 'function';
    const url = `https://wa.me/?text=${encodeURIComponent(texto)}`;
    const longoDemais = !temShare && url.length > LIMITE_URL_WHATSAPP;

    btnEnviar.hidden = !temShare;
    linkWhatsapp.hidden = temShare || longoDemais;
    linkWhatsapp.href = longoDemais ? '#' : url;
    elLongoDemais.hidden = !longoDemais;
    // Sem um caminho direto, o "Copiar" vira a ação principal.
    btnCopiar.classList.toggle('botao-primario', longoDemais);
    btnCopiar.classList.toggle('botao-fantasma', !longoDemais);
  }

  function abrirCompartilhar() {
    atualizarCompartilhar();
    elCopiado.textContent = '';
    btnCopiar.textContent = 'Copiar texto';
    elCompartilhar.hidden = false;
    btnAbrirCompartilhar.setAttribute('aria-expanded', 'true');
    $('lc-compartilhar-titulo').focus();
  }

  function fecharCompartilhar() {
    elCompartilhar.hidden = true;
    btnAbrirCompartilhar.setAttribute('aria-expanded', 'false');
    btnAbrirCompartilhar.focus();
  }

  btnAbrirCompartilhar.addEventListener('click', () => {
    if (elCompartilhar.hidden) abrirCompartilhar();
    else fecharCompartilhar();
  });
  $('lc-fechar-compartilhar').addEventListener('click', fecharCompartilhar);
  elCompartilhar.addEventListener('keydown', (evento) => {
    if (evento.key === 'Escape') fecharCompartilhar();
  });

  btnEnviar.addEventListener('click', async () => {
    try {
      await navigator.share({ text: textoParaCompartilhar() });
    } catch (erro) {
      // Cancelar a folha do aparelho não é erro; o resto cai no "Copiar".
      if (erro && erro.name !== 'AbortError') btnCopiar.focus();
    }
  });

  let tempoCopiado;
  btnCopiar.addEventListener('click', async () => {
    let copiou = false;
    try {
      await navigator.clipboard.writeText(textoParaCompartilhar());
      copiou = true;
    } catch {
      // Sem a API de área de transferência: seleciona o texto para a pessoa copiar.
      const intervalo = document.createRange();
      intervalo.selectNodeContents(elTextoCompartilhar);
      const selecao = window.getSelection();
      selecao.removeAllRanges();
      selecao.addRange(intervalo);
    }
    btnCopiar.textContent = copiou ? 'Copiado' : 'Texto selecionado';
    elCopiado.textContent = copiou ? 'Texto copiado.' : 'Texto selecionado: use o copiar do aparelho.';
    clearTimeout(tempoCopiado);
    tempoCopiado = setTimeout(() => { btnCopiar.textContent = 'Copiar texto'; }, 2000);
  });

  // ---------- Esconder ----------

  btnEsconder.addEventListener('click', () => {
    esconder = !esconder;
    gravarEsconder(esconder);
    renderizarEsconder();
    renderizarLista();
  });

  // ---------- Carregar ----------

  async function buscarUf() {
    try {
      const resposta = await ronuFetchAutenticado('/perfil');
      return resposta.ok ? (await resposta.json()).estado : null;
    } catch {
      return null;
    }
  }

  async function carregar() {
    esconderAviso();
    mostrarSomente(elCarregando);
    elData.hidden = true;

    let resposta;
    let uf;
    try {
      [resposta, uf] = await Promise.all([ronuFetchAutenticado('/dietas/atual/lista-compras'), buscarUf()]);
    } catch {
      mostrarSomente(elErro);
      return;
    }

    if (resposta.status === 404) {
      mostrarSomente(elSemDieta);
      return;
    }
    if (!resposta.ok) {
      mostrarSomente(elErro);
      return;
    }

    lista = await resposta.json();
    dataGeracaoTexto = formatarDia(lista.dataGeracao, uf);
    elData.textContent = `Dieta gerada em ${dataGeracaoTexto}`;
    elData.hidden = false;

    if (!lista.disponivel) {
      mostrarSomente(elAntiga);
      return;
    }

    detalheAberto = null;
    elAvisoOleo.hidden = !lista.avisoOleo;
    renderizarEsconder();
    renderizarLista();
    mostrarSomente(elConteudo);
  }

  $('lc-tentar-novamente').addEventListener('click', carregar);
  btnAtualizar.addEventListener('click', () => {
    fecharCompartilharSemFoco();
    carregar();
  });

  function fecharCompartilharSemFoco() {
    elCompartilhar.hidden = true;
    btnAbrirCompartilhar.setAttribute('aria-expanded', 'false');
  }

  elTudoMarcado.tabIndex = -1;
  carregar();
});
