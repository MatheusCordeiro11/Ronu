using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;
using System.Security.Claims;
using System.Text.Json;

namespace Ronu.Api.Controllers;

/// <summary>
/// Gera e consulta dietas semanais via IA para o usuário logado. Todas as
/// rotas exigem autenticação, pois os dados pertencem sempre a um usuário
/// específico.
/// </summary>
[ApiController]
[Route("api/dietas")]
[Authorize]
public class DietasController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IContextoDietaBuilder _contextoBuilder;
    private readonly IGeradorDietaIA _geradorDieta;
    private readonly IRepositorioDietaIA _repositorioDieta;

    public DietasController(
        ApplicationDbContext context,
        IContextoDietaBuilder contextoBuilder,
        IGeradorDietaIA geradorDieta,
        IRepositorioDietaIA repositorioDieta)
    {
        _context = context;
        _contextoBuilder = contextoBuilder;
        _geradorDieta = geradorDieta;
        _repositorioDieta = repositorioDieta;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Gera uma nova dieta semanal via IA para o usuário logado, com base no
    /// objetivo, modalidades e preferências já cadastrados. Mantém só as 3
    /// dietas mais recentes por usuário.
    /// </summary>
    [HttpPost("gerar")]
    public async Task<IActionResult> Gerar()
    {
        var usuario = await _context.Usuarios
            .FirstAsync(u => u.Id == UsuarioIdLogado);

        // Checagem feita aqui (não dentro de ContextoDietaBuilder) porque é
        // uma regra de negócio da requisição, não um detalhe de como buscar
        // dados no banco — e permite devolver um erro HTTP claro, em vez de
        // uma exceção genérica de null reference mais adiante.
        if (usuario.Altura is null || usuario.Sexo is null || usuario.DataNascimento is null)
        {
            return BadRequest(new { mensagem = "Complete seu perfil (altura, sexo e data de nascimento) antes de gerar uma dieta." });
        }

        var temObjetivo = await _context.ObjetivosUsuario
            .AnyAsync(o => o.UsuarioId == UsuarioIdLogado);

        if (!temObjetivo)
        {
            return BadRequest(new { mensagem = "Registre um objetivo antes de gerar uma dieta." });
        }

        var contexto = await _contextoBuilder.ConstruirAsync(UsuarioIdLogado);
        var dieta = await _geradorDieta.GerarDietaAsync(contexto);
        await _repositorioDieta.SalvarAsync(UsuarioIdLogado, dieta);

        var response = new DietaResponse
        {
            DataGeracao = DateTime.UtcNow,
            Dieta = dieta
        };

        return Ok(response);
    }

    /// <summary>
    /// Retorna a dieta mais recente gerada para o usuário logado.
    /// </summary>
    [HttpGet("atual")]
    public async Task<IActionResult> ObterAtual()
    {
        var dietaIA = await _context.DietasIA
            .Where(d => d.UsuarioId == UsuarioIdLogado)
            .OrderByDescending(d => d.DataGeracao)
            .FirstOrDefaultAsync();

        if (dietaIA is null)
        {
            return NotFound(new { mensagem = "Nenhuma dieta gerada ainda." });
        }

        var response = new DietaResponse
        {
            DataGeracao = dietaIA.DataGeracao,
            Dieta = JsonSerializer.Deserialize<DietaSemanalDto>(dietaIA.ConteudoJson)!
        };

        return Ok(response);
    }

    /// <summary>
    /// Retorna o histórico de dietas geradas para o usuário logado (até 3,
    /// da mais recente para a mais antiga), conforme a política de retenção
    /// aplicada por IRepositorioDietaIA.
    /// </summary>
    [HttpGet("historico")]
    public async Task<IActionResult> ObterHistorico()
    {
        var dietasIA = await _context.DietasIA
            .Where(d => d.UsuarioId == UsuarioIdLogado)
            .OrderByDescending(d => d.DataGeracao)
            .ToListAsync();

        var response = dietasIA.Select(d => new DietaResponse
        {
            DataGeracao = d.DataGeracao,
            Dieta = JsonSerializer.Deserialize<DietaSemanalDto>(d.ConteudoJson)!
        });

        return Ok(response);
    }
}
