using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

[ApiController]
[Route("api/preferencias-alimentares")]
[Authorize]
public class PreferenciasAlimentaresController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PreferenciasAlimentaresController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<IActionResult> Criar(PreferenciaAlimentarRequest request)
    {
        var preferencia = new PreferenciaAlimentar
        {
            Alimento = request.Alimento,
            Tipo = request.Tipo,
            UsuarioId = UsuarioIdLogado
        };

        _context.PreferenciasAlimentares.Add(preferencia);
        await _context.SaveChangesAsync();

        var response = new PreferenciaAlimentarResponse
        {
            Id = preferencia.Id,
            Alimento = preferencia.Alimento,
            Tipo = preferencia.Tipo
        };

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var preferencias = await _context.PreferenciasAlimentares
            .Where(p => p.UsuarioId == UsuarioIdLogado)
            .Select(p => new PreferenciaAlimentarResponse
            {
                Id = p.Id,
                Alimento = p.Alimento,
                Tipo = p.Tipo
            })
            .ToListAsync();

        return Ok(preferencias);
    }
}