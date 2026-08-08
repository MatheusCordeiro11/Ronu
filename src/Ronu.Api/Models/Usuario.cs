namespace Ronu.Api.Models;

public class Usuario
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public required string Email { get; set; }
    public required string SenhaHash { get; set; }
    public decimal Altura { get; set; }
    public DateOnly DataNascimento { get; set; }
    public required string Sexo { get; set; }

    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; } = new List<UsuarioModalidade>();
    public ICollection<ObjetivoUsuario> ObjetivosUsuario { get; set; } = new List<ObjetivoUsuario>();
    public ICollection<PreferenciaAlimentar> PreferenciasAlimentares { get; set; } = new List<PreferenciaAlimentar>();
    public ICollection<DietaIA> DietasIA { get; set; } = new List<DietaIA>();
}