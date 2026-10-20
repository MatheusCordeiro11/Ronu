// Ronu — Onboarding (perfil, objetivo, treino, alimentação e a primeira dieta)
//
// Tela aprovada: docs/redesign/telas/Onboarding. Cinco passos; cada um só
// avança depois de salvar com sucesso na API:
//   0 Perfil       PUT  /perfil (sexo, altura, nascimento). O peso fica na tela
//                  e vai no passo seguinte, junto com o objetivo.
//   1 Objetivo     POST /objetivos (peso + objetivo): a primeira pesagem do
//                  Progresso. No mesmo dia, um novo POST atualiza o registro.
//   2 Treino       POST /usuarios/modalidades por modalidade escolhida e DELETE
//                  das que estavam salvas e foram desmarcadas.
//   3 Alimentação  POST /preferencias-alimentares por alimento marcado e DELETE
//                  dos que foram desmarcados (opcional: "Pular por agora").
//   4 Fim          POST /dietas/gerar e, deu certo, vai para o dashboard.
// A conta da meta ao lado ("Sua meta se formando") vem da simulação da API
// (POST /api/metas/simulacao), com o que já foi respondido, a cada mudança.
// Os dados já salvos são carregados no começo: quem saiu no meio retoma com
// os passos preenchidos.

document.addEventListener('DOMContentLoaded', async () => {
  if (!ronuUsuarioLogado()) {
    window.location.href = 'login.html';
    return;
  }

  // Cadastro completo (ex.: voltou a esta URL à toa): vai para o dashboard.
  // Com a marca de cadastro completo (ronuCadastroJaCompleto, auth.js), nem
  // precisa consultar a API.
  try {
    const completo = ronuCadastroJaCompleto() || await ronuChecarCadastroCompleto();
    if (completo) {
      window.location.href = 'dashboard.html';
      return;
    }
  } catch (erro) {
    // 401 já redirecionou para o login. Qualquer outro erro (API fora do ar,
    // mesmo depois da nova tentativa) NÃO abre o assistente: sem saber se o
    // cadastro está completo, refazer o perfil poderia sobrescrever dados. O
    // dashboard tem a tela de erro com "Tentar novamente".
    if (erro.message !== 'Sessão expirada.') {
      window.location.href = 'dashboard.html';
    }
    return;
  }

  const $ = (id) => document.getElementById(id);
  const pagina = $('ob-pagina');
  const formError = $('form-error');
  const btnVoltar = $('btn-voltar');
  const btnAvancar = $('btn-avancar');
  const btnPular = $('btn-pular');
  const numero = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 });
  const fmt = (v) => numero.format(Math.round(v));
  // Sinal de menos tipográfico (−), como nas telas aprovadas.
  const sinal = (v) => (v < 0 ? '− ' : '+ ') + fmt(Math.abs(v));

  const FAIXAS = ['Faixa branca', 'Faixa azul', 'Faixa roxa', 'Faixa marrom', 'Faixa preta'];
  const DIAS = [
    [1, 'Seg', 'segunda-feira'], [2, 'Ter', 'terça-feira'], [3, 'Qua', 'quarta-feira'], [4, 'Qui', 'quinta-feira'],
    [5, 'Sex', 'sexta-feira'], [6, 'Sáb', 'sábado'], [7, 'Dom', 'domingo']
  ];
  const NOMES_OBJETIVO = { 'perder peso': 'perder peso', 'manter peso': 'manter peso', 'ganhar peso': 'ganhar peso' };
  const COMIDAS_BASE = ['frango', 'arroz', 'feijão', 'ovo', 'banana', 'aveia', 'cuscuz', 'tapioca', 'batata-doce',
    'carne moída', 'peixe', 'iogurte', 'leite', 'queijo', 'pão francês', 'camarão', 'mandioca', 'abacate'];
  const ESTADOS = {
    AC: 'Acre', AL: 'Alagoas', AP: 'Amapá', AM: 'Amazonas', BA: 'Bahia', CE: 'Ceará', DF: 'Distrito Federal',
    ES: 'Espírito Santo', GO: 'Goiás', MA: 'Maranhão', MT: 'Mato Grosso', MS: 'Mato Grosso do Sul', MG: 'Minas Gerais',
    PA: 'Pará', PB: 'Paraíba', PR: 'Paraná', PE: 'Pernambuco', PI: 'Piauí', RJ: 'Rio de Janeiro', RN: 'Rio Grande do Norte',
    RS: 'Rio Grande do Sul', RO: 'Rondônia', RR: 'Roraima', SC: 'Santa Catarina', SP: 'São Paulo', SE: 'Sergipe', TO: 'Tocantins'
  };
  // Duração da sessão: de 15 min a 5 h (faixa da API), de 15 em 15. Começa em 1h30.
  const DURACAO_MIN = 15;
  const DURACAO_MAX = 300;
  const DURACAO_PADRAO = 90;
  // Ordem dos grupos do catálogo, igual à da API (CategoriasModalidade.Ordem).
  const CATEGORIAS = ['Lutas', 'Esportes', 'Condicionamento', 'Corpo e movimento'];

  const estado = {
    passo: 0,
    perfilSalvo: null,
    sexo: null,
    objetivo: null,
    catalogo: [],
    semTreino: false,             // marcou "Não treino no momento" (nenhuma modalidade)
    escolhidas: new Map(),        // modalidadeId → { dias: Set, minutos }
    modalidadesSalvas: new Map(), // modalidadeId → id do vínculo (para o DELETE)
    comidas: new Map(),           // alimento (minúsculo) → 'gosto' | 'evitar'
    preferenciasSalvas: new Map(), // alimento (minúsculo) → { id, tipo }
    simulacao: null
  };

  // ---------- Carga inicial ----------
  // Tudo em paralelo; o que falhar (fora o 401, que já redireciona) só deixa
  // de pré-preencher. O catálogo é público.
  const json = async (promessa) => {
    const resposta = await promessa;
    if (!resposta.ok) throw Object.assign(new Error(), { status: resposta.status });
    return resposta.json();
  };
  const [perfilR, objetivoR, catalogoR, minhasR, prefsR] = await Promise.allSettled([
    json(ronuFetchAutenticado('/perfil')),
    json(ronuFetchAutenticado('/objetivos/atual')),
    json(fetch(`${RONU_CONFIG.API_BASE}/modalidades`)),
    json(ronuFetchAutenticado('/usuarios/modalidades')),
    json(ronuFetchAutenticado('/preferencias-alimentares'))
  ]);
  if ([perfilR, objetivoR, minhasR, prefsR].some((r) => r.status === 'rejected' && r.reason?.message === 'Sessão expirada.')) return;

  if (perfilR.status === 'fulfilled') {
    const p = perfilR.value;
    estado.perfilSalvo = p;
    if (p.sexo) estado.sexo = p.sexo;
    if (p.altura != null) $('altura').value = String(Math.round(p.altura));
    if (p.dataNascimento) $('data-nascimento').value = p.dataNascimento;
  }
  if (objetivoR.status === 'fulfilled') {
    $('peso').value = String(objetivoR.value.peso);
    if (NOMES_OBJETIVO[objetivoR.value.objetivo]) estado.objetivo = objetivoR.value.objetivo;
  }
  if (minhasR.status === 'fulfilled') {
    minhasR.value.forEach((v) => {
      estado.escolhidas.set(v.modalidade.id, { dias: new Set(v.diasSemana), minutos: Math.round(v.duracaoMediaHoras * 60) });
      estado.modalidadesSalvas.set(v.modalidade.id, v.id);
    });
  }
  if (prefsR.status === 'fulfilled') {
    prefsR.value.forEach((pref) => {
      const chave = pref.alimento.trim().toLowerCase();
      estado.preferenciasSalvas.set(chave, { id: pref.id, tipo: pref.tipo });
      estado.comidas.set(chave, pref.tipo === 'evitar' ? 'evitar' : 'gosto');
    });
  }
  if (catalogoR.status === 'fulfilled') estado.catalogo = catalogoR.value;

  pagina.hidden = false;

  // ---------- Simulação da meta ----------
  function modalidadesParaApi() {
    return [...estado.escolhidas.entries()]
      .filter(([, m]) => m.dias.size > 0)
      .map(([id, m]) => ({ modalidadeId: id, diasSemana: [...m.dias].sort(), duracaoMediaHoras: m.minutos / 60 }));
  }

  function corpoSimulacao(objetivo) {
    return {
      sexo: estado.sexo,
      altura: Number($('altura').value),
      dataNascimento: $('data-nascimento').value,
      peso: Number($('peso').value),
      objetivo,
      // Sempre explícito: antes do passo Treino, sem treino nenhum.
      modalidades: estado.passo >= 2 ? modalidadesParaApi() : []
    };
  }

  async function chamarSimulacao(corpo) {
    const resposta = await ronuFetchAutenticado('/metas/simulacao', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(corpo)
    });
    if (!resposta.ok) throw new Error();
    return resposta.json();
  }

  let ultimaSimulacao = 0;
  let esperaSimulacao = null;
  function agendarSimulacao() {
    clearTimeout(esperaSimulacao);
    esperaSimulacao = setTimeout(simular, 300);
  }

  async function simular() {
    if (estado.passo < 1) {
      desenharMeta(null);
      return;
    }
    const minha = ++ultimaSimulacao;
    try {
      const dados = await chamarSimulacao(corpoSimulacao(estado.objetivo || 'manter peso'));
      // Resposta atrasada de uma mudança anterior: descarta.
      if (minha !== ultimaSimulacao) return;
      estado.simulacao = dados;
      desenharMeta(dados);
      atualizarResumosTreino();
    } catch (erro) {
      // Sem a simulação, a conta fica como estava; ela não impede de avançar.
    }
  }

  function desenharMeta(d) {
    const linhas = $('ob-meta-linhas');
    const totais = $('ob-meta-totais');
    const celular = $('ob-meta-celular');

    if (!d) {
      linhas.innerHTML = '';
      $('ob-meta-vazia').hidden = false;
      totais.hidden = true;
      celular.hidden = true;
      return;
    }

    const diaTreino = d.dias.reduce((max, dia) => (dia.treinoKcal > max.treinoKcal ? dia : max), d.dias[0]);
    const diaDescanso = d.dias.find((dia) => dia.treinoKcal === 0);
    const temTreino = estado.passo >= 2 && diaTreino.treinoKcal > 0;
    const referencia = diaDescanso || d.dias[0];

    const partes = [
      ['Gasto parado', `${fmt(d.gastoRepouso.total)} kcal`],
      ['Dia a dia', `${sinal(d.atividadeForaDoTreino.kcal)} kcal`]
    ];
    if (estado.objetivo) {
      const v = referencia.objetivoKcal;
      partes.push([`Objetivo: ${NOMES_OBJETIVO[estado.objetivo]}`, v === 0 ? '± 0 kcal' : `${sinal(v)} kcal`]);
    }
    if (temTreino) partes.push(['Treino, no dia que tem', `${sinal(diaTreino.treinoKcal)} kcal`]);

    // Atualiza no lugar: só a parte que acabou de entrar na conta anima
    // (recriar tudo a cada resposta fazia as linhas piscarem sem parar).
    const existentes = new Map([...linhas.children].map((l) => [l.dataset.parte, l]));
    const ordem = partes.map(([rotulo, valor], i) => {
      const chave = rotulo.startsWith('Objetivo') ? 'objetivo' : rotulo;
      let linha = existentes.get(chave);
      if (!linha) {
        linha = document.createElement('div');
        linha.className = 'ob-meta-linha ob-entrando';
        linha.addEventListener('animationend', () => linha.classList.remove('ob-entrando'), { once: true });
        linha.dataset.parte = chave;
        linha.append(document.createElement('span'), document.createElement('span'));
      }
      linha.children[0].textContent = rotulo;
      linha.children[1].textContent = valor;
      linha.classList.toggle('ob-nova', i === partes.length - 1);
      existentes.delete(chave);
      return linha;
    });
    existentes.forEach((l) => l.remove());
    // Só move o que está fora do lugar: reinserir um elemento no DOM
    // reinicia a animação de entrada dele.
    ordem.forEach((l, i) => {
      if (linhas.children[i] !== l) linhas.insertBefore(l, linhas.children[i] || null);
    });
    $('ob-meta-vazia').hidden = true;

    // Totais só depois de escolher o objetivo: antes disso, seriam de "manter".
    const mostrarTotais = Boolean(estado.objetivo);
    totais.hidden = !mostrarTotais;
    celular.hidden = !mostrarTotais || estado.passo >= 4;
    if (!mostrarTotais) return;

    const totalA = temTreino ? diaTreino.metaFormula : referencia.metaFormula;
    $('ob-total-a-rotulo').textContent = temTreino ? 'Dia de treino' : 'Meta por dia';
    $('ob-total-a').textContent = fmt(totalA);
    $('ob-cel-a').textContent = fmt(totalA);
    $('ob-cel-a-rotulo').textContent = temTreino ? 'treino' : 'por dia';
    const mostrarB = temTreino && Boolean(diaDescanso);
    $('ob-total-b-bloco').hidden = !mostrarB;
    $('ob-cel-b-bloco').hidden = !mostrarB;
    if (mostrarB) {
      $('ob-total-b').textContent = fmt(diaDescanso.metaFormula);
      $('ob-cel-b').textContent = fmt(diaDescanso.metaFormula);
    }
  }

  // ---------- Passos ----------
  function mostrarPasso(passo) {
    estado.passo = passo;
    pagina.dataset.passo = String(passo);
    $('ob-faixa-rotulo').textContent = FAIXAS[passo];

    document.querySelectorAll('.ob-passo').forEach((secao) => {
      secao.hidden = Number(secao.dataset.passo) !== passo;
    });
    document.querySelectorAll('#ob-etapas [data-etapa]').forEach((item) => {
      const i = Number(item.dataset.etapa);
      item.classList.toggle('feita', i < passo);
      if (i === passo) item.setAttribute('aria-current', 'step');
      else item.removeAttribute('aria-current');
    });

    ronuOcultarErroFormulario(formError);
    btnVoltar.disabled = passo === 0;
    btnVoltar.setAttribute('aria-hidden', String(passo === 0));
    btnPular.hidden = passo !== 3;
    $('ob-nav').hidden = passo === 4;

    if (passo === 1) carregarAjustesObjetivo();
    if (passo === 4) prepararFim();

    const titulo = document.querySelector(`.ob-passo[data-passo="${passo}"] h1`);
    if (titulo) titulo.focus({ preventScroll: true });
    window.scrollTo({ top: 0 });
    agendarSimulacao();
  }

  function erro(mensagem, campoId) {
    ronuMostrarErroFormulario(formError, mensagem);
    if (campoId) {
      $(campoId).closest('.campo')?.classList.add('field-error');
      $(campoId).focus();
    }
  }

  async function enviar(caminho, metodo, corpo) {
    const resposta = await ronuFetchAutenticado(caminho, {
      method: metodo,
      headers: { 'Content-Type': 'application/json' },
      body: corpo === undefined ? undefined : JSON.stringify(corpo)
    });
    if (!resposta.ok && !(metodo === 'DELETE' && resposta.status === 404)) {
      const dados = await resposta.json().catch(() => null);
      throw new Error(dados?.mensagem || 'Não foi possível salvar. Tente novamente.');
    }
    return resposta.status === 204 ? null : resposta.json().catch(() => null);
  }

  // ----- Passo 0: perfil -----
  function marcarRadios(grupoId, valor) {
    $(grupoId).querySelectorAll('[role="radio"]').forEach((b) => {
      b.setAttribute('aria-checked', String(b.dataset.valor === valor));
      b.tabIndex = b.dataset.valor === valor || (!valor && b === b.parentElement.firstElementChild) ? 0 : -1;
    });
  }

  // Grupo de rádios feito de botões: clique e setas do teclado.
  function ligarRadios(grupoId, aoEscolher) {
    const grupo = $(grupoId);
    const botoes = [...grupo.querySelectorAll('[role="radio"]')];
    grupo.addEventListener('click', (evento) => {
      const botao = evento.target.closest('[role="radio"]');
      if (botao) aoEscolher(botao.dataset.valor);
    });
    grupo.addEventListener('keydown', (evento) => {
      const passo = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[evento.key];
      if (!passo) return;
      evento.preventDefault();
      const atual = botoes.indexOf(document.activeElement);
      const proximo = botoes[(atual + passo + botoes.length) % botoes.length];
      proximo.focus();
      aoEscolher(proximo.dataset.valor);
    });
  }

  ligarRadios('ob-sexo', (valor) => {
    estado.sexo = valor;
    marcarRadios('ob-sexo', valor);
  });
  marcarRadios('ob-sexo', estado.sexo);

  // Faixa de idade da API (RegrasDadosCorporais): 18 a 100 anos.
  const hoje = new Date();
  const dataIso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  $('data-nascimento').max = dataIso(new Date(hoje.getFullYear() - 18, hoje.getMonth(), hoje.getDate()));
  $('data-nascimento').min = dataIso(new Date(hoje.getFullYear() - 100, hoje.getMonth(), hoje.getDate()));

  ['altura', 'peso', 'data-nascimento'].forEach((id) => {
    $(id).addEventListener('input', () => $(id).closest('.campo').classList.remove('field-error'));
  });

  async function avancarPerfil() {
    const altura = Number($('altura').value);
    const peso = Number($('peso').value);
    const nascimento = $('data-nascimento').value;

    if (!estado.sexo) return erro('Selecione seu sexo.');
    if (!$('altura').value || altura < 100 || altura > 250) return erro('Informe uma altura entre 100 e 250 cm.', 'altura');
    if (!$('peso').value || peso < 20 || peso > 300) return erro('Informe um peso entre 20 e 300 kg.', 'peso');
    if (!nascimento) return erro('Informe sua data de nascimento.', 'data-nascimento');
    if (nascimento > $('data-nascimento').max) return erro('O Ronu é para maiores de 18 anos.', 'data-nascimento');
    if (nascimento < $('data-nascimento').min) return erro('Informe uma data de nascimento de até 100 anos atrás.', 'data-nascimento');

    // O PUT grava tudo junto: rotina e orçamento já salvos vão de volta como
    // estavam, para não serem apagados (lacuna "Duas abas abertas…").
    await enviar('/perfil', 'PUT', {
      altura,
      sexo: estado.sexo,
      dataNascimento: nascimento,
      rotinaDiaria: estado.perfilSalvo?.rotinaDiaria ?? null,
      orcamentoSemanal: estado.perfilSalvo?.orcamentoSemanal ?? null
    });
    mostrarPasso(1);
  }

  // ----- Passo 1: objetivo -----
  ligarRadios('ob-objetivos', (valor) => {
    estado.objetivo = valor;
    marcarRadios('ob-objetivos', valor);
    agendarSimulacao();
  });
  marcarRadios('ob-objetivos', estado.objetivo);

  // O ajuste de cada objetivo, pela mesma conta da dieta (com o teto do
  // déficit), sem treino: o número que aparece na direita de cada opção.
  async function carregarAjustesObjetivo() {
    const botoes = [...$('ob-objetivos').querySelectorAll('[role="radio"]')];
    try {
      const resultados = await Promise.all(botoes.map((b) => chamarSimulacao({ ...corpoSimulacao(b.dataset.valor), modalidades: [] })));
      botoes.forEach((b, i) => {
        const v = resultados[i].dias[0].objetivoKcal;
        b.querySelector('[data-ajuste]').textContent = v === 0 ? '± 0' : `${sinal(v)} kcal/dia`;
      });
    } catch (falha) {
      botoes.forEach((b) => { b.querySelector('[data-ajuste]').textContent = ''; });
    }
  }

  async function avancarObjetivo() {
    if (!estado.objetivo) return erro('Selecione um objetivo.');
    await enviar('/objetivos', 'POST', { peso: Number($('peso').value), objetivo: estado.objetivo });
    mostrarPasso(2);
  }

  // ----- Passo 2: treino -----
  const cartoes = new Map(); // modalidadeId → { el, resumo, botoesDia, saida, menos, mais }
  const duracaoTexto = (min) => {
    const h = Math.floor(min / 60);
    const m = min % 60;
    return [h ? `${h} h` : '', m ? `${m} min` : ''].filter(Boolean).join(' ');
  };

  function desenharCatalogo() {
    const area = $('ob-catalogo');
    area.innerHTML = '';
    if (estado.catalogo.length === 0) {
      const p = document.createElement('p');
      p.className = 'apoio';
      p.textContent = 'Não foi possível carregar as modalidades. Recarregue a página.';
      area.appendChild(p);
      return;
    }
    // Agrupa pela categoria que a API devolve (só para a tela); categoria
    // fora da lista conhecida entra no fim, com o próprio nome.
    const grupos = new Map(CATEGORIAS.map((nome) => [nome, []]));
    estado.catalogo.forEach((mod) => {
      if (!grupos.has(mod.categoria)) grupos.set(mod.categoria, []);
      grupos.get(mod.categoria).push(mod);
    });

    let indice = 0;
    grupos.forEach((modalidades, categoria) => {
      if (modalidades.length === 0) return;
      const secao = document.createElement('section');
      secao.className = 'ob-grupo';
      secao.setAttribute('aria-labelledby', `ob-grupo-${indice}`);
      const titulo = document.createElement('h2');
      titulo.className = 'rotulo ob-grupo-titulo';
      titulo.id = `ob-grupo-${indice}`;
      titulo.textContent = categoria;
      const chips = document.createElement('div');
      chips.className = 'chips';

      modalidades.forEach((mod) => {
        const botao = document.createElement('button');
        botao.type = 'button';
        botao.className = 'chip';
        botao.textContent = mod.nome;
        botao.setAttribute('aria-pressed', String(estado.escolhidas.has(mod.id)));
        botao.addEventListener('click', () => {
          if (estado.escolhidas.has(mod.id)) {
            estado.escolhidas.delete(mod.id);
            cartoes.get(mod.id)?.el.remove();
            cartoes.delete(mod.id);
          } else {
            estado.escolhidas.set(mod.id, { dias: new Set(), minutos: DURACAO_PADRAO });
            criarCartao(mod);
          }
          botao.setAttribute('aria-pressed', String(estado.escolhidas.has(mod.id)));
          agendarSimulacao();
        });
        chips.appendChild(botao);
      });

      secao.append(titulo, chips);
      area.appendChild(secao);
      indice += 1;
    });
    estado.catalogo.filter((mod) => estado.escolhidas.has(mod.id)).forEach(criarCartao);
  }

  // "Não treino no momento": esconde o catálogo e os cartões e deixa a lista
  // vazia. Se já havia modalidades escolhidas, pede confirmação na própria
  // página (aviso abaixo do chip), sem confirm() do navegador.
  const elSemTreino = $('ob-sem-treino');
  const elSemTreinoConfirma = $('ob-sem-treino-confirma');

  function definirSemTreino(valor) {
    estado.semTreino = valor;
    elSemTreino.setAttribute('aria-pressed', String(valor));
    elSemTreinoConfirma.hidden = true;
    $('ob-catalogo').hidden = valor;
    $('ob-escolhidas').hidden = valor;
    agendarSimulacao();
  }

  elSemTreino.addEventListener('click', () => {
    if (estado.semTreino) {
      definirSemTreino(false);
      return;
    }
    if (estado.escolhidas.size > 0) {
      elSemTreinoConfirma.hidden = false;
      $('ob-sem-treino-remover').focus();
      return;
    }
    definirSemTreino(true);
  });

  $('ob-sem-treino-remover').addEventListener('click', () => {
    estado.escolhidas.clear();
    cartoes.forEach((c) => c.el.remove());
    cartoes.clear();
    desenharCatalogo();
    definirSemTreino(true);
    elSemTreino.focus();
  });

  $('ob-sem-treino-cancelar').addEventListener('click', () => {
    elSemTreinoConfirma.hidden = true;
    elSemTreino.focus();
  });

  function criarCartao(mod) {
    const escolha = estado.escolhidas.get(mod.id);
    const el = document.createElement('div');
    el.className = 'cartao ob-escolhida';

    const cabeca = document.createElement('div');
    cabeca.className = 'ob-escolhida-cabeca';
    const nome = document.createElement('span');
    nome.className = 'ob-escolhida-nome';
    nome.textContent = mod.nome;
    const resumo = document.createElement('span');
    resumo.className = 'apoio num';
    cabeca.append(nome, resumo);

    const dias = document.createElement('div');
    dias.className = 'dias';
    dias.setAttribute('role', 'group');
    dias.setAttribute('aria-label', `Dias de ${mod.nome}`);
    const botoesDia = DIAS.map(([valor, rotulo, completo]) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.textContent = rotulo;
      b.setAttribute('aria-label', completo);
      b.setAttribute('aria-pressed', String(escolha.dias.has(valor)));
      b.addEventListener('click', () => {
        if (escolha.dias.has(valor)) escolha.dias.delete(valor);
        else escolha.dias.add(valor);
        b.setAttribute('aria-pressed', String(escolha.dias.has(valor)));
        atualizarCartao(mod.id);
        agendarSimulacao();
      });
      dias.appendChild(b);
      return b;
    });

    const linhaDuracao = document.createElement('div');
    linhaDuracao.className = 'ob-duracao-linha';
    const rotuloDuracao = document.createElement('span');
    rotuloDuracao.className = 'campo-rotulo';
    rotuloDuracao.textContent = 'Duração de cada sessão';
    const controle = document.createElement('div');
    controle.className = 'ob-duracao';
    controle.setAttribute('role', 'group');
    controle.setAttribute('aria-label', `Duração de cada sessão de ${mod.nome}`);
    const menos = document.createElement('button');
    menos.type = 'button';
    menos.textContent = '−';
    menos.setAttribute('aria-label', 'Diminuir 15 minutos');
    const saida = document.createElement('output');
    saida.setAttribute('aria-live', 'polite');
    const mais = document.createElement('button');
    mais.type = 'button';
    mais.textContent = '+';
    mais.setAttribute('aria-label', 'Aumentar 15 minutos');
    const mudar = (delta) => {
      escolha.minutos = Math.min(DURACAO_MAX, Math.max(DURACAO_MIN, escolha.minutos + delta));
      atualizarCartao(mod.id);
      agendarSimulacao();
    };
    menos.addEventListener('click', () => mudar(-15));
    mais.addEventListener('click', () => mudar(15));
    controle.append(menos, saida, mais);
    linhaDuracao.append(rotuloDuracao, controle);

    el.append(cabeca, dias, linhaDuracao);
    $('ob-escolhidas').appendChild(el);
    cartoes.set(mod.id, { el, resumo, saida, menos, mais, botoesDia });
    atualizarCartao(mod.id);
  }

  function atualizarCartao(id) {
    const c = cartoes.get(id);
    const escolha = estado.escolhidas.get(id);
    if (!c || !escolha) return;
    c.saida.textContent = duracaoTexto(escolha.minutos);
    c.menos.disabled = escolha.minutos <= DURACAO_MIN;
    c.mais.disabled = escolha.minutos >= DURACAO_MAX;
    atualizarResumosTreino();
  }

  // "3x por semana · ~816 kcal por sessão de 1 h 30 min", com as kcal da simulação.
  function atualizarResumosTreino() {
    cartoes.forEach((c, id) => {
      const escolha = estado.escolhidas.get(id);
      if (!escolha) return;
      if (escolha.dias.size === 0) {
        c.resumo.textContent = 'Marque os dias de treino';
        return;
      }
      const daSimulacao = estado.simulacao?.modalidades.find((m) =>
        m.modalidadeId === id && Math.round(m.duracaoHoras * 60) === escolha.minutos);
      const kcal = daSimulacao ? ` · ~${fmt(daSimulacao.kcalSessao)} kcal por sessão de ${duracaoTexto(escolha.minutos)}` : '';
      c.resumo.textContent = `${escolha.dias.size}x por semana${kcal}`;
    });
  }

  async function avancarTreino() {
    if (estado.escolhidas.size === 0 && !estado.semTreino) {
      return erro('Escolha uma modalidade ou marque “Não treino no momento”.');
    }
    const nomes = new Map(estado.catalogo.map((m) => [m.id, m.nome]));
    for (const [id, escolha] of estado.escolhidas) {
      if (escolha.dias.size === 0) return erro(`Marque pelo menos um dia para ${nomes.get(id)}.`);
    }

    await Promise.all(modalidadesParaApi().map(async (corpo) => {
      const salvo = await enviar('/usuarios/modalidades', 'POST', corpo);
      if (salvo?.id) estado.modalidadesSalvas.set(corpo.modalidadeId, salvo.id);
    }));
    // As que estavam salvas e foram desmarcadas saem da API. Com "Não treino no
    // momento" (nenhuma escolhida) saem todas, e o cadastro passa a valer pela
    // dieta gerada no último passo (ver ronuChecarCadastroCompleto em auth.js).
    const desmarcadas = [...estado.modalidadesSalvas.entries()].filter(([idMod]) => !estado.escolhidas.has(idMod));
    await Promise.all(desmarcadas.map(async ([idMod, idVinculo]) => {
      await enviar(`/usuarios/modalidades/${idVinculo}`, 'DELETE');
      estado.modalidadesSalvas.delete(idMod);
    }));
    mostrarPasso(3);
  }

  // ----- Passo 3: alimentação -----
  const MARCAS = {
    gosto: '<svg class="ob-chip-marca" viewBox="0 0 12 12" fill="currentColor" aria-hidden="true"><path d="M6 10.5 1.6 6.3A2.6 2.6 0 0 1 6 2.9a2.6 2.6 0 0 1 4.4 3.4z"/></svg>',
    evitar: '<svg class="ob-chip-marca" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" aria-hidden="true"><path d="M2.5 2.5l7 7M9.5 2.5l-7 7"/></svg>'
  };
  const ROTULO_ESTADO = { gosto: 'gosto', evitar: 'prefiro evitar' };

  function desenharComidas() {
    const area = $('ob-comidas');
    area.innerHTML = '';
    const nomes = [...COMIDAS_BASE, ...[...estado.comidas.keys()].filter((n) => !COMIDAS_BASE.includes(n))];
    nomes.forEach((nome) => {
      const b = document.createElement('button');
      b.type = 'button';
      b.className = 'chip';
      const pintar = () => {
        const valor = estado.comidas.get(nome);
        b.dataset.estado = valor || '';
        b.innerHTML = (valor ? MARCAS[valor] : '');
        b.append(document.createTextNode(nome));
        b.setAttribute('aria-label', valor ? `${nome}, ${ROTULO_ESTADO[valor]}` : nome);
      };
      b.addEventListener('click', () => {
        const atual = estado.comidas.get(nome);
        if (!atual) estado.comidas.set(nome, 'gosto');
        else if (atual === 'gosto') estado.comidas.set(nome, 'evitar');
        else estado.comidas.delete(nome);
        pintar();
        contarComidas();
      });
      pintar();
      area.appendChild(b);
    });
    contarComidas();
  }

  function contarComidas() {
    const valores = [...estado.comidas.values()];
    const g = valores.filter((v) => v === 'gosto').length;
    const e = valores.filter((v) => v === 'evitar').length;
    $('ob-conta-gosto').textContent = `${g} que você gosta`;
    $('ob-conta-evitar').textContent = `${e} para evitar`;
  }

  let tipoOutro = 'gosto';
  ligarRadios('ob-outro-tipo', (valor) => {
    tipoOutro = valor;
    marcarRadios('ob-outro-tipo', valor);
  });
  function adicionarOutro() {
    const nome = $('ob-outro').value.trim().toLowerCase();
    if (!nome) {
      $('ob-outro').focus();
      return;
    }
    estado.comidas.set(nome, tipoOutro);
    $('ob-outro').value = '';
    desenharComidas();
    $('ob-outro').focus();
  }
  $('ob-outro-adicionar').addEventListener('click', adicionarOutro);
  $('ob-outro').addEventListener('keydown', (evento) => {
    if (evento.key === 'Enter') {
      evento.preventDefault();
      adicionarOutro();
    }
  });

  async function avancarAlimentacao() {
    const pedidos = [];
    estado.comidas.forEach((valor, nome) => {
      const tipo = valor === 'evitar' ? 'evitar' : 'preferido';
      if (estado.preferenciasSalvas.get(nome)?.tipo === tipo) return;
      // O POST atualiza o tipo se o alimento já existe (sem duplicar).
      pedidos.push(enviar('/preferencias-alimentares', 'POST', { alimento: nome, tipo }).then((salvo) => {
        if (salvo?.id) estado.preferenciasSalvas.set(nome, { id: salvo.id, tipo });
      }));
    });
    estado.preferenciasSalvas.forEach((salvo, nome) => {
      if (estado.comidas.has(nome)) return;
      pedidos.push(enviar(`/preferencias-alimentares/${salvo.id}`, 'DELETE').then(() => estado.preferenciasSalvas.delete(nome)));
    });
    await Promise.all(pedidos);
    mostrarPasso(4);
  }

  // ----- Passo 4: a primeira dieta -----
  function prepararFim() {
    const usuario = ronuUsuarioLogado();
    const primeiro = (estado.perfilSalvo?.nome || usuario?.nome || '').trim().split(/\s+/)[0];
    $('ob-nome').textContent = primeiro && !primeiro.includes('@') ? `, ${primeiro}` : '';
    const uf = estado.perfilSalvo?.estado;
    $('ob-gerando-regiao').textContent = ESTADOS[uf] ? `Ajustando os nomes para ${ESTADOS[uf]}` : 'Ajustando os nomes para a sua região';
  }

  $('ob-gerar').addEventListener('click', async () => {
    const pronto = $('ob-fim-pronto');
    const gerando = $('ob-fim-gerando');
    ronuOcultarErroFormulario($('ob-gerar-erro'));
    pronto.hidden = true;
    gerando.hidden = false;
    try {
      const resposta = await ronuFetchAutenticado('/dietas/gerar', { method: 'POST' });
      if (!resposta.ok) {
        const dados = await resposta.json().catch(() => null);
        throw new Error(dados?.mensagem || 'Não foi possível gerar sua dieta agora. Tente novamente em alguns instantes.');
      }
      window.location.href = 'dashboard.html';
    } catch (falha) {
      if (falha.message === 'Sessão expirada.') return;
      gerando.hidden = true;
      pronto.hidden = false;
      ronuMostrarErroFormulario($('ob-gerar-erro'), falha.message);
      $('ob-ir-painel').hidden = false;
      $('ob-gerar').querySelector('.btn-label').textContent = 'Tentar de novo';
    }
  });

  // ---------- Navegação ----------
  const avancos = [avancarPerfil, avancarObjetivo, avancarTreino, avancarAlimentacao];

  btnAvancar.addEventListener('click', async () => {
    if (btnAvancar.dataset.loading === 'true') return;
    ronuOcultarErroFormulario(formError);
    document.querySelectorAll('.ob-passo .field-error').forEach((c) => c.classList.remove('field-error'));
    ronuDefinirCarregando(btnAvancar, true, 'Salvando...');
    try {
      await avancos[estado.passo]();
    } catch (falha) {
      if (falha.message !== 'Sessão expirada.') ronuMostrarErroFormulario(formError, falha.message);
    } finally {
      ronuDefinirCarregando(btnAvancar, false);
    }
  });

  // O aviso de erro do passo some assim que a pessoa mexe para corrigir.
  ['click', 'input'].forEach((tipo) => {
    document.querySelector('.ob-conteudo').addEventListener(tipo, (evento) => {
      if (!formError.hidden && !formError.contains(evento.target)) ronuOcultarErroFormulario(formError);
    });
  });

  btnPular.addEventListener('click', () => mostrarPasso(4));
  btnVoltar.addEventListener('click', () => {
    if (estado.passo > 0) mostrarPasso(estado.passo - 1);
  });

  desenharCatalogo();
  desenharComidas();
  mostrarPasso(0);
});
