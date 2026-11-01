namespace Ronu.Api.Models.IA;

/// <summary>
/// A última troca de alimento de uma dieta, para o "Desfazer". Vale só enquanto a revisão da dieta
/// (DietaIA.Revisao) for a que a troca gravou: qualquer gravação depois dela a substitui.
/// </summary>
public class UltimaTrocaDto
{
    public required int Revisao { get; set; }
    public required List<ItemAnteriorDto> Itens { get; set; }
}

/// <summary>Um item como estava antes da troca, na posição em que foi trocado.</summary>
public class ItemAnteriorDto
{
    public required int Dia { get; set; }
    public required int Refeicao { get; set; }
    public required int Item { get; set; }
    public required AlimentoDto Anterior { get; set; }
}
