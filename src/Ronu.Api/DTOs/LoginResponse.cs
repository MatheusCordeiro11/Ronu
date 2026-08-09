namespace Ronu.Api.DTOs;

public class LoginResponse
{
    public required string Token { get; set; }
    public required UsuarioResumo Usuario { get; set; }
}

public class UsuarioResumo
{
    public int Id { get; set; }
    public required string Nome { get; set; }
}