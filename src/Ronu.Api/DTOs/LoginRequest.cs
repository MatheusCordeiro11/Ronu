namespace Ronu.Api.DTOs;

/// <summary>
/// Credenciais enviadas pelo cliente para autenticação.
/// </summary>
public class LoginRequest
{
    public required string Email { get; set; }
    public required string Senha { get; set; }
}