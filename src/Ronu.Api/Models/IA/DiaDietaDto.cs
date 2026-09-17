namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa um dia da dieta semanal gerada pela IA, contendo as refeições
/// planejadas para esse dia e o total de macros consolidado.
/// </summary>
public class DiaDietaDto
{
    /// <summary>
    /// Dia da semana ao qual este planejamento se refere, como "Segunda-feira".
    /// </summary>
    public required string DiaSemana { get; set; }

    /// <summary>
    /// Refeições planejadas para este dia.
    /// </summary>
    public required List<RefeicaoDto> Refeicoes { get; set; }

    // TotalDoDia não é calculado dinamicamente somando Refeicoes toda vez que
    // for lido — fica armazenado como valor já pronto, porque a dieta inteira
    // é gerada uma vez pela IA e depois só lida (nunca recalculada em tempo
    // real). Calcular sob demanda seria desperdício de processamento repetido
    // sem necessidade.
    /// <summary>
    /// Soma dos macros de todas as refeições do dia.
    /// </summary>
    public required MacrosDto TotalDoDia { get; set; }
}
