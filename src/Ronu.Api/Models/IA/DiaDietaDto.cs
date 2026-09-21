namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa um dia da dieta semanal gerada pela IA, contendo as refeições
/// planejadas para esse dia e o total de macros consolidado.
/// </summary>
public class DiaDietaDto
{
    public required string DiaSemana { get; set; }
    public required List<RefeicaoDto> Refeicoes { get; set; }
    public required MacrosDto TotalDoDia { get; set; }

    // Meta calculada especificamente para este dia (TMB + gasto de treino
    // deste dia + ajuste por objetivo) — varia entre dias de treino e
    // descanso. Diferente de TotalDoDia, que é o que a IA de fato montou.
    public required MacrosDto MetaCalculada { get; set; }
}
