using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação da meta calórica adaptativa. Estima o erro da fórmula de
/// manutenção na janela — gasto real (o que a pessoa comeu, menos a variação
/// de peso) menos a manutenção que a fórmula calculou para aqueles dias — e
/// leva esse erro para a meta de hoje.
///
/// "O que a pessoa comeu" é dia a dia: a meta do dia da semana na dieta que
/// estava ativa naquele dia (MetasDieta, com ajuste adaptativo e piso — o que
/// ela recebeu), vezes a aderência da pesagem que fecha aquele intervalo.
/// Antes, o cálculo supunha que ela tinha comido a meta base ATUAL a janela
/// inteira: mudanças de meta no meio (fórmula, peso, o próprio ajuste
/// anterior) distorciam o resultado — inclusive desfazendo parte do próprio
/// ajuste (equilíbrio em ⅓ do erro, em vez de ½).
///
/// Modelo "absoluto e sem memória": o ajuste é refeito do zero a cada dieta,
/// com amortecimento de 50% e teto de ±5% — na prática, uma correção
/// permanente de metade do erro (ver docs/lacunas-conhecidas.md).
/// </summary>
public class CalculadoraAjusteAdaptativo : ICalculadoraAjusteAdaptativo
{
    // Janela: só o comportamento recente, só sob o objetivo atual — dados de
    // outro objetivo (ex.: um período de ganho antes de um de perda) distorceriam
    // o ritmo esperado — e só a partir da primeira dieta (sem dieta, não se sabe
    // o que a pessoa comeu).
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

    // Acima desta diferença entre a meta prescrita média da janela e a meta
    // base atual, os ritmos vão nulos no DTO: o texto detalhado do dashboard
    // supõe que a pessoa comeu a meta atual e atribuiria à aderência o que é
    // efeito da meta antiga.
    public const decimal DiferencaMaximaParaRitmos = 0.01m;

    // Ritmo esperado e kcal por kg vêm de RitmoObjetivo, os mesmos da meta da
    // fórmula (CalculadoraManutencao).
    private const decimal KcalPorKg = RitmoObjetivo.KcalPorKg;

    private const string NaoSeguiu = "nao_seguiu";

    // Quanto a pessoa comeu em relação à meta, por resposta de aderência. Sem
    // resposta = 100% (neutro). "nao_seguiu" não entra: o ponto é descartado.
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
        IReadOnlyList<MetaDietaRegistroDto> metasDietas,
        string objetivoAtual,
        decimal metaBaseMediaDiaria,
        IReadOnlyList<decimal> manutencaoAtualPorDia,
        DateTime agoraUtc)
    {
        if (historico.Count == 0 || metasDietas.Count == 0 || metaBaseMediaDiaria <= 0)
        {
            return SemAjuste(MotivoAjusteAdaptativo.HistoricoInsuficiente, pontos: 0);
        }

        var ordenado = historico.OrderBy(r => r.Data).ToList();
        var dietas = metasDietas.OrderBy(m => m.DataGeracao).ToList();

        // Tendência calculada sobre o histórico INTEIRO (inclusive dias
        // "nao_seguiu" e pesagens anteriores à primeira dieta — o peso deles é
        // real) e só depois recortada: a média móvel começa "presa" ao primeiro
        // peso e precisa de tempo para assentar. Mesma ordem por data.
        var tendencia = _calculadoraPesoTendencia.Calcular(
            ordenado.Select(r => new RegistroPesoDto { Data = r.Data, Peso = r.Peso }).ToList());

        // Início do período do objetivo atual: volta do registro mais recente
        // até a última troca de objetivo.
        var inicioObjetivoAtual = ordenado.Count;
        for (var i = ordenado.Count - 1; i >= 0 && ordenado[i].Objetivo == objetivoAtual; i--)
        {
            inicioObjetivoAtual = i;
        }

        // Últimos 28 dias, mas nunca antes do dia (UTC) da primeira dieta: a
        // dieta gerada num dia já vale para aquele dia.
        var inicioJanela = agoraUtc.AddDays(-JanelaDias);
        var diaPrimeiraDieta = dietas[0].DataGeracao.Date;
        if (inicioJanela < diaPrimeiraDieta)
        {
            inicioJanela = diaPrimeiraDieta;
        }
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
        var ritmoEsperado = RitmoObjetivo.KgPorSemana(objetivoAtual, pesoAtual);

        // Ingestão e manutenção dia a dia, do dia da primeira pesagem usada até
        // a véspera da última (o peso de um dia reflete o que se comeu antes).
        var pontosUsados = indicesUsados.Select(i => ordenado[i]).ToList();
        var primeiroDia = pontosUsados[0].Data.Date;
        var ultimoDia = pontosUsados[^1].Data.Date;
        decimal somaIngestao = 0m, somaManutencao = 0m, somaMetaPrescrita = 0m;
        var dias = 0;

        for (var dia = primeiroDia; dia < ultimoDia; dia = dia.AddDays(1))
        {
            // Dieta ativa: a mais recente gerada até aquele dia (inclusive).
            var dieta = dietas.Last(m => m.DataGeracao.Date <= dia);
            var indiceSemana = ((int)dia.DayOfWeek + 6) % 7; // 0 = segunda

            // Aderência: a da pesagem que fecha o intervalo daquele dia.
            var fechamento = pontosUsados.First(p => p.Data.Date > dia);
            var multiplicador = MultiplicadorIngestao.GetValueOrDefault(fechamento.Aderencia ?? "", 1.00m);

            // Manutenção comparável só da mesma versão da fórmula; senão, a
            // atual (supõe que o gasto real não mudou — o que mudou foi a conta).
            var manutencao = dieta.VersaoFormula == CalculadoraManutencao.VersaoFormula && dieta.ManutencoesPorDia is { Length: 7 }
                ? dieta.ManutencoesPorDia[indiceSemana]
                : manutencaoAtualPorDia[indiceSemana];

            var meta = dieta.MetasPorDia[indiceSemana];
            somaIngestao += multiplicador * meta;
            somaManutencao += manutencao;
            somaMetaPrescrita += meta;
            dias++;
        }

        var ingestaoMedia = somaIngestao / dias;
        var manutencaoMedia = somaManutencao / dias;
        var metaPrescritaMedia = somaMetaPrescrita / dias;

        // Gasto real      = ingestão média − ritmoReal · 7700/7
        // Erro da fórmula = gasto real − manutenção média da fórmula (mesmos dias)
        // Fator bruto     = erro / meta base atual
        // Com metas e manutenção constantes (M = F + ritmoEsperado · 7700/7),
        // é exatamente o cálculo antigo: (ingestão − 1) − (real − esperado) · (7700/7) / M.
        var erroFormula = ingestaoMedia - ritmoReal * (KcalPorKg / 7m) - manutencaoMedia;
        var fatorBruto = erroFormula / metaBaseMediaDiaria;

        var mostrarRitmos = Math.Abs(metaPrescritaMedia - metaBaseMediaDiaria) / metaBaseMediaDiaria <= DiferencaMaximaParaRitmos;
        decimal? ritmoRealDto = mostrarRitmos ? Math.Round(ritmoReal, 3) : null;
        decimal? ritmoEsperadoDto = mostrarRitmos ? Math.Round(ritmoEsperado, 3) : null;

        // 0,1 ponto percentual de precisão; zona morta -> 0%.
        var percentual = Math.Abs(fatorBruto) < ZonaMorta
            ? 0m
            : Math.Round(Math.Clamp(Amortecimento * fatorBruto, -Teto, Teto), 3);

        return new AjusteAdaptativoDto
        {
            Percentual = percentual,
            Motivo = percentual == 0m ? MotivoAjusteAdaptativo.DentroDoEsperado
                : percentual < 0 ? MotivoAjusteAdaptativo.PesoAcimaDoEsperado
                : MotivoAjusteAdaptativo.PesoAbaixoDoEsperado,
            RitmoRealKgSemana = ritmoRealDto,
            RitmoEsperadoKgSemana = ritmoEsperadoDto,
            PontosUsados = indicesUsados.Count,
            FatorBruto = Math.Round(fatorBruto, 4),
            ErroFormulaKcalDia = Math.Round(erroFormula, 1),
            IngestaoMediaDiaria = Math.Round(ingestaoMedia, 1),
            ManutencaoMediaDiaria = Math.Round(manutencaoMedia, 1),
            MetaPrescritaMediaDiaria = Math.Round(metaPrescritaMedia, 1),
            DiasComDieta = dias
        };
    }

    private static AjusteAdaptativoDto SemAjuste(MotivoAjusteAdaptativo motivo, int pontos) =>
        new()
        {
            Percentual = 0m,
            Motivo = motivo,
            PontosUsados = pontos
        };
}
