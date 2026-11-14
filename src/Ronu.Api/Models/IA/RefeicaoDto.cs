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

    /// <summary>
    /// Plano família: a refeição é o almoço ou o jantar da casa (a mesma panela, com a porção desta pessoa). Fora do
    /// JSON quando falso, para as dietas de fora da casa continuarem iguais.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool DaCasa { get; set; }

    /// <summary>
    /// O vínculo estável com a refeição da semana da casa ("seg-almoco", "dom-jantar"): o nome é texto livre e a posição
    /// muda entre as dietas. Fora do JSON quando nulo.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ChaveCasa { get; set; }
}
