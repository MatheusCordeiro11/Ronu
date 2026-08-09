namespace Ronu.Api.DTOs;

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Senha { get; set; }
}