using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.ListaCompras;
using System.Security.Claims;
using System.Text.Json;

namespace Ronu.Api.Controllers;

/// <summary>
/// Lista de compras da dieta atual do usuário logado. Calculada na hora a partir da dieta salva: nada
/// é gravado. A dieta é sempre buscada pelo usuário do token, então ninguém chega à lista de outra pessoa.
/// </summary>
[ApiController]
[Route("api/dietas/atual/lista-compras")]
[Authorize]
public class ListaComprasController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ListaComprasController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Obter()
    {
        // A mesma regra do GET /api/dietas/atual: a mais recente do usuário.
        var dietaIA = await _context.DietasIA
            .Where(d => d.UsuarioId == UsuarioIdLogado)
            .OrderByDescending(d => d.DataGeracao)
            .FirstOrDefaultAsync();

        if (dietaIA is null)
        {
            return NotFound(new { mensagem = "Nenhuma dieta gerada ainda." });
        }

        // A UF só escolhe o nome regional (Aipim, Macaxeira).
        var uf = await _context.Usuarios
            .Where(u => u.Id == UsuarioIdLogado)
            .Select(u => u.Estado)
            .FirstOrDefaultAsync();

        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(dietaIA.ConteudoJson)!;
        var lista = MontadorListaCompras.Montar(dieta, uf);

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
}
