using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação da meta calórica adaptativa. Modelo "absoluto e sem memória":
/// o ajuste é sempre calculado do zero sobre a meta da fórmula, a cada dieta
/// gerada — os mesmos dados dão sempre o mesmo ajuste. Por isso ele não
/// converge até a correção inteira: com amortecimento de 50% e teto de ±5%, é
/// um empurrão gradual, nunca uma troca brusca de meta.
/// </summary>
public class CalculadoraAjusteAdaptativo : ICalculadoraAjusteAdaptativo
{
    // Janela: só o comportamento recente, e só sob o objetivo atual — dados de
    // outro objetivo (ex.: um período de ganho antes de um de perda) distorceriam
    // o ritmo esperado.
    public const int JanelaDias = 28;

    // Mínimos para confiar na inclinação: com menos pontos ou um intervalo
    // curto, um único peso fora da curva (sal, água) já muda muito o resultado.
    public const int MinimoPontos = 5;
    public const double MinimoDiasIntervalo = 14;

    // Aplica só metade da correção sugerida, nunca mais que ±5%, e ignora
    // diferenças abaixo de 1% (zona morta), para a meta não oscilar por ruído.
    public const decimal Amortecimento = 0.5m;
    public const decimal Teto = 0.05m;
    public const decimal ZonaMorta = 0.01m;

    // 1 kg de gordura ≈ 7700 kcal.
    private const decimal KcalPorKg = 7700m;

    // Ritmo esperado, em fração do peso corporal por semana.
    private const decimal RitmoPerderPorSemana = -0.005m;
    private const decimal RitmoGanharPorSemana = 0.0025m;

    private const string NaoSeguiu = "nao_seguiu";

    // Quanto a pessoa comeu em relação à meta, por resposta de aderência. Sem
    // resposta = 100% (neutro). "nao_seguiu" não entra: o dia é descartado.
    private static readonly Dictionary<string, decimal> MultiplicadorIngestao = new()
    {
        ["seguiu"] = 1.00m,
        ["comeu_mais"] = 1.15m,
        ["comeu_menos"] = 0.85m
    };

    private readonly ICalculadoraPesoTendencia _calculadoraPesoTendencia;

    public CalculadoraAjusteAdaptativo(ICalculadoraPesoTendencia calculadoraPesoTendencia)
    {
        _calculadoraPesoTendencia = calculadoraPesoTendencia;
    }

    public AjusteAdaptativoDto Calcular(
        IReadOnlyList<RegistroPesoAderenciaDto> historico,
        string objetivoAtual,
        decimal metaBaseMediaDiaria,
        DateTime agoraUtc)
    {
        if (historico.Count == 0 || metaBaseMediaDiaria <= 0)
        {
            return SemAjuste(MotivoAjusteAdaptativo.HistoricoInsuficiente, pontos: 0);
        }

        var ordenado = historico.OrderBy(r => r.Data).ToList();

        // Tendência calculada sobre o histórico INTEIRO (inclusive dias
        // "nao_seguiu" — o peso deles é real) e só depois recortada: a média
        // móvel começa "presa" ao primeiro peso e precisa de tempo para
        // assentar. A calculadora devolve os pontos na mesma ordem por data.
        var tendencia = _calculadoraPesoTendencia.Calcular(
            ordenado.Select(r => new RegistroPesoDto { Data = r.Data, Peso = r.Peso }).ToList());

        // Início do período do objetivo atual: volta do registro mais recente
        // até a última troca de objetivo.
        var inicioObjetivoAtual = ordenado.Count;
        for (var i = ordenado.Count - 1; i >= 0 && ordenado[i].Objetivo == objetivoAtual; i--)
        {
            inicioObjetivoAtual = i;
        }

        var inicioJanela = agoraUtc.AddDays(-JanelaDias);
        var indicesUsados = Enumerable.Range(inicioObjetivoAtual, ordenado.Count - inicioObjetivoAtual)
            .Where(i => ordenado[i].Data >= inicioJanela && ordenado[i].Aderencia != NaoSeguiu)
            .ToList();

        if (indicesUsados.Count < MinimoPontos
            || (ordenado[indicesUsados[^1]].Data - ordenado[indicesUsados[0]].Data).TotalDays < MinimoDiasIntervalo)
        {
            return SemAjuste(MotivoAjusteAdaptativo.HistoricoInsuficiente, indicesUsados.Count);
        }

        // Ritmo real: regressão linear simples (mínimos quadrados) do peso de
        // tendência contra o tempo, em kg/dia -> kg/semana.
        var dataInicial = ordenado[indicesUsados[0]].Data;
        var x = indicesUsados.Select(i => (ordenado[i].Data - dataInicial).TotalDays).ToList();
        var y = indicesUsados.Select(i => (double)tendencia[i].PesoTendencia).ToList();
        var mediaX = x.Average();
        var mediaY = y.Average();
        var covariancia = x.Zip(y, (xi, yi) => (xi - mediaX) * (yi - mediaY)).Sum();
        var variancia = x.Sum(xi => (xi - mediaX) * (xi - mediaX));
        var ritmoReal = (decimal)(covariancia / variancia * 7);

        // Ritmo esperado sobre o peso de tendência mais recente.
        var pesoAtual = tendencia[^1].PesoTendencia;
        var ritmoEsperado = objetivoAtual switch
        {
            "perder peso" => RitmoPerderPorSemana * pesoAtual,
            "ganhar peso" => RitmoGanharPorSemana * pesoAtual,
            _ => 0m
        };

        // Ingestão estimada relativa à meta (1 = comeu a meta), média dos dias usados.
        var ingestao = indicesUsados.Average(i => MultiplicadorIngestao.GetValueOrDefault(ordenado[i].Aderencia ?? "", 1.00m));

        // Gasto real    = ingestão · meta − ritmoReal · 7700/7
        // Meta ideal    = gasto real + ritmoEsperado · 7700/7
        // Fator bruto   = meta ideal / meta − 1
        //               = (ingestão − 1) − (ritmoReal − ritmoEsperado) · (7700/7) / meta
        // A aderência entra em (ingestão − 1): se a pessoa engordou além do
        // previsto mas comeu mais que a meta, parte do erro é explicada por isso
        // e a meta cai menos.
        var fatorBruto = (ingestao - 1m) - (ritmoReal - ritmoEsperado) * (KcalPorKg / 7m) / metaBaseMediaDiaria;

        var ritmoRealArredondado = Math.Round(ritmoReal, 3);
        var ritmoEsperadoArredondado = Math.Round(ritmoEsperado, 3);

        if (Math.Abs(fatorBruto) < ZonaMorta)
        {
            return SemAjuste(MotivoAjusteAdaptativo.DentroDoEsperado, indicesUsados.Count, ritmoRealArredondado, ritmoEsperadoArredondado);
        }

        // 0,1 ponto percentual de precisão.
        var percentual = Math.Round(Math.Clamp(Amortecimento * fatorBruto, -Teto, Teto), 3);

        return new AjusteAdaptativoDto
        {
            Percentual = percentual,
            Motivo = percentual < 0 ? MotivoAjusteAdaptativo.PesoAcimaDoEsperado : MotivoAjusteAdaptativo.PesoAbaixoDoEsperado,
            RitmoRealKgSemana = ritmoRealArredondado,
            RitmoEsperadoKgSemana = ritmoEsperadoArredondado,
            PontosUsados = indicesUsados.Count
        };
    }

    private static AjusteAdaptativoDto SemAjuste(
        MotivoAjusteAdaptativo motivo, int pontos, decimal? ritmoReal = null, decimal? ritmoEsperado = null) =>
        new()
        {
            Percentual = 0m,
            Motivo = motivo,
            RitmoRealKgSemana = ritmoReal,
            RitmoEsperadoKgSemana = ritmoEsperado,
            PontosUsados = pontos
        };
}
