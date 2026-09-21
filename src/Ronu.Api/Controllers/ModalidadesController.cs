using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

/// <summary>
/// Expõe o catálogo de modalidades e a relação entre um usuário e as modalidades
/// que ele pratica. A listagem do catálogo é pública (qualquer um pode ver as
/// modalidades disponíveis), mas vincular/consultar modalidades de um usuário
/// específico exige autenticação.
/// </summary>
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

    /// <summary>
    /// Vincula uma modalidade existente do catálogo ao usuário logado, com os
    /// dias da semana e a duração média informados. Se o usuário já pratica
    /// essa modalidade, atualiza os dados em vez de criar um vínculo duplicado
    /// (upsert) — o onboarding pode reenviar a mesma modalidade se o usuário
    /// voltar uma etapa.
    /// </summary>
    [Authorize]
    [HttpPost("api/usuarios/modalidades")]
    public async Task<IActionResult> AdicionarModalidade(UsuarioModalidadeRequest request)
    {
        bool modalidadeExiste = await _context.Modalidades.AnyAsync(m => m.Id == request.ModalidadeId);
        if (!modalidadeExiste)
        {
            return NotFound(new { mensagem = "Modalidade não encontrada." });
        }

        if (request.DiasSemana.Any(d => d < 1 || d > 7))
        {
            return BadRequest(new { mensagem = "Os dias da semana devem estar entre 1 (Segunda) e 7 (Domingo)." });
        }

        if (request.DiasSemana.Distinct().Count() != request.DiasSemana.Length)
        {
            return BadRequest(new { mensagem = "Não é possível repetir o mesmo dia da semana." });
        }

        var usuarioModalidadeExistente = await _context.UsuarioModalidades
            .FirstOrDefaultAsync(um => um.UsuarioId == UsuarioIdLogado && um.ModalidadeId == request.ModalidadeId);

        UsuarioModalidade usuarioModalidade;

        if (usuarioModalidadeExistente is not null)
        {
            usuarioModalidadeExistente.DiasSemana = request.DiasSemana;
            usuarioModalidadeExistente.DuracaoMediaHoras = request.DuracaoMediaHoras;
            usuarioModalidade = usuarioModalidadeExistente;
        }
        else
        {
            usuarioModalidade = new UsuarioModalidade
            {
                UsuarioId = UsuarioIdLogado,
                ModalidadeId = request.ModalidadeId,
                DiasSemana = request.DiasSemana,
                DuracaoMediaHoras = request.DuracaoMediaHoras
            };

            _context.UsuarioModalidades.Add(usuarioModalidade);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            usuarioModalidade.Id,
            usuarioModalidade.ModalidadeId,
            usuarioModalidade.DiasSemana,
            usuarioModalidade.DuracaoMediaHoras
        });
    }

    /// <summary>
    /// Lista as modalidades vinculadas ao usuário logado, já com os dados de
    /// cada modalidade (nome, MET) embutidos na resposta.
    /// </summary>
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
                DiasSemana = um.DiasSemana,
                DuracaoMediaHoras = um.DuracaoMediaHoras,
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

    /// <summary>
    /// Remove uma modalidade praticada pelo usuário logado. Busca sempre
    /// filtrando também por UsuarioIdLogado para impedir que um usuário
    /// remova a modalidade de outro só adivinhando um Id.
    /// </summary>
    [Authorize]
    [HttpDelete("api/usuarios/modalidades/{id}")]
    public async Task<IActionResult> Remover(int id)
    {
        var usuarioModalidade = await _context.UsuarioModalidades
            .FirstOrDefaultAsync(m => m.Id == id && m.UsuarioId == UsuarioIdLogado);

        if (usuarioModalidade is null)
        {
            return NotFound(new { mensagem = "Modalidade não encontrada." });
        }

        _context.UsuarioModalidades.Remove(usuarioModalidade);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
