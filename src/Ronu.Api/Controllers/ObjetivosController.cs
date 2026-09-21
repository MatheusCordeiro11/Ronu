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

    /// <summary>
    /// Remove um registro específico de objetivo/peso do usuário logado.
    /// Bloqueia a remoção se for o único registro restante — sem isso, o
    /// usuário ficaria sem nenhum objetivo cadastrado, reabrindo o mesmo loop
    /// de onboarding indevido que já corrigimos para Altura/Sexo/DataNascimento.
    /// Busca sempre filtrando também por UsuarioIdLogado, para impedir que um
    /// usuário remova o registro de outro só adivinhando um Id.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Remover(int id)
    {
        var totalRegistros = await _context.ObjetivosUsuario
            .CountAsync(o => o.UsuarioId == UsuarioIdLogado);

        if (totalRegistros <= 1)
        {
            return BadRequest(new { mensagem = "Você precisa manter pelo menos um objetivo registrado." });
        }

        var objetivo = await _context.ObjetivosUsuario
            .FirstOrDefaultAsync(o => o.Id == id && o.UsuarioId == UsuarioIdLogado);

        if (objetivo is null)
        {
            return NotFound(new { mensagem = "Objetivo não encontrado." });
        }

        _context.ObjetivosUsuario.Remove(objetivo);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}