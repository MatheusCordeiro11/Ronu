using Ronu.Api.Services;

namespace Ronu.Api.Tests;

/// <summary>
/// O "hoje" do registro de peso no fuso do estado do usuário
/// (FusoHorarioEstado, usado por ObjetivosController.BuscarObjetivoDeHojeAsync).
/// </summary>
public class FusoHorarioEstadoTests
{
    private static DateTime Utc(int dia, int hora, int minuto = 0) =>
        new(2026, 10, dia, hora, minuto, 0, DateTimeKind.Utc);

    // Antes, o dia era o UTC e virava às 21h de Brasília: uma pesagem às 20h59
    // e outra às 21h01 do mesmo dia caíam em dias diferentes.
    [Fact]
    public void Em_Brasilia_O_Dia_Nao_Vira_As_21h()
    {
        var as20h59 = Utc(3, 23, 59); // 03/10 20:59 em Brasília
        var as21h01 = Utc(4, 0, 1);   // 03/10 21:01 em Brasília

        Assert.Equal(new DateOnly(2026, 10, 3), FusoHorarioEstado.Hoje("SP", as20h59));
        Assert.Equal(new DateOnly(2026, 10, 3), FusoHorarioEstado.Hoje("SP", as21h01));

        var (inicio, fim) = FusoHorarioEstado.IntervaloUtcDeHoje("SP", as21h01);
        Assert.True(as20h59 >= inicio && as20h59 < fim);
        Assert.True(as21h01 >= inicio && as21h01 < fim);
    }

    [Fact]
    public void Em_Brasilia_O_Dia_Vira_A_Meia_Noite_Local()
    {
        Assert.Equal(new DateOnly(2026, 10, 3), FusoHorarioEstado.Hoje("SP", Utc(4, 2, 59))); // 23:59
        Assert.Equal(new DateOnly(2026, 10, 4), FusoHorarioEstado.Hoje("SP", Utc(4, 3, 0)));  // 00:00

        var (inicio, fim) = FusoHorarioEstado.IntervaloUtcDeHoje("SP", Utc(4, 0, 30));
        Assert.Equal(Utc(3, 3), inicio);
        Assert.Equal(Utc(4, 3), fim);
        Assert.Equal(DateTimeKind.Utc, inicio.Kind);
        Assert.Equal(DateTimeKind.Utc, fim.Kind);
    }

    // Acre, UTC−5: às 23h30 locais ainda é o mesmo dia, quando em Brasília já
    // passou da meia-noite.
    [Fact]
    public void No_Acre_O_Dia_Vira_Duas_Horas_Depois_De_Brasilia()
    {
        var agora = Utc(4, 4, 30); // 03/10 23:30 no Acre; 04/10 01:30 em Brasília

        Assert.Equal(new DateOnly(2026, 10, 3), FusoHorarioEstado.Hoje("AC", agora));
        Assert.Equal(new DateOnly(2026, 10, 4), FusoHorarioEstado.Hoje("SP", agora));

        var (inicio, fim) = FusoHorarioEstado.IntervaloUtcDeHoje("AC", agora);
        Assert.Equal(Utc(3, 5), inicio);
        Assert.Equal(Utc(4, 5), fim);
    }

    [Fact]
    public void No_Amazonas_E_UTC_Menos_4()
    {
        var (inicio, fim) = FusoHorarioEstado.IntervaloUtcDeHoje("AM", Utc(4, 12));

        Assert.Equal(Utc(4, 4), inicio);
        Assert.Equal(Utc(5, 4), fim);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XX")]
    public void Sem_Estado_Ou_Desconhecido_Usa_Brasilia(string? uf)
    {
        Assert.Equal(FusoHorarioEstado.FusoPadrao, FusoHorarioEstado.FusoDaUf(uf).Iana);
        Assert.Equal(new DateOnly(2026, 10, 3), FusoHorarioEstado.Hoje(uf, Utc(4, 1)));
    }

    [Fact]
    public void Sigla_Em_Minusculas_Ou_Com_Espacos_E_Reconhecida()
    {
        Assert.Equal("America/Rio_Branco", FusoHorarioEstado.FusoDaUf(" ac ").Iana);
    }

    public static TheoryData<string, int> Ufs => new()
    {
        { "AC", -5 },
        { "AM", -4 }, { "RR", -4 }, { "RO", -4 }, { "MT", -4 }, { "MS", -4 },
        { "AL", -3 }, { "AP", -3 }, { "BA", -3 }, { "CE", -3 }, { "DF", -3 }, { "ES", -3 },
        { "GO", -3 }, { "MA", -3 }, { "MG", -3 }, { "PA", -3 }, { "PB", -3 }, { "PE", -3 },
        { "PI", -3 }, { "PR", -3 }, { "RJ", -3 }, { "RN", -3 }, { "RS", -3 }, { "SC", -3 },
        { "SE", -3 }, { "SP", -3 }, { "TO", -3 }
    };

    // As 27 UFs, conferidas contra a base de fusos do sistema: o fuso IANA e
    // o deslocamento de fallback (sem tzdata) precisam concordar.
    [Theory]
    [MemberData(nameof(Ufs))]
    public void Cada_Uf_Tem_O_Deslocamento_Esperado(string uf, int horasUtc)
    {
        var (iana, fallback) = FusoHorarioEstado.FusoDaUf(uf);
        Assert.Equal(horasUtc, fallback);

        var instante = Utc(4, 12);
        var doSistema = TimeZoneInfo.FindSystemTimeZoneById(iana).GetUtcOffset(instante);
        Assert.Equal(TimeSpan.FromHours(horasUtc), doSistema);
        Assert.Equal(TimeSpan.FromHours(horasUtc), FusoHorarioEstado.Fuso(uf).GetUtcOffset(instante));
    }
}
