using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

/// <summary>
/// Gerencia o histórico de objetivos/peso do usuário logado. Todas as rotas exigem
/// autenticação, pois os dados aqui pertencem sempre a um usuário específico.
/// </summary>
[ApiController]
[Route("api/objetivos")]
[Authorize]
public class ObjetivosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ObjetivosController(ApplicationDbContext context)
    {
        _context = context;
    }

    // O Id do usuário logado vem sempre do claim do token JWT, nunca do corpo da
    // requisição: assim um usuário não consegue criar ou consultar objetivos de
    // outra pessoa informando um Id diferente do seu.
    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Registra um novo objetivo/peso para o usuário logado, na data atual.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar(ObjetivoRequest request)
    {
        var objetivo = new ObjetivoUsuario
        {
            Peso = request.Peso,
            Objetivo = request.Objetivo,
            DataRegistro = DateTime.UtcNow,
            UsuarioId = UsuarioIdLogado
        };

        _context.ObjetivosUsuario.Add(objetivo);
        await _context.SaveChangesAsync();

        var response = new ObjetivoResponse
        {
            Id = objetivo.Id,
            Peso = objetivo.Peso,
            Objetivo = objetivo.Objetivo,
            DataRegistro = objetivo.DataRegistro
        };

        return Ok(response);
    }

    /// <summary>
    /// Retorna o objetivo mais recente registrado pelo usuário logado.
    /// </summary>
    [HttpGet("atual")]
    public async Task<IActionResult> ObterAtual()
    {
        var objetivo = await _context.ObjetivosUsuario
            .Where(o => o.UsuarioId == UsuarioIdLogado)
            // Como cada atualização de peso/objetivo gera um novo registro, o "atual"
            // é sempre o mais recente por data de registro, não um único registro fixo.
            .OrderByDescending(o => o.DataRegistro)
            .FirstOrDefaultAsync();

        if (objetivo is null)
        {
            return NotFound(new { mensagem = "Nenhum objetivo cadastrado ainda." });
        }

        var response = new ObjetivoResponse
        {
            Id = objetivo.Id,
            Peso = objetivo.Peso,
            Objetivo = objetivo.Objetivo,
            DataRegistro = objetivo.DataRegistro
        };

        return Ok(response);
    }
}