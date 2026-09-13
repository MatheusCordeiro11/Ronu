namespace Ronu.Api.Models;

/// <summary>
/// Registro histórico do objetivo (ex: emagrecimento, hipertrofia) e peso do usuário
/// em uma data específica. Cada novo registro representa uma atualização, permitindo
/// acompanhar a evolução do usuário ao longo do tempo.
/// </summary>
public class ObjetivoUsuario
{
    public int Id { get; set; }
    public decimal Peso { get; set; }
    public required string Objetivo { get; set; }
    public DateTime DataRegistro { get; set; }

    public int UsuarioId { get; set; }

    // "= null!" em vez de "required": o EF Core só precisa do UsuarioId (FK) para
    // salvar o registro. O objeto Usuario só vem preenchido quando a consulta usa .Include().
    public Usuario Usuario { get; set; } = null!;
}