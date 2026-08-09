using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

[ApiController]
public class ModalidadesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ModalidadesController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("api/modalidades")]
    public async Task<IActionResult> ListarTodas()
    {
        var modalidades = await _context.Modalidades
            .Select(m => new ModalidadeResponse
            {
                Id = m.Id,
                Nome = m.Nome,
                MetReferencia = m.MetReferencia
            })
            .ToListAsync();

        return Ok(modalidades);
    }

    [Authorize]
    [HttpPost("api/usuarios/modalidades")]
    public async Task<IActionResult> AdicionarModalidade(UsuarioModalidadeRequest request)
    {
        bool modalidadeExiste = await _context.Modalidades.AnyAsync(m => m.Id == request.ModalidadeId);
        if (!modalidadeExiste)
        {
            return NotFound(new { mensagem = "Modalidade não encontrada." });
        }

        var usuarioModalidade = new UsuarioModalidade
        {
            UsuarioId = UsuarioIdLogado,
            ModalidadeId = request.ModalidadeId,
            FrequenciaSemanal = request.FrequenciaSemanal
        };

        _context.UsuarioModalidades.Add(usuarioModalidade);
        await _context.SaveChangesAsync();

        return Ok(new { usuarioModalidade.Id, usuarioModalidade.ModalidadeId, usuarioModalidade.FrequenciaSemanal });
    }

    [Authorize]
    [HttpGet("api/usuarios/modalidades")]
    public async Task<IActionResult> ListarMinhasModalidades()
    {
        var modalidades = await _context.UsuarioModalidades
            .Where(um => um.UsuarioId == UsuarioIdLogado)
            .Include(um => um.Modalidade)
            .Select(um => new UsuarioModalidadeResponse
            {
                Id = um.Id,
                FrequenciaSemanal = um.FrequenciaSemanal,
                Modalidade = new ModalidadeResponse
                {
                    Id = um.Modalidade.Id,
                    Nome = um.Modalidade.Nome,
                    MetReferencia = um.Modalidade.MetReferencia
                }
            })
            .ToListAsync();

        return Ok(modalidades);
    }
}