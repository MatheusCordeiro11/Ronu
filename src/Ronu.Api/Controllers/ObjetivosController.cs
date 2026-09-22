using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
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
    private readonly ICalculadoraPesoTendencia _calculadoraPesoTendencia;

    public ObjetivosController(ApplicationDbContext context, ICalculadoraPesoTendencia calculadoraPesoTendencia)
    {
        _context = context;
        _calculadoraPesoTendencia = calculadoraPesoTendencia;
    }

    // O Id do usuário logado vem sempre do claim do token JWT, nunca do corpo da
    // requisição: assim um usuário não consegue criar ou consultar objetivos de
    // outra pessoa informando um Id diferente do seu.
    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Registra um novo objetivo/peso para o usuário logado. Se já existe um
    /// registro com a mesma data (no calendário UTC) para este usuário, atualiza
    /// esse registro em vez de criar um novo — evita duplicatas no mesmo dia, que
    /// não agregam nada à suavização de peso de tendência (que já ignora
    /// múltiplos registros no mesmo dia matematicamente) e só poluiriam o
    /// histórico visualmente. Refinamento consciente da regra original de "nunca
    /// fazer upsert": a granularidade do histórico passa a ser por dia, não por
    /// chamada de API — dias diferentes continuam sempre gerando registros
    /// distintos, preservando a evolução real ao longo do tempo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar(ObjetivoRequest request)
    {
        var hojeUtc = DateOnly.FromDateTime(DateTime.UtcNow);

        var objetivoDeHoje = await _context.ObjetivosUsuario
            .Where(o => o.UsuarioId == UsuarioIdLogado)
            .Where(o => DateOnly.FromDateTime(o.DataRegistro) == hojeUtc)
            .FirstOrDefaultAsync();

        ObjetivoUsuario objetivo;

        if (objetivoDeHoje is not null)
        {
            objetivoDeHoje.Peso = request.Peso;
            objetivoDeHoje.Objetivo = request.Objetivo;
            objetivo = objetivoDeHoje;
        }
        else
        {
            objetivo = new ObjetivoUsuario
            {
                Peso = request.Peso,
                Objetivo = request.Objetivo,
                DataRegistro = DateTime.UtcNow,
                UsuarioId = UsuarioIdLogado
            };

            _context.ObjetivosUsuario.Add(objetivo);
        }

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
    /// Retorna o histórico de peso do usuário logado com o peso de tendência
    /// (suavizado) calculado ao lado de cada registro bruto — usado pelo
    /// frontend para renderizar o gráfico de evolução de peso.
    /// </summary>
    [HttpGet("tendencia")]
    public async Task<IActionResult> ObterTendencia()
    {
        var registros = await _context.ObjetivosUsuario
            .Where(o => o.UsuarioId == UsuarioIdLogado)
            .Select(o => new RegistroPesoDto { Data = o.DataRegistro, Peso = o.Peso })
            .ToListAsync();

        var resultado = _calculadoraPesoTendencia.Calcular(registros);

        return Ok(resultado);
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
        var objetivo = await _context.ObjetivosUsuario
            .FirstOrDefaultAsync(o => o.Id == id && o.UsuarioId == UsuarioIdLogado);

        if (objetivo is null)
        {
            return NotFound(new { mensagem = "Objetivo não encontrado." });
        }

        var totalRegistros = await _context.ObjetivosUsuario
            .CountAsync(o => o.UsuarioId == UsuarioIdLogado);

        if (totalRegistros <= 1)
        {
            return BadRequest(new { mensagem = "Você precisa manter pelo menos um objetivo registrado." });
        }

        _context.ObjetivosUsuario.Remove(objetivo);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}