namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa um alimento dentro de uma refeição da dieta gerada pela IA.
/// </summary>
public class AlimentoDto
{
    public required string Nome { get; set; }

    // Quantidade e Unidade ficam separados (em vez de uma string única tipo
    // "150g") de propósito: permite recalcular macros se o usuário ajustar
    // a quantidade no futuro, e permite somar alimentos repetidos na semana
    // para gerar uma lista de compras automática — nenhum dos dois seria
    // possível fazendo parsing de texto livre.
    public required decimal Quantidade { get; set; }
    public required string Unidade { get; set; }
}
