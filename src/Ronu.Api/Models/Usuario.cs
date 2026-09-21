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

    // Nulo para contas criadas via login com Google, que não têm senha
    // própria — só é obrigatório para contas criadas via cadastro tradicional
    // (email/senha), preenchido nesse momento pelo AuthController.
    public string? SenhaHash { get; set; }

    // Identificador único da conta Google vinculada (claim "sub" do token),
    // nulo para contas que nunca usaram login com Google. Não usamos o email
    // como chave de vínculo com o Google porque o "sub" nunca muda, mesmo que
    // o usuário troque o email da conta Google no futuro.
    public string? GoogleId { get; set; }

    public decimal? Altura { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public string? Sexo { get; set; }

    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; } = new List<UsuarioModalidade>();
    public ICollection<ObjetivoUsuario> ObjetivosUsuario { get; set; } = new List<ObjetivoUsuario>();
    public ICollection<PreferenciaAlimentar> PreferenciasAlimentares { get; set; } = new List<PreferenciaAlimentar>();
    public ICollection<DietaIA> DietasIA { get; set; } = new List<DietaIA>();
}
