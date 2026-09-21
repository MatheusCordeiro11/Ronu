namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa a modalidade de exercício praticada pelo usuário, contendo
/// os dados necessários para o cálculo de gasto calórico e planejamento da dieta.
/// </summary>
public class ModalidadeContextoDto
{
    public required string Nome { get; set; }
    public required int FrequenciaSemanal { get; set; }
    public required decimal MetReferencia { get; set; }
    public required decimal DuracaoHoras { get; set; }
}
