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

    // METs dos dois blocos da aula (leve: técnica/força; intenso: rola, sparring,
    // circuito), nas modalidades modeladas como aula em blocos
    // (MetsCompendio2024.BlocosPorModalidade). Nulos = MET único, sem o seletor
    // "Ritmo da aula". Com eles, MetReferencia é o MET do ritmo "normal".
    public decimal? MetBlocoLeve { get; set; }
    public decimal? MetBlocoIntenso { get; set; }

    // Grupo do catálogo na tela (Lutas, Esportes, Condicionamento, Corpo e
    // movimento): valores em Data/CategoriasModalidade.cs.
    public required string Categoria { get; set; }
    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; } = new List<UsuarioModalidade>();
}