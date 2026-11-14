using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.ListaCompras;

/// <summary>
/// TACO 2c, decisão D10: marca cada item da lista com as alergias de UMA pessoa (os chips que ela marcou). O item com
/// alergia fica na lista, marcado, para a tela dizer "tem soja: troque no painel"; esconder deixaria a lista diferente da
/// dieta. Serve à lista individual e à lista da casa (etapa 6), que usa só as alergias de quem está vendo: a de outro
/// membro seria dado de saúde dele.
/// </summary>
public static class AlergiasNaLista
{
    public static void Marcar(IEnumerable<SecaoListaComprasDto> secoes, IReadOnlyList<string> alergias)
    {
        foreach (var item in secoes.SelectMany(s => s.Itens))
        {
            foreach (var origem in item.Detalhe.Origens)
            {
                origem.Alergias = CodigosAlergia.ChipsQueTiram(BaseNutricional.Itens.Single(i => i.Id == origem.AlimentoId), alergias)
                    .Select(c => c.Codigo)
                    .ToList();
            }
            // Na ordem dos chips (a mesma da tela), sem repetir.
            item.Alergias = CodigosAlergia.Chips
                .Select(c => c.Codigo)
                .Where(c => item.Detalhe.Origens.Any(o => o.Alergias.Contains(c)))
                .ToList();
            item.TemAlergeno = item.Alergias.Count > 0;
        }
    }
}
