namespace Ronu.Api.DTOs;

/// <summary>
/// Retorno de um login bem-sucedido: o token JWT que o cliente deve usar
/// nas próximas requisições autenticadas, e um resumo do usuário logado.
/// </summary>
public class LoginResponse
{
    public required string Token { get; set; }
    public required UsuarioResumo Usuario { get; set; }
    public bool PrecisaInformarEstado { get; set; }

    // A conta ainda não aceitou a versão vigente do consentimento geral (conta Google nova, conta anterior ao
    // consentimento ou texto novo): a tela leva ao aceite antes de tudo.
    public bool PrecisaConsentimento { get; set; }
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