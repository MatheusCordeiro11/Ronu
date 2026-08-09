using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

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

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

    [HttpGet("atual")]
    public async Task<IActionResult> ObterAtual()
    {
        var objetivo = await _context.ObjetivosUsuario
            .Where(o => o.UsuarioId == UsuarioIdLogado)
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