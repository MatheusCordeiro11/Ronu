using System.ComponentModel.DataAnnotations;

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

    // Quando a senha foi trocada pela última vez (redefinição). Tokens JWT
    // emitidos antes disso deixam de valer (ValidacaoSessaoJwt). Nulo se a
    // senha nunca foi trocada.
    public DateTime? SenhaAlteradaEm { get; set; }

    // Prazos de guarda (docs/privacidade.md). Nulo nas contas anteriores ao campo: a limpeza de conta sem aceite
    // só apaga quando CriadoEm é conhecido, para nunca tomar uma conta antiga por recém-criada.
    public DateTime? CriadoEm { get; set; }

    // Atualizado a cada login (senha ou Google). O token vale 2 horas, então o último login é um bom retrato do
    // último uso, sem gravar nada a cada requisição. Nulo até o primeiro login depois do campo existir.
    public DateTime? UltimoLoginEm { get; set; }

    public decimal? Altura { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public string? Sexo { get; set; }
    [MaxLength(2)]
    public string Estado { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? RotinaDiaria { get; set; }
    public string? OrcamentoSemanal { get; set; }

    // Como é o dia fora do treino: "sentado", "em_pe", "andando" ou "bracal"
    // (CalculadoraManutencao.FatoresPorNivel). Nulo = não respondeu: a fórmula
    // usa o fator de antes da pergunta (CalculadoraManutencao.FatorPadrao).
    [MaxLength(20)]
    public string? NivelAtividade { get; set; }

    // Horários estruturados (RegrasHorario), no lugar da rotina em texto no prompt. Nulos = não informados.
    public TimeOnly? HorarioAcordar { get; set; }
    public TimeOnly? HorarioDormir { get; set; }

    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; } = new List<UsuarioModalidade>();
    public ICollection<ObjetivoUsuario> ObjetivosUsuario { get; set; } = new List<ObjetivoUsuario>();
    public ICollection<PreferenciaAlimentar> PreferenciasAlimentares { get; set; } = new List<PreferenciaAlimentar>();
    public ICollection<DietaIA> DietasIA { get; set; } = new List<DietaIA>();
}
