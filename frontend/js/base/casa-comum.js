// Ronu — Peças da casa (plano família) usadas em mais de uma tela: a aba Casa
// (casa.js), o painel (dashboard.js) e a lista de compras (compras.js).
//
// - As chaves das refeições da casa ("qua-jantar") e o nome delas em frase.
// - O "Pedido enviado" do membro, guardado SÓ neste aparelho e pela semana: a
//   API não guarda quem pediu (decisão do plano), então não tem como dizer à
//   tela que esta pessoa já pediu. Semana nova, pedidos novos.
// - O POST /casa/pedidos e a pílula "da casa".
// Carregar depois do auth.js.

window.RonuCasa = (function () {
  const DIAS = ['seg', 'ter', 'qua', 'qui', 'sex', 'sab', 'dom'];
  const NOMES_DIAS = ['segunda', 'terça', 'quarta', 'quinta', 'sexta', 'sábado', 'domingo'];
  const NOMES_REFEICAO = { almoco: 'almoço', jantar: 'jantar' };

  // "qua-jantar" -> { dia: 2, refeicao: 'jantar' }
  function lerChave(chave) {
    const [dia, refeicao] = String(chave || '').split('-');
    return { dia: DIAS.indexOf(dia), refeicao };
  }

  // "qua-jantar" -> "jantar de quarta"
  function nomeDaChave(chave) {
    const { dia, refeicao } = lerChave(chave);
    return `${NOMES_REFEICAO[refeicao] || refeicao} de ${NOMES_DIAS[dia] || ''}`.trim();
  }

  // ---------- Pedidos feitos neste aparelho ----------

  function chaveLocal() {
    const usuario = typeof ronuUsuarioLogado === 'function' ? ronuUsuarioLogado() : null;
    return `ronu:casa:pedidos:${usuario ? usuario.id : 'anonimo'}`;
  }

  function lerPedidos(semanaId) {
    try {
      const salvo = JSON.parse(localStorage.getItem(chaveLocal()) || 'null');
      if (salvo && salvo.semanaId === semanaId) return salvo;
    } catch {
      // Sem armazenamento ou valor estranho: começa do zero.
    }
    return { semanaId, semana: false, trocas: [] };
  }

  function gravarPedidos(registro) {
    try {
      localStorage.setItem(chaveLocal(), JSON.stringify(registro));
    } catch {
      // Sem armazenamento: o "Pedido enviado" vale só até sair da página.
    }
  }

  const idTroca = (chaveCasa, item) => `${chaveCasa}:${item}`;

  function jaPediuSemana(semanaId) {
    return lerPedidos(semanaId).semana;
  }

  function jaPediuTroca(semanaId, chaveCasa, item) {
    return lerPedidos(semanaId).trocas.includes(idTroca(chaveCasa, item));
  }

  // POST /casa/pedidos. Devolve { ok, status, mensagem }. A resposta (202) é a
  // mesma para um pedido novo e para um igual a um já aberto: a tela também não
  // distingue. semanaId da resposta é a chave do registro local.
  async function pedir({ tipo, semanaId, chaveCasa, item, alimentoId }) {
    const corpo = { tipo, semanaId: semanaId ?? null };
    if (tipo === 'troca') Object.assign(corpo, { chaveCasa, item, alimentoId: alimentoId || null });
    let resposta;
    try {
      resposta = await ronuFetchAutenticado('/casa/pedidos', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(corpo)
      });
    } catch (erro) {
      if (erro.message === 'Sessão expirada.') return null;
      return { ok: false, status: 0, mensagem: 'Sem conexão: o pedido não foi enviado. Tente de novo.' };
    }
    const dados = await resposta.json().catch(() => null);
    if (!resposta.ok) {
      return { ok: false, status: resposta.status, mensagem: dados?.mensagem || 'Não foi possível enviar o pedido. Tente de novo.' };
    }
    const semana = dados?.semanaId ?? semanaId ?? null;
    const registro = lerPedidos(semana);
    if (tipo === 'troca') {
      if (!registro.trocas.includes(idTroca(chaveCasa, item))) registro.trocas.push(idTroca(chaveCasa, item));
    } else {
      registro.semana = true;
    }
    gravarPedidos(registro);
    return { ok: true, status: resposta.status, semanaId: semana };
  }

  // ---------- Pílula ----------

  function pilula() {
    const el = document.createElement('span');
    el.className = 'pilula-casa';
    el.textContent = 'da casa';
    return el;
  }

  return { DIAS, lerChave, nomeDaChave, jaPediuSemana, jaPediuTroca, pedir, pilula };
})();
