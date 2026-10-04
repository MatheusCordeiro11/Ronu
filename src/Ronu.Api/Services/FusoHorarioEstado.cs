namespace Ronu.Api.Services;

/// <summary>
/// Fuso horário do usuário a partir do estado (UF) do cadastro, para decidir
/// qual é o "hoje" dele. Antes, o dia era o do calendário UTC: em Brasília o
/// dia virava às 21h, e uma pesagem às 22h caía no dia seguinte.
/// O Brasil não tem horário de verão desde 2019, mas o fuso vem pelo nome IANA
/// para seguir qualquer mudança futura da base de fusos do sistema.
/// </summary>
public static class FusoHorarioEstado
{
    // Sem estado (contas antigas ou via Google que ainda não informaram) ou
    // com um valor desconhecido: horário de Brasília, o da maior parte do país.
    public const string FusoPadrao = "America/Sao_Paulo";

    // O UTC−2 (America/Noronha) não aparece: Fernando de Noronha faz parte de
    // PE, que fica em UTC−3 — pela UF não dá para distinguir. Alguns estados
    // também têm municípios em outro fuso (o oeste do AM fica em UTC−5); vale
    // o fuso da capital. O deslocamento ao lado é o fallback se o sistema não
    // tiver a base de fusos (ex.: contêiner sem tzdata).
    private static readonly Dictionary<string, (string Iana, int HorasUtc)> FusoPorUf = new()
    {
        ["AC"] = ("America/Rio_Branco", -5),

        ["AM"] = ("America/Manaus", -4),
        ["RR"] = ("America/Boa_Vista", -4),
        ["RO"] = ("America/Porto_Velho", -4),
        ["MT"] = ("America/Cuiaba", -4),
        ["MS"] = ("America/Campo_Grande", -4),

        ["PA"] = ("America/Belem", -3),
        ["AP"] = ("America/Belem", -3),
        ["TO"] = ("America/Araguaina", -3),
        ["MA"] = ("America/Fortaleza", -3),
        ["PI"] = ("America/Fortaleza", -3),
        ["CE"] = ("America/Fortaleza", -3),
        ["RN"] = ("America/Fortaleza", -3),
        ["PB"] = ("America/Fortaleza", -3),
        ["PE"] = ("America/Recife", -3),
        ["AL"] = ("America/Maceio", -3),
        ["SE"] = ("America/Maceio", -3),
        ["BA"] = ("America/Bahia", -3),
        ["GO"] = (FusoPadrao, -3),
        ["DF"] = (FusoPadrao, -3),
        ["MG"] = (FusoPadrao, -3),
        ["ES"] = (FusoPadrao, -3),
        ["RJ"] = (FusoPadrao, -3),
        ["SP"] = (FusoPadrao, -3),
        ["PR"] = (FusoPadrao, -3),
        ["SC"] = (FusoPadrao, -3),
        ["RS"] = (FusoPadrao, -3)
    };

    /// <summary>Nome IANA e deslocamento de fallback do fuso da UF (padrão: Brasília).</summary>
    public static (string Iana, int HorasUtc) FusoDaUf(string? uf)
    {
        var sigla = (uf ?? string.Empty).Trim().ToUpperInvariant();
        return FusoPorUf.TryGetValue(sigla, out var fuso) ? fuso : (FusoPadrao, -3);
    }

    public static TimeZoneInfo Fuso(string? uf)
    {
        var (iana, horasUtc) = FusoDaUf(uf);

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(iana);
        }
        catch (Exception erro) when (erro is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(iana, TimeSpan.FromHours(horasUtc), iana, iana);
        }
    }

    /// <summary>O dia do calendário no fuso da UF no instante informado (UTC).</summary>
    public static DateOnly Hoje(string? uf, DateTime agoraUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(agoraUtc, DateTimeKind.Utc), Fuso(uf));
        return DateOnly.FromDateTime(local);
    }

    /// <summary>
    /// Início (inclusive) e fim (exclusivo), em UTC, do dia de hoje no fuso da
    /// UF — para filtrar uma coluna gravada em UTC (DataRegistro) sem converter
    /// cada linha no banco.
    /// </summary>
    public static (DateTime InicioUtc, DateTime FimUtc) IntervaloUtcDeHoje(string? uf, DateTime agoraUtc)
    {
        var fuso = Fuso(uf);
        var hoje = Hoje(uf, agoraUtc);

        var inicioLocal = hoje.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var fimLocal = hoje.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return (TimeZoneInfo.ConvertTimeToUtc(inicioLocal, fuso), TimeZoneInfo.ConvertTimeToUtc(fimLocal, fuso));
    }
}
