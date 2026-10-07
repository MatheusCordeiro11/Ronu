using System.ComponentModel.DataAnnotations;
using Ronu.Api.Validacao;

namespace Ronu.Api.DTOs;

/// <summary>Pedido de "esqueci minha senha".</summary>
public class EsqueciSenhaRequest
{
    [Required(ErrorMessage = "Informe seu email.")]
    public required string Email { get; set; }
}

/// <summary>
/// Confere o link de redefinição antes de mostrar o formulário. O token vai
/// no corpo (POST), nunca na URL da API, para não aparecer em logs.
/// </summary>
public class VerificarRedefinicaoSenhaRequest
{
    [Required(ErrorMessage = "Link de redefinição inválido.")]
    public required string Token { get; set; }
}

/// <summary>Define a senha nova a partir do link de redefinição.</summary>
public class RedefinirSenhaRequest
{
    [Required(ErrorMessage = "Link de redefinição inválido.")]
    public required string Token { get; set; }

    // Mesmas regras do cadastro (RegrasSenha).
    [Required(ErrorMessage = "Informe a nova senha.")]
    [MinLength(RegrasSenha.TamanhoMinimo, ErrorMessage = RegrasSenha.MensagemCurta)]
    [SenhaNaoLongaDemais]
    public required string NovaSenha { get; set; }
}
