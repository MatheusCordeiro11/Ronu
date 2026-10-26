using Ronu.Api.Data;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Fase 4 da base nutricional: o C# ajusta as quantidades do cardápio que o
/// Gemini montou para cada dia chegar perto da meta (kcal e macros), sem trocar
/// nenhum alimento. No g9a (2026-10-25), calculado pela base, só 19 de 49 dias
/// ficavam a até 5% da meta de kcal, com a proteína acima da meta em quase todo
/// dia (+23% a +46% em 4 perfis).
///
/// Como: cada item ajustável pertence ao grupo do seu nutriente principal
/// (proteína, carboidrato ou gordura; BaseNutricionalClassificacao). Por dia,
/// um fator por grupo multiplica as quantidades do grupo, escolhido por mínimos
/// quadrados para aproximar ao mesmo tempo kcal, proteína, carboidrato e
/// gordura da meta. Assim cada macro é fechado pelo seu grupo, e as kcal vêm
/// junto. Itens sem nutriente principal (hortaliças, café, chá) e os não
/// ajustáveis que não são contáveis ficam como estão.
///
/// Limites: cada item fica entre 50% e 150% do que o Gemini escolheu (nenhum
/// some) e no máximo na porção máxima; quem bate num limite trava ali e os
/// outros do grupo compensam. Os contáveis são arredondados para unidade
/// inteira (pelo menos 1) e travados, e os fatores dos outros são recalculados.
/// No fim, as gramas dos contínuos vão para múltiplos legíveis (5 g a partir de
/// 50 g, 1 g abaixo). Quando não dá para fechar, fica a melhor solução dentro
/// dos limites (sem nova chamada ao Gemini), e o log registra o dia.
///
/// Simulado offline sobre as 7 respostas do g9a (2026-10-26): 49 de 49 dias com
/// kcal a até 5% da meta (antes, 19) e 34 de 49 com proteína, carboidrato e
/// gordura a até 10% (antes, 4).
/// </summary>
public static class AjusteQuantidades
{
    // Calibração do projeto, não valor de tabela (simulação de 2026-10-26).
    public const decimal Piso = 0.5m;
    public const decimal Teto = 1.5m;

    // kcal primeiro, depois proteína, como a regra 13 do prompt.
    private static readonly double[] Pesos = [3.0, 2.0, 1.0, 1.0];

    // Puxa os fatores para 1: mexer o mínimo no cardápio que o Gemini montou.
    private const double Regularizacao = 0.02;

    private const int MaximoIteracoes = 10;

    private static readonly IReadOnlyDictionary<string, AlimentoBase> PorId = BaseNutricional.Itens.ToDictionary(i => i.Id);

    /// <summary>Ajusta as quantidades de todos os dias, em cima de AlimentoId (AplicarLista antes).</summary>
    public static List<AjusteDoDia> Ajustar(List<DiaDietaDto> dias) => dias.Select(AjustarDia).ToList();

    private sealed class Item
    {
        public required AlimentoDto Alimento { get; init; }
        public required AlimentoBase Base { get; init; }
        public required int? Grupo { get; init; }
        public required decimal Original { get; init; }
        public required decimal Minimo { get; init; }
        public required decimal Maximo { get; init; }
        public decimal? Unidade => Base.MedidaUnidade?.Gramas;
        public bool Travado { get; set; }
        public double Quantidade { get; set; }

        public double[] Vetor(double quantidade) =>
        [
            (double)Base.Kcal * quantidade / 100, (double)Base.ProteinaG * quantidade / 100,
            (double)Base.CarboidratoG * quantidade / 100, (double)Base.GorduraG * quantidade / 100
        ];
    }

    private static int? GrupoDe(AlimentoBase item) => item.Nutriente switch
    {
        NutrientePrincipal.Proteina => 0,
        NutrientePrincipal.Carboidrato => 1,
        NutrientePrincipal.Gordura => 2,
        _ => null
    };

    private static AjusteDoDia AjustarDia(DiaDietaDto dia)
    {
        var meta = new[]
        {
            (double)dia.MetaCalculada.Calorias, (double)dia.MetaCalculada.ProteinasG,
            (double)dia.MetaCalculada.CarboidratosG, (double)dia.MetaCalculada.GordurasG
        };

        var itens = dia.Refeicoes.SelectMany(r => r.Alimentos).Select(a =>
        {
            var b = PorId[a.AlimentoId!];
            var unidade = b.MedidaUnidade?.Gramas;
            var grupo = b.Ajustavel || unidade is not null ? GrupoDe(b) : null;
            var minimo = unidade is null ? Piso * a.Quantidade : Math.Max(unidade.Value, Piso * a.Quantidade);
            var maximo = Math.Max(minimo, Math.Min(Teto * a.Quantidade, b.PorcaoMaxima));
            return new Item
            {
                Alimento = a, Base = b, Grupo = grupo, Original = a.Quantidade, Minimo = minimo, Maximo = maximo,
                Travado = grupo is null, Quantidade = (double)a.Quantidade
            };
        }).ToList();

        var antes = Desvio(Totais(itens), meta);

        // Contáveis sem nutriente principal (tomate, cenoura, cebola, pepino): não entram em nenhum fator, mas
        // vão para a unidade inteira mais próxima (pelo menos 1), para o cardápio poder mostrar a contagem.
        foreach (var i in itens.Where(i => i.Grupo is null && i.Unidade is not null))
        {
            var u = (double)i.Unidade!.Value;
            i.Quantidade = Math.Max(1, Math.Round(i.Quantidade / u, MidpointRounding.AwayFromZero)) * u;
        }

        Resolver(itens, meta);

        // Contáveis: unidade inteira dentro dos limites, travados; os contínuos recalculam os fatores.
        var contaveis = itens.Where(i => i.Grupo is not null && i.Unidade is not null).ToList();
        if (contaveis.Count > 0)
        {
            foreach (var c in contaveis)
            {
                var u = (double)c.Unidade!.Value;
                var quantas = Math.Max(1, Math.Round(c.Quantidade / u, MidpointRounding.AwayFromZero));
                quantas = Math.Min(Math.Max(quantas, Math.Ceiling((double)c.Minimo / u - 1e-9)), Math.Max(1, Math.Floor((double)c.Maximo / u + 1e-9)));
                c.Quantidade = quantas * u;
                c.Travado = true;
            }

            foreach (var i in itens.Where(i => i.Grupo is not null && i.Unidade is null && i.Quantidade > (double)i.Minimo && i.Quantidade < (double)i.Maximo))
            {
                i.Travado = false;
            }

            Resolver(itens, meta);
        }

        foreach (var i in itens.Where(i => i.Grupo is not null && i.Unidade is null))
        {
            i.Quantidade = (double)Legivel((decimal)i.Quantidade, i.Minimo, i.Maximo);
        }

        foreach (var i in itens)
        {
            i.Alimento.Quantidade = i.Grupo is null && i.Unidade is null ? i.Original : Math.Round((decimal)i.Quantidade, 2);
        }

        return new AjusteDoDia(dia.DiaSemana, antes[0], Desvio(Totais(itens), meta), itens.Count(i => i.Alimento.Quantidade != i.Original));
    }

    // Mínimos quadrados com pesos sobre kcal, P, C e G, um fator por grupo; quem passa de um limite trava
    // ali e o cálculo se repete com os outros.
    private static void Resolver(List<Item> itens, double[] meta)
    {
        for (var iteracao = 0; iteracao < MaximoIteracoes; iteracao++)
        {
            var fixo = new double[4];
            var porGrupo = new double[3][];
            for (var g = 0; g < 3; g++) porGrupo[g] = new double[4];

            foreach (var i in itens)
            {
                var v = i.Vetor(i.Quantidade);
                var destino = i.Travado ? fixo : porGrupo[i.Grupo!.Value];
                for (var j = 0; j < 4; j++) destino[j] += v[j];
            }

            var ativos = Enumerable.Range(0, 3).Where(g => porGrupo[g].Any(x => x != 0)).ToList();
            if (ativos.Count == 0)
            {
                return;
            }

            var fatores = MinimosQuadrados(ativos.Select(g => porGrupo[g]).ToList(), fixo, meta);
            var mudou = false;
            foreach (var i in itens.Where(i => !i.Travado))
            {
                var novo = i.Quantidade * fatores[ativos.IndexOf(i.Grupo!.Value)];
                if (novo <= (double)i.Minimo || novo >= (double)i.Maximo)
                {
                    i.Quantidade = Math.Clamp(novo, (double)i.Minimo, (double)i.Maximo);
                    i.Travado = true;
                    mudou = true;
                }
                else
                {
                    i.Quantidade = novo;
                }
            }

            if (!mudou)
            {
                return;
            }
        }
    }

    // min Σ w_j ((fixo_j + Σ_k s_k v_kj) / meta_j − 1)² + λ Σ (s_k − 1)², pelas equações normais (até 3x3).
    private static double[] MinimosQuadrados(List<double[]> colunas, double[] fixo, double[] meta)
    {
        var n = colunas.Count;
        var a = new double[n, n + 1];
        for (var j = 0; j < 4; j++)
        {
            var linha = colunas.Select(c => c[j] / meta[j]).ToArray();
            var alvo = 1 - fixo[j] / meta[j];
            for (var r = 0; r < n; r++)
            {
                for (var c = 0; c < n; c++) a[r, c] += Pesos[j] * linha[r] * linha[c];
                a[r, n] += Pesos[j] * linha[r] * alvo;
            }
        }

        for (var r = 0; r < n; r++)
        {
            a[r, r] += Regularizacao;
            a[r, n] += Regularizacao;
        }

        // Eliminação de Gauss com pivô parcial (a matriz é simétrica e positiva definida pela regularização).
        for (var c = 0; c < n; c++)
        {
            var pivo = Enumerable.Range(c, n - c).MaxBy(r => Math.Abs(a[r, c]));
            for (var k = 0; k <= n; k++) (a[c, k], a[pivo, k]) = (a[pivo, k], a[c, k]);
            for (var r = 0; r < n; r++)
            {
                if (r == c) continue;
                var f = a[r, c] / a[c, c];
                for (var k = c; k <= n; k++) a[r, k] -= f * a[c, k];
            }
        }

        return Enumerable.Range(0, n).Select(r => a[r, n] / a[r, r]).ToArray();
    }

    /// <summary>Múltiplo legível dentro dos limites: 5 g a partir de 50 g, 1 g abaixo.</summary>
    public static decimal Legivel(decimal quantidade, decimal minimo, decimal maximo)
    {
        var passo = quantidade >= 50 ? 5m : 1m;
        var candidatos = new[] { Math.Floor(quantidade / passo) * passo, Math.Ceiling(quantidade / passo) * passo }
            .Where(c => c > 0 && c >= minimo - 0.0001m && c <= maximo + 0.0001m)
            .ToList();
        return candidatos.Count == 0 ? quantidade : candidatos.MinBy(c => Math.Abs(c - quantidade));
    }

    private static double[] Totais(List<Item> itens)
    {
        var t = new double[4];
        foreach (var i in itens)
        {
            var v = i.Vetor(i.Quantidade);
            for (var j = 0; j < 4; j++) t[j] += v[j];
        }

        return t;
    }

    private static double[] Desvio(double[] total, double[] meta) =>
        Enumerable.Range(0, 4).Select(j => meta[j] == 0 ? 0 : (total[j] - meta[j]) / meta[j] * 100).ToArray();
}

/// <summary>O que o ajuste fez num dia, para o log: desvio de kcal antes, desvios depois e itens mudados.</summary>
public record AjusteDoDia(string Dia, double DesvioKcalAntes, double[] DesvioDepois, int ItensMudados)
{
    public bool DentroDaMargem => Math.Abs(DesvioDepois[0]) <= 5;
}
