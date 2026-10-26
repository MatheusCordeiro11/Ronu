using Ronu.Api.Data;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Fase 3 da base nutricional: as kcal e os macros da dieta passam a ser
/// calculados pelo C# a partir da base (quantidade ÷ 100 × valor por 100 g),
/// no lugar dos números que o Gemini declara. No g9a (2026-10-25), nas
/// refeições inteiras pela base, o declarado diferia do cardápio real em 12,9%
/// na média.
///
/// Sem arredondar: os valores são gravados exatos e arredondados só na
/// exibição (dashboard), para o total do dia ser exatamente a soma das
/// refeições, e a refeição, a soma dos itens. Para as bebidas, o valor por
/// 100 g da base vale como 100 ml (BaseNutricional).
///
/// Só para dietas novas: uma dieta salva nunca é recalculada (as antigas, sem
/// AlimentoId, mostram o que o Gemini declarou na época).
/// </summary>
public static class CalculoPelaBase
{
    private static readonly IReadOnlyDictionary<string, AlimentoBase> PorId =
        BaseNutricional.Itens.ToDictionary(i => i.Id);

    public static void Calcular(List<DiaDietaDto> dias)
    {
        foreach (var dia in dias)
        {
            foreach (var refeicao in dia.Refeicoes)
            {
                foreach (var alimento in refeicao.Alimentos)
                {
                    // Numa dieta nova, todo item vem da lista fechada: sem id na base, a resposta é inválida
                    // (nova tentativa, depois 503), nunca um item com 0 kcal.
                    if (alimento.AlimentoId is null || !PorId.TryGetValue(alimento.AlimentoId, out var item))
                    {
                        throw new RespostaIaInvalidaException($"Alimento sem item na base: '{alimento.Nome}' ({alimento.AlimentoId ?? "sem AlimentoId"}).");
                    }

                    var fator = alimento.Quantidade / 100m;
                    alimento.Macros = new MacrosDto
                    {
                        Calorias = item.Kcal * fator,
                        ProteinasG = item.ProteinaG * fator,
                        CarboidratosG = item.CarboidratoG * fator,
                        GordurasG = item.GorduraG * fator
                    };
                }

                refeicao.Macros = Somar(refeicao.Alimentos.Select(a => a.Macros!));
            }

            dia.TotalDoDia = Somar(dia.Refeicoes.Select(r => r.Macros));
        }
    }

    public static MacrosDto Somar(IEnumerable<MacrosDto> partes)
    {
        var lista = partes.ToList();
        return new MacrosDto
        {
            Calorias = lista.Sum(m => m.Calorias),
            ProteinasG = lista.Sum(m => m.ProteinasG),
            CarboidratosG = lista.Sum(m => m.CarboidratosG),
            GordurasG = lista.Sum(m => m.GordurasG)
        };
    }
}
