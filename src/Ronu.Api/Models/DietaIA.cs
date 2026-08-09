namespace Ronu.Api.Models;

public class DietaIA
{
    public int Id { get; set; }
    public DateTime DataGeracao { get; set; }
    public required string ConteudoJson { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}