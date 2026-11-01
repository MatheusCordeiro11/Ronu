using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.ListaCompras;

/// <summary>
/// Quais marcações "já tenho" deixam de valer depois de uma troca de alimento (decisão D5, 2026-10-26): as dos
/// itens que saíram da lista e as dos que passaram a pedir MAIS do que antes. Se a quantidade diminuiu, quem já
/// tem continua tendo, e a marca fica. A comparação é pela quantidade arredondada da lista, a que a pessoa compra.
/// Vale igual para trocar, desfazer e voltar ao original.
/// </summary>
public static class MarcacoesAposTroca
{
    public static IReadOnlyList<string> QueCaem(DietaSemanalDto antes, DietaSemanalDto depois, string? uf)
    {
        var listaAntes = Itens(MontadorListaCompras.Montar(antes, uf));
        var listaDepois = Itens(MontadorListaCompras.Montar(depois, uf));

        return listaAntes
            .Where(a => !listaDepois.TryGetValue(a.Key, out var d) || d.Unidade != a.Value.Unidade || d.Quantidade > a.Value.Quantidade)
            .Select(a => a.Key)
            .ToList();
    }

    private static Dictionary<string, ItemListaComprasDto> Itens(ListaComprasMontada lista) =>
        lista.Secoes.SelectMany(s => s.Itens).ToDictionary(i => i.Chave);
}
