namespace Ronu.Api.Models.IA;

/// <summary>
/// As metas de uma dieta gerada (linha de MetasDieta), como entrada da meta
/// calórica adaptativa: o que a pessoa recebeu para comer em cada dia da
/// semana enquanto essa dieta era a mais recente.
/// </summary>
public class MetaDietaRegistroDto
{
    public required DateTime DataGeracao { get; set; }
    public required int VersaoFormula { get; set; }

    // Índice 0 = segunda ... 6 = domingo.
    public required decimal[] MetasPorDia { get; set; }

    // Manutenção da fórmula de cada dia; nula quando desconhecida.
    public decimal[]? ManutencoesPorDia { get; set; }
}
