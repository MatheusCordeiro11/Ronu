using System.ComponentModel.DataAnnotations;
using Ronu.Api.Validacao;

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
    // Em centímetros. Mesma faixa da simulação (RegrasDadosCorporais).
    [Range(100.0, 250.0, ErrorMessage = RegrasDadosCorporais.MensagemAltura)]
    public required decimal Altura { get; set; }

    [Required(ErrorMessage = "Informe seu sexo.")]
    public required string Sexo { get; set; }

    public required DateOnly DataNascimento { get; set; }
    public string? RotinaDiaria { get; set; }
    public string? OrcamentoSemanal { get; set; }
}
