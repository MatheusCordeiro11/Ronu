namespace Ronu.Api.Models;

public class ObjetivoUsuario
{
    public int Id { get; set; }
    public decimal Peso { get; set; }
    public required string Objetivo { get; set; }
    public DateTime DataRegistro { get; set; }

    public int UsuarioId { get; set; }
    public required Usuario Usuario { get; set; }
}