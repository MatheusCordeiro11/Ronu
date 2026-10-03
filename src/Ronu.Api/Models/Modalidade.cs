namespace Ronu.Api.Models;

/// <summary>
/// Representa uma modalidade de atividade física do catálogo do sistema
/// (ex: corrida, musculação, natação). É cadastrada previamente e compartilhada
/// entre todos os usuários, não pertence a um usuário específico.
/// </summary>
public class Modalidade
{
    public int Id { get; set; }
    public required string Nome { get; set; }

    // MET (Metabolic Equivalent of Task) de referência da modalidade, usado como
    // base para estimar o gasto calórico do usuário nessa atividade. Valores e
    // premissas (aula de luta em blocos) em Data/MetsCompendio2024.cs.
    public decimal MetReferencia { get; set; }
    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; } = new List<UsuarioModalidade>();
}