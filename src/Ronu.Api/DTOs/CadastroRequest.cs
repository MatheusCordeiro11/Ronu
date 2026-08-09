namespace Ronu.Api.DTOs;

public class CadastroRequest
{
    public required string Nome { get; set; }
    public required string Email { get; set; }
    public required string Senha { get; set; }
}