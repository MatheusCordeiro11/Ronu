namespace Ronu.Api.DTOs;

/// <summary>
/// Retorno de um login bem-sucedido: o token JWT que o cliente deve usar
/// nas próximas requisições autenticadas, e um resumo do usuário logado.
/// </summary>
public class LoginResponse
{
    public required string Token { get; set; }
    public required UsuarioResumo Usuario { get; set; }
}

/// <summary>
/// Versão resumida do usuário exposta ao cliente após o login.
/// Nunca inclui a senha ou o hash da senha.
/// </summary>
public class UsuarioResumo
{
    public int Id { get; set; }
    public required string Nome { get; set; }
}