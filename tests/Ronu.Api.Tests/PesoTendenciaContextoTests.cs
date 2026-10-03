using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Peso das contas da dieta (ContextoDietaBuilder.PesoDeTendencia): o peso de
/// tendência mais recente, não a última pesagem bruta.
/// </summary>
public class PesoTendenciaContextoTests
{
    private static readonly DateTime Hoje = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    private static RegistroPesoAderenciaDto Registro(int diasAtras, decimal peso) =>
        new() { Data = Hoje.AddDays(-diasAtras), Peso = peso, Objetivo = "perder peso" };

    private static decimal Peso(params RegistroPesoAderenciaDto[] historico) =>
        ContextoDietaBuilder.PesoDeTendencia(historico, new CalculadoraPesoTendencia());

    [Fact]
    public void Um_Registro_So_E_O_Proprio_Peso()
    {
        Assert.Equal(80m, Peso(Registro(0, 80)));
    }

    // Um pico de +2 kg no último dia (água, sal) mexe pouco no peso usado.
    [Fact]
    public void Pico_No_Ultimo_Dia_Mexe_Pouco()
    {
        var historico = Enumerable.Range(1, 20).Select(d => Registro(d, 80)).Append(Registro(0, 82)).ToArray();

        var peso = Peso(historico);

        Assert.InRange(peso, 80.1m, 80.5m); // 80 + 2 · (1 − e^(−1/7)) ≈ 80,27
    }

    // Numa série, é o último PesoTendencia da CalculadoraPesoTendencia, com 1 casa.
    [Fact]
    public void Serie_Usa_A_Ultima_Tendencia_Arredondada()
    {
        var historico = new[] { Registro(10, 85), Registro(5, 84), Registro(0, 83) };
        var esperado = new CalculadoraPesoTendencia()
            .Calcular(historico.Select(r => new RegistroPesoDto { Data = r.Data, Peso = r.Peso }).ToList())[^1].PesoTendencia;

        Assert.Equal(Math.Round(esperado, 1, MidpointRounding.AwayFromZero), Peso(historico));
        Assert.NotEqual(83m, Peso(historico));
    }

    // Defesa: se a calculadora devolver vazio, cai na última pesagem bruta.
    [Fact]
    public void Sem_Tendencia_Usa_A_Ultima_Pesagem()
    {
        var historico = new[] { Registro(3, 81), Registro(0, 79) };

        Assert.Equal(79m, ContextoDietaBuilder.PesoDeTendencia(historico, new TendenciaVazia()));
    }

    private sealed class TendenciaVazia : ICalculadoraPesoTendencia
    {
        public List<PesoTendenciaDto> Calcular(List<RegistroPesoDto> registros) => new();
    }
}
