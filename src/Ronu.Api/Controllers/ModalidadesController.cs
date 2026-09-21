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

    // O Id do usuário logado vem sempre do claim do token JWT, nunca do corpo da
    // requisição: se viesse do corpo, um usuário mal-intencionado poderia informar
    // o Id de outra pessoa e manipular dados que não são dele.
    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Sem [Authorize]: o catálogo de modalidades é informação do sistema (não é
    // dado de nenhum usuário específico), então pode ser consultado por qualquer
    // cliente, autenticado ou não.
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
    /// Vincula uma modalidade existente do catálogo ao usuário logado, com a
    /// frequência semanal informada. Se o usuário já pratica essa modalidade,
    /// atualiza a frequência em vez de criar um vínculo duplicado (upsert) — o
    /// onboarding pode reenviar a mesma modalidade se o usuário voltar uma etapa.
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

        var usuarioModalidadeExistente = await _context.UsuarioModalidades
            .FirstOrDefaultAsync(um => um.UsuarioId == UsuarioIdLogado && um.ModalidadeId == request.ModalidadeId);

        UsuarioModalidade usuarioModalidade;

        if (usuarioModalidadeExistente is not null)
        {
            usuarioModalidadeExistente.FrequenciaSemanal = request.FrequenciaSemanal;
            usuarioModalidadeExistente.DuracaoMediaHoras = request.DuracaoMediaHoras;
            usuarioModalidade = usuarioModalidadeExistente;
        }
        else
        {
            usuarioModalidade = new UsuarioModalidade
            {
                UsuarioId = UsuarioIdLogado,
                ModalidadeId = request.ModalidadeId,
                FrequenciaSemanal = request.FrequenciaSemanal,
                DuracaoMediaHoras = request.DuracaoMediaHoras
            };

            _context.UsuarioModalidades.Add(usuarioModalidade);
        }

        await _context.SaveChangesAsync();

        return Ok(new { usuarioModalidade.Id, usuarioModalidade.ModalidadeId, usuarioModalidade.FrequenciaSemanal, usuarioModalidade.DuracaoMediaHoras });
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
            // .Include() é necessário para carregar o objeto Modalidade completo:
            // por padrão o EF Core só traz o ModalidadeId, não a entidade relacionada.
            .Include(um => um.Modalidade)
            .Select(um => new UsuarioModalidadeResponse
            {
                Id = um.Id,
                FrequenciaSemanal = um.FrequenciaSemanal,
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
    /// filtrando também por UsuarioIdLogado (não só pelo Id do registro em
    /// UsuarioModalidade) para impedir que um usuário remova a modalidade de
    /// outro só adivinhando um Id — se o registro existir mas pertencer a
    /// outro usuário, o resultado é o mesmo de não existir.
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