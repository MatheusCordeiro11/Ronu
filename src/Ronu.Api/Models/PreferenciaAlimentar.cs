namespace Ronu.Api.Models;

public class PreferenciaAlimentar
{
    public int Id { get; set; }
    public required string Alimento { get; set; }
    public required string Tipo { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}