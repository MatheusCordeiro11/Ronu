using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

/// <summary>
/// O texto de consentimento vigente e o aceite da pessoa logada. Exige login, mas não o próprio consentimento
/// ([DispensaConsentimento]): é por aqui que quem ainda não aceitou aceita.
/// </summary>
[ApiController]
[Route("api/consentimentos")]
[Authorize]
[DispensaConsentimento]
public class ConsentimentosController : ControllerBase
{
    private const string MensagemTipoInvalido = "Tipo de autorização inválido.";

    private readonly ApplicationDbContext _context;

    public ConsentimentosController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Do token, nunca do corpo: ninguém aceita (nem lê o aceite) em nome de outra conta.
    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("{tipo}")]
    public async Task<IActionResult> Consultar(string tipo)
    {
        if (!TextosConsentimento.TipoValido(tipo))
        {
            return BadRequest(new { mensagem = MensagemTipoInvalido });
        }

        var vigente = TextosConsentimento.VersaoVigente(tipo);
        var texto = TextosConsentimento.TextoVigente(tipo);
        var aceito = await _context.Consentimentos
            .AsNoTracking()
            .Where(c => c.UsuarioId == UsuarioIdLogado && c.Tipo == tipo && c.RevogadoEm == null)
            .OrderByDescending(c => c.Versao).ThenByDescending(c => c.AceitoEm)
            .Select(c => new AceiteResponse { Versao = c.Versao, AceitoEm = c.AceitoEm })
            .FirstOrDefaultAsync();

        return Ok(new ConsentimentoResponse
        {
            Tipo = tipo,
            VersaoVigente = vigente,
            Titulo = texto.Titulo,
            Texto = texto.Corpo,
            Aceito = aceito,
            PrecisaAceitar = aceito?.Versao != vigente
        });
    }

    /// <summary>
    /// Grava o aceite da versão que a tela mostrou. Só a vigente é aceita: uma versão velha (a tela ficou aberta
    /// enquanto o texto mudou) dá 409, para a pessoa ler o texto novo. Aceitar de novo a mesma versão não duplica.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(LimitesConsentimento.PoliticaAceite)]
    public async Task<IActionResult> Aceitar(ConsentimentoRequest request)
    {
        if (!TextosConsentimento.TipoValido(request.Tipo))
        {
            return BadRequest(new { mensagem = MensagemTipoInvalido });
        }

        var vigente = TextosConsentimento.VersaoVigente(request.Tipo);
        if (request.Versao != vigente)
        {
            return Conflict(new { mensagem = RegrasConsentimento.MensagemVersaoMudou, versaoVigente = vigente });
        }

        if (await RegrasConsentimento.TemAceiteVigenteAsync(_context, UsuarioIdLogado, request.Tipo))
        {
            return NoContent();
        }

        _context.Consentimentos.Add(new Consentimento
        {
            UsuarioId = UsuarioIdLogado,
            Tipo = request.Tipo,
            Versao = vigente,
            AceitoEm = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Duas abas aceitando ao mesmo tempo: o índice único parcial barrou a segunda, e o aceite já está gravado.
        }

        return NoContent();
    }
}
