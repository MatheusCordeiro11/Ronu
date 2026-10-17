using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Monta a ComposicaoMetaDto de um dia a partir das parcelas exatas da
/// fórmula (CalculadoraManutencao.ComposicaoPorDia), do percentual da meta
/// adaptativa e da meta final (depois do piso de carboidrato).
/// </summary>
public static class ComposicaoMeta
{
    /// <param name="formula">Parcelas exatas da fórmula naquele dia.</param>
    /// <param name="percentualAjuste">Fração da meta adaptativa (−0,03 = −3%).</param>
    /// <param name="metaFinal">MetaCalculada.Calorias do dia, com ajuste e piso.</param>
    public static ComposicaoMetaDto Montar(ComposicaoFormulaDia formula, decimal percentualAjuste, decimal metaFinal)
    {
        // Mesma conta do GeradorDietaGemini.MontarMetasPorDia: o ajuste incide
        // sobre a meta da fórmula; o piso é o que sobrou acima disso.
        var ajuste = formula.MetaFormula * percentualAjuste;
        var piso = metaFinal - formula.MetaFormula * (1 + percentualAjuste);

        var exatas = new List<decimal> { formula.GastoRepouso, formula.AtividadeForaDoTreino };
        exatas.AddRange(formula.Treinos.Select(t => t.Kcal));
        exatas.AddRange(new[] { formula.Objetivo, ajuste, piso });

        // As primeiras partes (repouso, dia a dia, treino) ficam com o
        // arredondamento comum, o mesmo da simulação; a diferença para a meta
        // mostrada cai na última parte diferente de zero.
        var meta = ArredondamentoPartes.Arredondar(metaFinal);
        var partes = ArredondamentoPartes.AjustandoUltima(exatas, meta);
        var n = formula.Treinos.Count;

        return new ComposicaoMetaDto
        {
            GastoRepouso = partes[0],
            AtividadeForaDoTreino = partes[1],
            Treino = formula.Treinos.Select((t, i) => new TreinoComposicaoDto
            {
                Nome = t.Nome,
                Horas = t.Horas,
                Kcal = partes[2 + i]
            }).ToList(),
            Objetivo = partes[2 + n],
            AjusteProgresso = partes[3 + n],
            PisoCarboidrato = partes[4 + n],
            Meta = meta
        };
    }
}
