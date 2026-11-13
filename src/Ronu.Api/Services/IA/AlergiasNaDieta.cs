using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Os alimentos de uma dieta salva que têm uma alergia marcada pela pessoa (TACO 2c). Marcar um chip não muda a dieta
/// já gerada, então o painel avisa e sugere trocar ou gerar de novo. Itens sem AlimentoId (dietas anteriores à lista
/// fechada) ficam de fora: sem o item da base, não há marca de alérgeno para conferir.
/// </summary>
public static class AlergiasNaDieta
{
    private static readonly IReadOnlyDictionary<string, AlimentoBase> PorId = BaseNutricional.Itens.ToDictionary(i => i.Id);

    public static List<AlergiaNaDietaDto> Calcular(DietaSemanalDto dieta, IReadOnlyList<string> alergias)
    {
        if (alergias.Count == 0)
        {
            return [];
        }

        return dieta.Dias
            .SelectMany(d => d.Refeicoes)
            .SelectMany(r => r.Alimentos)
            .Where(a => a.AlimentoId is not null && PorId.ContainsKey(a.AlimentoId))
            .GroupBy(a => a.AlimentoId!)
            .Select(g => (Item: PorId[g.Key], Vezes: g.Count()))
            .Select(x => (x.Item, x.Vezes, Chips: CodigosAlergia.ChipsQueTiram(x.Item, alergias)))
            .Where(x => x.Chips.Count > 0)
            .Select(x => new AlergiaNaDietaDto
            {
                AlimentoId = x.Item.Id,
                Nome = x.Item.Nome,
                Alergias = x.Chips.Select(c => c.Codigo).ToList(),
                Vezes = x.Vezes
            })
            .ToList();
    }

    /// <summary>
    /// O primeiro item que mudou entre as duas versões da dieta e voltou com uma alergia marcada: o "Desfazer" e o
    /// "Voltar ao original" não podem trazer de volta o que a pessoa marcou como alergia depois da troca. Nulo
    /// quando nada volta com alergia.
    /// </summary>
    public static (AlimentoDto Alimento, CodigosAlergia.Chip Chip)? QueVolta(DietaSemanalDto antes, DietaSemanalDto depois, IReadOnlyList<string> alergias)
    {
        if (alergias.Count == 0)
        {
            return null;
        }

        for (var d = 0; d < depois.Dias.Count; d++)
        {
            for (var r = 0; r < depois.Dias[d].Refeicoes.Count; r++)
            {
                var itens = depois.Dias[d].Refeicoes[r].Alimentos;
                for (var i = 0; i < itens.Count; i++)
                {
                    var novo = itens[i];
                    var anterior = antes.Dias[d].Refeicoes[r].Alimentos[i];
                    if (novo.AlimentoId is null || novo.AlimentoId == anterior.AlimentoId || !PorId.TryGetValue(novo.AlimentoId, out var item))
                    {
                        continue;
                    }

                    var chips = CodigosAlergia.ChipsQueTiram(item, alergias);
                    if (chips.Count > 0)
                    {
                        return (novo, chips[0]);
                    }
                }
            }
        }

        return null;
    }
}
