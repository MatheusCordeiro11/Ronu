using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;
using Ronu.Api.Services.Conta;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

/// <summary>
/// Apagar a conta e baixar os dados. Exigem login, mas não o consentimento ([DispensaConsentimento]): são direitos de
/// quem nunca aceitou ou quer revogar (LGPD, art. 18), e revogar o consentimento geral é justamente apagar a conta.
/// </summary>
[ApiController]
[Route("api/conta")]
[Authorize]
[DispensaConsentimento]
public class ContaController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ContaController> _logger;

    public ContaController(ApplicationDbContext context, ILogger<ContaController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Do token, nunca do corpo: ninguém apaga nem baixa a conta de outra pessoa.
    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Apaga a conta e tudo dela. Os erros de confirmação são 400, não 401: o 401 faria a tela encerrar a sessão, e a
    /// pessoa só errou a senha. Depois de apagada, todo token da conta deixa de valer (ValidacaoSessaoJwt).
    /// </summary>
    [HttpDelete]
    [EnableRateLimiting(LimitesConta.PoliticaExclusao)]
    public async Task<IActionResult> Apagar(ExclusaoContaRequest request)
    {
        var usuario = await _context.Usuarios.AsNoTracking()
            .Where(u => u.Id == UsuarioIdLogado)
            .Select(u => new { u.SenhaHash })
            .FirstOrDefaultAsync();
        if (usuario is null)
        {
            return NoContent();
        }

        if (usuario.SenhaHash is not null)
        {
            if (string.IsNullOrEmpty(request.Senha))
            {
                return BadRequest(new { mensagem = "Digite sua senha para confirmar." });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash))
            {
                return BadRequest(new { mensagem = "Senha incorreta." });
            }
        }
        else if (!string.Equals(request.Confirmacao?.Trim(), ExclusaoContaRequest.FraseConfirmacao, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { mensagem = $"Digite \"{ExclusaoContaRequest.FraseConfirmacao}\" para confirmar." });
        }

        await ExclusaoConta.ApagarAsync(_context, UsuarioIdLogado);

        // Só o id: nada do que a conta tinha vai para o log.
        _logger.LogInformation("Conta apagada pelo próprio usuário {UsuarioId}", UsuarioIdLogado);
        return NoContent();
    }

    [HttpGet("exportacao")]
    [EnableRateLimiting(LimitesConta.PoliticaExportacao)]
    public async Task<IActionResult> Exportar()
    {
        var agora = DateTime.UtcNow;
        var arquivo = await ExportacaoConta.GerarAsync(_context, UsuarioIdLogado, agora);
        if (arquivo is null)
        {
            return NotFound(new { mensagem = "Conta não encontrada." });
        }

        // Dado de saúde: nenhum cache (navegador, proxy) guarda uma cópia.
        Response.Headers.CacheControl = "no-store";
        return File(arquivo, "application/json", $"ronu-dados-{agora:yyyy-MM-dd}.json");
    }
}
