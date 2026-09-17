namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa uma refeição da dieta gerada pela IA,
/// contendo os alimentos que a compõem e o total de macros da refeição.
/// </summary>
public class RefeicaoDto
{
    /// <summary>
    /// Nome utilizado para identificar a refeição, como "Café da manhã",
    /// "Almoço" ou "Lanche da tarde".
    /// </summary>
    public required string Nome { get; set; }

    /// <summary>
    /// Alimentos que compõem a refeição, incluindo suas respectivas
    /// quantidades e informações nutricionais.
    /// </summary>
    public required List<AlimentoDto> Alimentos { get; set; }

    /// <summary>
    /// Valores totais de macronutrientes da refeição,
    /// calculados com base nos alimentos e suas quantidades.
    /// </summary>
    public required MacrosDto Macros { get; set; }
}
