using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.ListaCompras;
using System.Security.Claims;
using System.Text.Json;

namespace Ronu.Api.Controllers;

/// <summary>
/// Lista de compras da dieta atual do usuário logado, calculada na hora a partir da dieta salva, e as
/// marcações "já tenho". A dieta é sempre buscada pelo usuário do token, então ninguém chega à lista
/// (nem às marcações) de outra pessoa.
/// </summary>
[ApiController]
[Route("api/dietas/atual/lista-compras")]
[Authorize]
public partial class ListaComprasController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ListaComprasController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // O formato de ItemCompra.Chave: a chave vem da URL e vai para o banco.
    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex FormatoChave();

    // A mesma regra do GET /api/dietas/atual: a mais recente do usuário.
    private Task<DietaIA?> DietaAtual() => _context.DietasIA
        .Where(d => d.UsuarioId == UsuarioIdLogado)
        .OrderByDescending(d => d.DataGeracao)
        .FirstOrDefaultAsync();

    private async Task<ListaComprasMontada> Montar(DietaIA dietaIA)
    {
        // A UF só escolhe o nome regional (Aipim, Macaxeira).
        var uf = await _context.Usuarios
            .Where(u => u.Id == UsuarioIdLogado)
            .Select(u => u.Estado)
            .FirstOrDefaultAsync();

        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(dietaIA.ConteudoJson)!;
        return MontadorListaCompras.Montar(dieta, uf);
    }

    [HttpGet]
    public async Task<IActionResult> Obter()
    {
        var dietaIA = await DietaAtual();
        if (dietaIA is null)
        {
            return NotFound(new { mensagem = "Nenhuma dieta gerada ainda." });
        }

        var lista = await Montar(dietaIA);

        var marcadas = await _context.ListaComprasMarcacoes
            .Where(m => m.DietaIAId == dietaIA.Id && m.UsuarioId == UsuarioIdLogado)
            .Select(m => m.Chave)
            .ToListAsync();
        foreach (var item in lista.Secoes.SelectMany(s => s.Itens))
        {
            item.Tenho = marcadas.Contains(item.Chave);
        }

        return Ok(new ListaComprasResponse
        {
            DietaId = dietaIA.Id,
            DataGeracao = dietaIA.DataGeracao,
            VersaoConversao = ConversaoCompras.VersaoConversao,
            Disponivel = lista.Disponivel,
            Motivo = lista.Motivo,
            AvisoOleo = lista.AvisoOleo,
            Secoes = lista.Secoes
        });
    }

    /// <summary>
    /// Marca (tenho = true) ou desmarca um item como "já tenho" na dieta atual. Idempotente: marcar o
    /// que já está marcado, ou desmarcar o que não está, devolve 204 do mesmo jeito.
    /// </summary>
    [HttpPut("itens/{chave}")]
    public async Task<IActionResult> Marcar(string chave, [FromBody] MarcacaoListaComprasRequest request)
    {
        if (!FormatoChave().IsMatch(chave))
        {
            return BadRequest(new { mensagem = "Item inválido." });
        }

        var dietaIA = await DietaAtual();
        if (dietaIA is null)
        {
            return NotFound(new { mensagem = "Nenhuma dieta gerada ainda." });
        }

        // O dietaId do corpo só é comparado com a dieta atual do usuário do token: um id de dieta de
        // outra pessoa nunca é a dieta atual de quem chama, então cai aqui também.
        if (request.DietaId != dietaIA.Id)
        {
            return Conflict(new { mensagem = "Sua dieta mudou desde que a lista foi aberta. Atualize a lista e tente de novo." });
        }

        var lista = await Montar(dietaIA);
        if (!lista.Secoes.SelectMany(s => s.Itens).Any(i => i.Chave == chave))
        {
            return BadRequest(new { mensagem = "Este item não está na sua lista de compras." });
        }

        var existente = await _context.ListaComprasMarcacoes
            .FirstOrDefaultAsync(m => m.DietaIAId == dietaIA.Id && m.Chave == chave);

        if (request.Tenho == true && existente is null)
        {
            _context.ListaComprasMarcacoes.Add(new ListaComprasMarcacao
            {
                UsuarioId = UsuarioIdLogado,
                DietaIAId = dietaIA.Id,
                Chave = chave,
                MarcadoEm = DateTime.UtcNow
            });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EhChaveDuplicada(ex))
            {
                // Dois toques ao mesmo tempo (PC e celular): o índice único (DietaIAId, Chave) barra o
                // segundo, e o item já está marcado, que é o que se pediu. Qualquer outro erro do banco
                // não passa no filtro e continua subindo para o GlobalExceptionHandler.
            }
        }
        else if (request.Tenho == false && existente is not null)
        {
            _context.ListaComprasMarcacoes.Remove(existente);
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>Só a violação de chave única do PostgreSQL (SQLSTATE 23505).</summary>
    public static bool EhChaveDuplicada(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
