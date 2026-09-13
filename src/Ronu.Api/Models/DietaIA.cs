namespace Ronu.Api.Models;

/// <summary>
/// Dieta gerada por IA para um usuário, salva em um determinado momento.
/// O conteúdo é armazenado como JSON bruto para manter flexibilidade no formato
/// retornado pela IA, sem precisar de uma tabela relacional rígida para cada campo.
/// </summary>
public class DietaIA
{
    public int Id { get; set; }
    public DateTime DataGeracao { get; set; }
    public required string ConteudoJson { get; set; }

    public int UsuarioId { get; set; }

    // "= null!" em vez de "required": o EF Core só precisa do UsuarioId (FK) para
    // salvar o registro. O objeto Usuario só vem preenchido quando a consulta usa .Include().
    public Usuario Usuario { get; set; } = null!;
}