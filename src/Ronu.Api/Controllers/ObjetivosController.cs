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

    // Valores fixos da resposta de aderência do registro diário — sem
    // normalização, porque vêm de opções fixas na tela, não de digitação livre.
    // "nao_seguiu" é guardado, mas o dia é descartado do cálculo da meta adaptativa.
    private static readonly HashSet<string> ValoresAderencia = new()
    {
        "seguiu", "comeu_mais", "comeu_menos", "nao_seguiu"
    };

    /// <summary>
    /// Registra um novo objetivo/peso para o usuário logado. Se já existe um
    /// registro de hoje (no fuso do estado do usuário) para ele, atualiza
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
        // Opcional: nulo ou vazio segue normalmente; qualquer outro valor
        // precisa ser uma das opções conhecidas.
        if (!string.IsNullOrEmpty(request.Aderencia) && !ValoresAderencia.Contains(request.Aderencia))
        {
            return BadRequest(new { mensagem = "Resposta de aderência inválida." });
        }

        // "" chega como "sem resposta": grava null, não uma string vazia.
        var aderencia = string.IsNullOrEmpty(request.Aderencia) ? null : request.Aderencia;

        var objetivoDeHoje = await BuscarObjetivoDeHojeAsync();

        ObjetivoUsuario objetivo;

        if (objetivoDeHoje is not null)
        {
            objetivoDeHoje.Peso = request.Peso;
            objetivoDeHoje.Objetivo = request.Objetivo;
            objetivoDeHoje.Aderencia = aderencia;
            objetivo = objetivoDeHoje;
        }
        else
        {
            objetivo = new ObjetivoUsuario
            {
                Peso = request.Peso,
                Objetivo = request.Objetivo,
                Aderencia = aderencia,
                DataRegistro = DateTime.UtcNow,
                UsuarioId = UsuarioIdLogado
            };

            _context.ObjetivosUsuario.Add(objetivo);
        }

        await _context.SaveChangesAsync();

        // Mesmo formato do GET /atual: o progresso.js atualiza o estado do
        // formulário com esta resposta.
        return Ok(await MontarObjetivoAtualResponseAsync(objetivo));
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

        return Ok(await MontarObjetivoAtualResponseAsync(objetivo));
    }

    // Regra única de "hoje" (dia do calendário no fuso do estado do usuário —
    // ver FusoHorarioEstado; sem estado, Brasília): usada no upsert do POST e
    // no RegistradoHoje da resposta, para os dois nunca divergirem. Se houver
    // mais de um registro no mesmo dia local (gravados quando o dia era o UTC,
    // ex.: um às 20h e outro às 22h de Brasília), vale o mais recente, o mesmo
    // que o GET /atual devolve.
    private async Task<ObjetivoUsuario?> BuscarObjetivoDeHojeAsync()
    {
        var estado = await _context.Usuarios
            .Where(u => u.Id == UsuarioIdLogado)
            .Select(u => u.Estado)
            .FirstAsync();

        var (inicioUtc, fimUtc) = FusoHorarioEstado.IntervaloUtcDeHoje(estado, DateTime.UtcNow);

        return await _context.ObjetivosUsuario
            .Where(o => o.UsuarioId == UsuarioIdLogado)
            .Where(o => o.DataRegistro >= inicioUtc && o.DataRegistro < fimUtc)
            .OrderByDescending(o => o.DataRegistro)
            .FirstOrDefaultAsync();
    }

    // Resposta comum ao POST e ao GET /atual. RegistradoHoje compara pelo Id
    // (não só "existe registro de hoje"): o que importa é se este é o registro
    // que o próximo POST vai sobrescrever.
    private async Task<ObjetivoAtualResponse> MontarObjetivoAtualResponseAsync(ObjetivoUsuario objetivo)
    {
        var objetivoDeHoje = await BuscarObjetivoDeHojeAsync();
        var temDieta = await _context.DietasIA.AnyAsync(d => d.UsuarioId == UsuarioIdLogado);

        return new ObjetivoAtualResponse
        {
            Id = objetivo.Id,
            Peso = objetivo.Peso,
            Objetivo = objetivo.Objetivo,
            DataRegistro = objetivo.DataRegistro,
            Aderencia = objetivo.Aderencia,
            RegistradoHoje = objetivoDeHoje?.Id == objetivo.Id,
            TemDieta = temDieta
        };
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