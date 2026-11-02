// Ronu — Horários estruturados (acordar, dormir, início de cada treino)
// Usado pela Alimentação e pelo Treino. Os horários vão para a IA no lugar da rotina em texto livre
// (GeradorDietaGemini). Os avisos daqui não bloqueiam nada: só apontam uma combinação provável de erro.
//
// Dormir mais cedo que acordar (00:30 com acordar às 07:00) é dormir depois da meia-noite: o dia vira. A API e o
// prompt entendem do mesmo jeito.

window.RonuHorarios = (function () {
  const DIA = 24 * 60;
  const MINIMO_ACORDADO_H = 8;

  // "HH:mm" → minutos desde 00:00; vazio ou fora do formato → null.
  function minutos(texto) {
    const m = /^(\d{2}):(\d{2})$/.exec(texto || '');
    return m ? Number(m[1]) * 60 + Number(m[2]) : null;
  }

  // A janela acordado, em minutos contados a partir do acordar; o fim passa de 24 h quando a pessoa dorme
  // depois da meia-noite.
  function janela(acordar, dormir) {
    const a = minutos(acordar);
    const d = minutos(dormir);
    if (a == null || d == null || a === d) return null;
    return { inicio: a, fim: d > a ? d : d + DIA };
  }

  function formatarDuracao(min) {
    const h = Math.floor(min / 60);
    const m = min % 60;
    return m ? `${h} h ${m} min` : `${h} h`;
  }

  // Treino de [inicio, inicio + duração] cabe na janela? Um treino de madrugada conta no dia que virou.
  function treinoNaJanela(j, inicio, duracaoHoras) {
    let ini = minutos(inicio);
    if (ini == null) return true;
    if (ini < j.inicio) ini += DIA;
    return ini + Math.round(Number(duracaoHoras) * 60) <= j.fim;
  }

  // Avisos curtos para a combinação de horários. modalidades: [{ nome, horarioInicio, duracaoMediaHoras }].
  function avisos(acordar, dormir, modalidades) {
    const j = janela(acordar, dormir);
    if (!j) return [];
    const lista = [];
    const acordado = j.fim - j.inicio;
    if (acordado < MINIMO_ACORDADO_H * 60) {
      lista.push(`Entre acordar e dormir são só ${formatarDuracao(acordado)}. Confira se os dois horários estão certos.`);
    }
    (modalidades || [])
      .filter((m) => m.horarioInicio && !treinoNaJanela(j, m.horarioInicio, m.duracaoMediaHoras))
      .forEach((m) => {
        lista.push(`O treino de ${m.nome} (${m.horarioInicio}) fica fora do horário em que você está acordado.`);
      });
    return lista;
  }

  return { minutos, janela, avisos, treinoNaJanela };
})();
