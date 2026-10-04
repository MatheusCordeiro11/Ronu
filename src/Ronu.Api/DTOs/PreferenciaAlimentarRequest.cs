using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// Dados enviados pelo cliente para registrar uma preferência alimentar do usuário logado.
/// Não inclui o UsuarioId: ele é obtido do token JWT no controller, nunca do corpo da requisição.
/// </summary>
public class PreferenciaAlimentarRequest
{
    [Required(ErrorMessage = "Informe o alimento.")]
    public required string Alimento { get; set; }

    // "preferido" ou "evitar" — conferido no controller.
    [Required(ErrorMessage = "Informe o tipo da preferência.")]
    public required string Tipo { get; set; }
}
