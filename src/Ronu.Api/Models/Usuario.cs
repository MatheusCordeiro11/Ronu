namespace Ronu.Api.Models;

/// <summary>
/// Representa um usuário do sistema. É a entidade central: modalidades, objetivos,
/// preferências alimentares e dietas geradas por IA são todos vinculados a um usuário.
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public required string Email { get; set; }

    // Nunca armazenamos a senha em texto puro, apenas o hash gerado pelo BCrypt no cadastro.
    public required string SenhaHash { get; set; }

    // Altura, data de nascimento e sexo são opcionais porque não são coletados no
    // cadastro inicial: esses dados são preenchidos depois, durante o onboarding.
    public decimal? Altura { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public string? Sexo { get; set; }

    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; } = new List<UsuarioModalidade>();
    public ICollection<ObjetivoUsuario> ObjetivosUsuario { get; set; } = new List<ObjetivoUsuario>();
    public ICollection<PreferenciaAlimentar> PreferenciasAlimentares { get; set; } = new List<PreferenciaAlimentar>();
    public ICollection<DietaIA> DietasIA { get; set; } = new List<DietaIA>();
}