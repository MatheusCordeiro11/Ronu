namespace Ronu.Api.Models;

/// <summary>
/// Preferência alimentar de um usuário (ex: um alimento que ele evita ou prefere).
/// Usada como entrada para a geração de dietas personalizadas via IA.
/// </summary>
public class PreferenciaAlimentar
{
    public int Id { get; set; }
    public required string Alimento { get; set; }

    // "preferido", "nao_como" (bloqueio) ou "evitar" (leve): ver TiposPreferencia.
    public required string Tipo { get; set; }

    public int UsuarioId { get; set; }

    // "= null!" em vez de "required": o EF Core só precisa do UsuarioId (FK) para
    // salvar o registro. O objeto Usuario só vem preenchido quando a consulta usa .Include().
    public Usuario Usuario { get; set; } = null!;
}