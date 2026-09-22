using Ronu.Api.DTOs;

namespace Ronu.Api.Services;

/// <summary>
/// Implementação de ICalculadoraPesoTendencia usando a variante time-aware
/// da EMA (Libra/Hacker's Diet): o peso do ajuste depende de quantos dias
/// realmente se passaram entre dois registros, não assume pesagem diária.
/// </summary>
public class CalculadoraPesoTendencia : ICalculadoraPesoTendencia
{
    // Constante de suavização em dias — equivalente em ordem de grandeza ao
    // alfa=0,1 (10%) do Hacker's Diet original para um intervalo de ~1 dia.
    private const double SuavizacaoDias = 7.0;

    public List<PesoTendenciaDto> Calcular(List<RegistroPesoDto> registros)
    {
        var ordenados = registros.OrderBy(r => r.Data).ToList();
        var resultado = new List<PesoTendenciaDto>();

        if (ordenados.Count == 0)
        {
            return resultado;
        }

        // O primeiro registro não tem tendência anterior para suavizar
        // contra — a técnica original (Hacker's Diet) trata o primeiro
        // ponto como a semente da tendência, sem filtro nenhum.
        decimal tendenciaAnterior = ordenados[0].Peso;
        resultado.Add(new PesoTendenciaDto
        {
            Data = ordenados[0].Data,
            PesoBruto = ordenados[0].Peso,
            PesoTendencia = tendenciaAnterior
        });

        for (int i = 1; i < ordenados.Count; i++)
        {
            var diasDesdeUltimo = (ordenados[i].Data - ordenados[i - 1].Data).TotalDays;

            // power = 1 - e^(-Δt / suavizacaoDias): quanto maior o intervalo
            // real entre registros, mais peso o novo valor recebe sobre a
            // tendência anterior — evita distorcer a tendência tratando um
            // intervalo de 10 dias como se fosse 1 dia.
            var power = 1 - Math.Exp(-diasDesdeUltimo / SuavizacaoDias);
            var novaTendencia = tendenciaAnterior + (decimal)power * (ordenados[i].Peso - tendenciaAnterior);

            resultado.Add(new PesoTendenciaDto
            {
                Data = ordenados[i].Data,
                PesoBruto = ordenados[i].Peso,
                PesoTendencia = novaTendencia
            });

            tendenciaAnterior = novaTendencia;
        }

        return resultado;
    }
}
