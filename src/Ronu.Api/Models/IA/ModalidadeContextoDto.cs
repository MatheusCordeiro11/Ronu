namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa a modalidade de exercício praticada pelo usuário, contendo
/// os dados necessários para o cálculo de gasto calórico e planejamento da dieta.
/// </summary>
public class ModalidadeContextoDto
{
    public required string Nome { get; set; }
    public required decimal MetReferencia { get; set; }
    public required decimal DuracaoHoras { get; set; }

    // 1=Segunda ... 7=Domingo (ISO 8601). Usado para calcular uma meta
    // calórica diferente por dia da semana, em vez de uma meta única semanal.
    public required int[] DiasSemana { get; set; }
}
