using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services;

/// <summary>
/// As regras da refeição feita que dependem do relógio, sem banco: o "hoje" da pessoa, quais datas dá para marcar e
/// qual dia da dieta corresponde a uma data. Recebe o instante por parâmetro, para os testes fixarem o relógio.
///
/// Dá para marcar hoje e ontem (decisão de 2026-11-16), só na dieta atual, e nunca antes do dia (local) em que ela foi
/// gerada: ontem a dieta era outra. Futuro não (o gesto é "um toque depois de comer"), e mais de um dia para trás
/// também não (a lembrança fica ruim e abre espaço para preencher a semana de uma vez).
/// </summary>
public static class RegrasRefeicoesFeitas
{
    /// <summary>As datas marcáveis, da mais antiga para a mais nova. Hoje sempre entra.</summary>
    public static IReadOnlyList<DateOnly> DatasMarcaveis(DateTime dataGeracaoUtc, string? uf, DateTime agoraUtc)
    {
        var hoje = FusoHorarioEstado.Hoje(uf, agoraUtc);
        var diaDaGeracao = FusoHorarioEstado.Hoje(uf, dataGeracaoUtc);
        var ontem = hoje.AddDays(-1);

        return ontem >= diaDaGeracao ? [ontem, hoje] : [hoje];
    }

    /// <summary>
    /// A posição, em dieta.Dias, do dia cujo nome casa com o dia da semana da data (como o
    /// RepositorioDietaIA.MontarMetaDieta: a lista da IA pode vir em outra ordem). Nulo se nenhum casa.
    /// </summary>
    public static int? IndiceDoDia(DietaSemanalDto dieta, DateOnly data)
    {
        var nome = GeradorDietaGemini.NomesDias[((int)data.DayOfWeek + 6) % 7]; // 0 = segunda
        var indice = dieta.Dias.FindIndex(d => string.Equals(d.DiaSemana.Trim(), nome, StringComparison.OrdinalIgnoreCase));
        return indice < 0 ? null : indice;
    }
}
