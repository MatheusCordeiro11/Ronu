namespace Ronu.Api.DTOs;

/// <summary>
/// Dados pessoais usados para calcular a taxa metabólica basal (Mifflin-St
/// Jeor) e a idade do usuário. Sexo deve ser exatamente "Masculino" ou
/// "Feminino" — esse valor é comparado por igualdade de string no cálculo
/// de TMB (GeradorDietaGemini), então qualquer variação de grafia faz o
/// sistema calcular com a fórmula errada silenciosamente.
/// </summary>
public class PerfilRequest
{
    public required decimal Altura { get; set; }
    public required string Sexo { get; set; }
    public required DateOnly DataNascimento { get; set; }
}
