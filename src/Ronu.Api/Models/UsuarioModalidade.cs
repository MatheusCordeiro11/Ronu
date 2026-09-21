namespace Ronu.Api.Models;

/// <summary>
/// Tabela de associação entre Usuario e Modalidade (relação N:N).
/// </summary>
public class UsuarioModalidade
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int ModalidadeId { get; set; }
    public Modalidade Modalidade { get; set; } = null!;

    // Dias da semana em que a modalidade é praticada (1=Segunda ... 7=Domingo,
    // padrão ISO 8601). Não existe mais um campo separado de frequência — ela
    // é sempre calculada como DiasSemana.Length, para nunca haver
    // inconsistência entre "quantos dias" e "quais dias".
    public int[] DiasSemana { get; set; } = Array.Empty<int>();

    public decimal DuracaoMediaHoras { get; set; }
}
