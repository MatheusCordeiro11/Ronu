namespace Ronu.Api.Models;

/// <summary>
/// Tabela de associação entre Usuario e Modalidade (relação N:N), guardando também
/// a frequência semanal com que o usuário pratica aquela modalidade.
/// </summary>
public class UsuarioModalidade
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    // Inicializada com "= null!" em vez de "required": o EF Core só precisa do
    // UsuarioId (a FK) para persistir o registro. O objeto Usuario completo só é
    // preenchido quando a consulta usa .Include(), então não faz sentido exigi-lo
    // como obrigatório na criação do objeto em memória.
    public Usuario Usuario { get; set; } = null!;

    public int ModalidadeId { get; set; }
    public Modalidade Modalidade { get; set; } = null!;

    public int FrequenciaSemanal { get; set; }

    public decimal DuracaoMediaHoras { get; set; }
}