using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

/// <summary>
/// Gerencia as preferências alimentares do usuário logado. Todas as rotas exigem
/// autenticação, pois os dados aqui pertencem sempre a um usuário específico.
/// </summary>
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

    // O Id do usuário logado vem sempre do claim do token JWT, nunca do corpo da
    // requisição: assim um usuário não consegue criar ou consultar preferências
    // de outra pessoa informando um Id diferente do seu.
    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Registra uma preferência alimentar para o usuário logado. Se o usuário já
    /// tem uma preferência para o mesmo alimento (comparação sem diferenciar
    /// maiúsculas/minúsculas), atualiza o tipo em vez de duplicar (upsert) — o
    /// onboarding pode reenviar o mesmo alimento se o usuário voltar uma etapa.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar(PreferenciaAlimentarRequest request)
    {
        var alimento = request.Alimento.Trim();

        var preferenciaExistente = await _context.PreferenciasAlimentares
            .FirstOrDefaultAsync(p => p.UsuarioId == UsuarioIdLogado && p.Alimento.ToLower() == alimento.ToLower());

        PreferenciaAlimentar preferencia;

        if (preferenciaExistente is not null)
        {
            preferenciaExistente.Tipo = request.Tipo;
            preferencia = preferenciaExistente;
        }
        else
        {
            preferencia = new PreferenciaAlimentar
            {
                Alimento = request.Alimento,
                Tipo = request.Tipo,
                UsuarioId = UsuarioIdLogado
            };

            _context.PreferenciasAlimentares.Add(preferencia);
        }

        await _context.SaveChangesAsync();

        var response = new PreferenciaAlimentarResponse
        {
            Id = preferencia.Id,
            Alimento = preferencia.Alimento,
            Tipo = preferencia.Tipo
        };

        return Ok(response);
    }

    /// <summary>
    /// Lista todas as preferências alimentares cadastradas pelo usuário logado.
    /// </summary>
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