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

    /// <summary>
    /// Horário sugerido da refeição, no formato HH:mm (ex: "07:30"). Nulo em
    /// dietas geradas antes deste campo existir — por isso não é required:
    /// as dietas já salvas em DietasIA.ConteudoJson são desserializadas com
    /// esta mesma classe e quebrariam se o campo fosse obrigatório.
    /// </summary>
    public string? Horario { get; set; }
}
