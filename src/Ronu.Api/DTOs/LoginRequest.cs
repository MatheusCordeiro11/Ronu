using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// Credenciais enviadas pelo cliente para autenticação.
/// </summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Informe seu email.")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Informe sua senha.")]
    public required string Senha { get; set; }
}
