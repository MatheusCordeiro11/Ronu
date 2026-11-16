using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;

namespace Ronu.Api.Controllers;

/// <summary>
/// "Comi esta refeição do plano": marcar e desmarcar refeições da dieta atual como feitas, em hoje e ontem. A dieta é
/// sempre buscada pelo usuário do token; o dietaId que vem do cliente só é comparado com ela, então ninguém chega à
/// dieta (nem às marcações) de outra pessoa. Vale também para as dietas da casa (Origem = casa): a marcação é pessoal.
///
/// Separado do GET /api/dietas/atual de propósito: a resposta dele é reaproveitada nas trocas, e a marcação não entra
/// nesse contrato. Marcar não sobe a DietaIA.Revisao: uma folha de troca aberta em outra aba não leva 409 por causa
/// de um toque. Sem [DispensaConsentimento]: as duas rotas exigem o consentimento geral (403 do FiltroConsentimento).
/// </summary>
[ApiController]
[Route("api/dietas/atual/refeicoes-feitas")]
[Authorize]
public class RefeicoesFeitasController : ControllerBase
{
    private const string MensagemSemDieta = "Nenhuma dieta gerada ainda.";
    private const string MensagemMudou = "Sua dieta mudou desde que a tela foi aberta. Atualize e tente de novo.";

    private readonly ApplicationDbContext _context;

    public RefeicoesFeitasController(ApplicationDbContext context)
    {
        _context = context;
    }

    // O relógio, trocável nos testes (o "hoje" depende do fuso da UF).
    public Func<DateTime> Relogio { get; set; } = () => DateTime.UtcNow;

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // A mesma regra do GET /api/dietas/atual: a mais recente do usuário. Só leitura: a marcação não mexe na dieta.
    private Task<DietaIA?> DietaAtual() => _context.DietasIA.AsNoTracking()
        .Where(d => d.UsuarioId == UsuarioIdLogado)
        .OrderByDescending(d => d.DataGeracao)
        .FirstOrDefaultAsync();

    private Task<string?> Uf() => _context.Usuarios
        .Where(u => u.Id == UsuarioIdLogado)
        .Select(u => u.Estado)
        .FirstOrDefaultAsync();

    [HttpGet]
    [EnableRateLimiting(LimitesRefeicoesFeitas.PoliticaLeitura)]
    public async Task<IActionResult> Obter()
    {
        var dietaIA = await DietaAtual();
        if (dietaIA is null)
        {
            return NotFound(new { mensagem = MensagemSemDieta });
        }

        var agora = Relogio();
        var uf = await Uf();
        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(dietaIA.ConteudoJson)!;
        var datas = RegrasRefeicoesFeitas.DatasMarcaveis(dietaIA.DataGeracao, uf, agora).ToList();

        var marcacoes = await _context.RefeicoesFeitas.AsNoTracking()
            .Where(r => r.UsuarioId == UsuarioIdLogado && r.DietaIAId == dietaIA.Id && datas.Contains(r.Data))
            .Select(r => new { r.Data, r.RefeicaoIndice })
            .ToListAsync();

        return Ok(new RefeicoesFeitasResponse
        {
            DietaId = dietaIA.Id,
            Hoje = FusoHorarioEstado.Hoje(uf, agora),
            Dias = datas.Select(data => new DiaRefeicoesFeitasDto
            {
                Data = data,
                IndiceDia = RegrasRefeicoesFeitas.IndiceDoDia(dieta, data),
                Marcadas = marcacoes.Where(m => m.Data == data).Select(m => m.RefeicaoIndice).Order().ToList()
            }).ToList()
        });
    }

    /// <summary>
    /// Marca (feita = true) ou desmarca uma refeição de hoje ou de ontem. Idempotente: marcar o que já está marcado, ou
    /// desmarcar o que não está, devolve 204 do mesmo jeito. Desmarcar apaga a linha.
    /// </summary>
    [HttpPut("{data}/{refeicao}")]
    [EnableRateLimiting(LimitesRefeicoesFeitas.PoliticaEscrita)]
    public async Task<IActionResult> Marcar(string data, int refeicao, [FromBody] MarcacaoRefeicaoRequest request)
    {
        if (!DateOnly.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
        {
            return BadRequest(new { mensagem = "Data inválida. Use o formato AAAA-MM-DD." });
        }

        var dietaIA = await DietaAtual();
        if (dietaIA is null)
        {
            return NotFound(new { mensagem = MensagemSemDieta });
        }

        // Um id de dieta de outra pessoa nunca é a dieta atual de quem chama, então cai aqui também.
        if (request.DietaId != dietaIA.Id)
        {
            return Conflict(new { mensagem = MensagemMudou });
        }

        var uf = await Uf();
        if (!RegrasRefeicoesFeitas.DatasMarcaveis(dietaIA.DataGeracao, uf, Relogio()).Contains(dia))
        {
            return UnprocessableEntity(new { mensagem = "Só dá para marcar as refeições de hoje e de ontem da dieta atual." });
        }

        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(dietaIA.ConteudoJson)!;
        var indiceDia = RegrasRefeicoesFeitas.IndiceDoDia(dieta, dia);
        if (indiceDia is null || refeicao < 0 || refeicao >= dieta.Dias[indiceDia.Value].Refeicoes.Count)
        {
            return BadRequest(new { mensagem = "Refeição não encontrada na dieta." });
        }

        var existente = await _context.RefeicoesFeitas.FirstOrDefaultAsync(r =>
            r.UsuarioId == UsuarioIdLogado && r.Data == dia && r.DietaIAId == dietaIA.Id && r.RefeicaoIndice == refeicao);

        if (request.Feita == true && existente is null)
        {
            var refeicoesDoDia = dieta.Dias[indiceDia.Value].Refeicoes;
            _context.RefeicoesFeitas.Add(new RefeicaoFeita
            {
                UsuarioId = UsuarioIdLogado,
                DietaIAId = dietaIA.Id,
                Data = dia,
                RefeicaoIndice = refeicao,
                // Fotos do momento do toque, lidas da dieta: a troca de alimento muda as kcal depois.
                Kcal = Math.Round(refeicoesDoDia[refeicao].Macros.Calorias, 1, MidpointRounding.AwayFromZero),
                RefeicoesNoDia = refeicoesDoDia.Count,
                MarcadaEm = DateTime.UtcNow
            });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ListaComprasController.EhChaveDuplicada(ex))
            {
                // Dois toques ao mesmo tempo (PC e celular): o índice único barra o segundo, e a refeição já está
                // marcada, que é o que se pediu. Qualquer outro erro do banco continua subindo.
            }
        }
        else if (request.Feita == false && existente is not null)
        {
            _context.RefeicoesFeitas.Remove(existente);
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }
}
