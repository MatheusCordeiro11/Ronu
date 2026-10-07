using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.Models;

/// <summary>
/// Um pedido de "esqueci minha senha" de uma conta existente. Pedidos para
/// emails sem conta não geram registro (nem guardam o email digitado).
/// Conta com senha: Tipo "link", com o token de uso único (só o hash SHA-256
/// fica no banco) e validade. Conta só com Google: Tipo "aviso_google", sem
/// token — o email só avisa que a conta usa o login com Google. Os dois tipos
/// contam no limite de pedidos por conta.
/// </summary>
public class PedidoRedefinicaoSenha
{
    public const string TipoLink = "link";
    public const string TipoAvisoGoogle = "aviso_google";

    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    [MaxLength(20)]
    public required string Tipo { get; set; }

    // SHA-256 do token, em hexadecimal (64 caracteres). Nulo no aviso_google.
    [MaxLength(64)]
    public string? TokenHash { get; set; }

    public DateTime CriadoEm { get; set; }

    // Nulo no aviso_google.
    public DateTime? ExpiraEm { get; set; }

    public DateTime? UsadoEm { get; set; }

    // Preenchido quando um pedido mais novo ou a própria redefinição o
    // substitui: só o link mais recente vale.
    public DateTime? InvalidadoEm { get; set; }
}
