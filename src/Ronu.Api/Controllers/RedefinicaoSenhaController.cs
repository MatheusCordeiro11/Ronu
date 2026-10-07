using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ronu.Api.DTOs;
using Ronu.Api.Services;

namespace Ronu.Api.Controllers;

/// <summary>
/// "Esqueci minha senha" (rotas públicas, sem [Authorize]). A regra fica no
/// ServicoRedefinicaoSenha; aqui ficam as respostas HTTP e o limite por IP
/// (LimitesRedefinicaoSenha, configurado no Program.cs).
/// </summary>
[ApiController]
[Route("api/auth")]
public class RedefinicaoSenhaController : ControllerBase
{
    // A mesma resposta exista ou não a conta, seja ela com senha ou só com
    // Google, e também acima do limite por conta.
    public const string MensagemPedido = "Se houver uma conta com esse email, enviamos as instruções para redefinir a senha.";

    private readonly ServicoRedefinicaoSenha _servico;

    public RedefinicaoSenhaController(ServicoRedefinicaoSenha servico)
    {
        _servico = servico;
    }

    [HttpPost("esqueci-senha")]
    [EnableRateLimiting(LimitesRedefinicaoSenha.PoliticaPedido)]
    public async Task<IActionResult> EsqueciSenha(EsqueciSenhaRequest request)
    {
        await _servico.SolicitarAsync(request.Email, DateTime.UtcNow);
        return Accepted(new { mensagem = MensagemPedido });
    }

    [HttpPost("redefinir-senha/verificar")]
    [EnableRateLimiting(LimitesRedefinicaoSenha.PoliticaLink)]
    public async Task<IActionResult> Verificar(VerificarRedefinicaoSenhaRequest request)
    {
        // O email só chega a quem tem o token (secreto, enviado para esse
        // mesmo email): serve para o gerenciador de senhas do navegador.
        var resultado = await _servico.VerificarAsync(request.Token, DateTime.UtcNow);
        return resultado.Motivo is null
            ? Ok(new { mensagem = "Link válido.", email = resultado.Email })
            : LinkInvalido(resultado.Motivo.Value);
    }

    [HttpPost("redefinir-senha")]
    [EnableRateLimiting(LimitesRedefinicaoSenha.PoliticaLink)]
    public async Task<IActionResult> Redefinir(RedefinirSenhaRequest request)
    {
        var motivo = await _servico.RedefinirAsync(request.Token, request.NovaSenha, DateTime.UtcNow);
        return motivo is null ? Ok(new { mensagem = "Senha alterada. Entre com a senha nova." }) : LinkInvalido(motivo.Value);
    }

    // Dizer se o link expirou ou já foi usado não revela nada: só quem tem o
    // token (secreto) chega aqui.
    private BadRequestObjectResult LinkInvalido(MotivoLinkInvalido motivo) => motivo switch
    {
        MotivoLinkInvalido.Expirado => BadRequest(new { mensagem = "Este link de redefinição expirou. Peça um novo.", motivo = "expirado" }),
        MotivoLinkInvalido.Usado => BadRequest(new { mensagem = "Este link de redefinição já foi usado. Se precisar, peça um novo.", motivo = "usado" }),
        _ => BadRequest(new { mensagem = "Este link de redefinição não é válido. Se você pediu mais de um, use o mais recente.", motivo = "invalido" })
    };
}
