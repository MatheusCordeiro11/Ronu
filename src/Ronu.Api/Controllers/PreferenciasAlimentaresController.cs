using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;
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

    // Os únicos tipos que o GeradorDietaGemini reconhece ao montar o prompt.
    // Sem normalização (como a aderência): vêm de opções fixas na tela, e
    // qualquer outra grafia ("Evitar") ficaria salva mas seria ignorada na dieta.
    private static readonly HashSet<string> TiposPreferencia = new()
    {
        "preferido", "evitar"
    };

    /// <summary>
    /// Registra uma preferência alimentar para o usuário logado. Se o usuário já
    /// tem uma preferência para o mesmo alimento (comparação sem diferenciar
    /// maiúsculas/minúsculas), atualiza o tipo em vez de duplicar (upsert) — o
    /// onboarding pode reenviar o mesmo alimento se o usuário voltar uma etapa.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar(PreferenciaAlimentarRequest request)
    {
        if (!TiposPreferencia.Contains(request.Tipo))
        {
            return BadRequest(new { mensagem = "Tipo de preferência inválido." });
        }

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

    /// <summary>
    /// Confere as preferências do usuário logado contra a base nutricional (fase
    /// 2b-3): o que cada uma casa, o que não casou, como foi lida pela tolerância
    /// a 1 letra e se a lista que sobra atende aos mínimos por papel no prato.
    /// Só as preferências de quem está logado (o id vem do token), como as outras
    /// rotas daqui.
    /// </summary>
    [HttpGet("conferencia")]
    public async Task<IActionResult> Conferir()
    {
        var estado = await _context.Usuarios
            .Where(u => u.Id == UsuarioIdLogado)
            .Select(u => u.Estado)
            .FirstOrDefaultAsync();

        var salvas = await _context.PreferenciasAlimentares
            .Where(p => p.UsuarioId == UsuarioIdLogado)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var lista = ListaAlimentosPessoa.Montar(estado, salvas.Select(p => new PreferenciaContextoDto { Alimento = p.Alimento, Tipo = p.Tipo }));
        var base_ = BaseNutricional.Itens.ToDictionary(i => i.Id);

        // ListaAlimentosPessoa devolve uma conferência por preferência, na mesma ordem em que recebeu.
        var preferencias = salvas.Zip(lista.Preferencias, (salva, conferida) => new PreferenciaConferidaResponse
        {
            Id = salva.Id,
            Alimento = salva.Alimento,
            Tipo = salva.Tipo,
            Casou = conferida.Casou,
            Alimentos = BaseNutricional.Itens
                .Where(i => conferida.ItensIds.Contains(i.Id))
                .Select(i => new AlimentoCasadoResponse { Nome = i.Nome, Grupo = i.Grupo.ToString() })
                .ToList(),
            SemCasamento = conferida.TrechosSemCasamento.ToList(),
            LidoComo = conferida.LidoComo.ToList()
        }).ToList();

        var minimos = lista.Minimos;
        return Ok(new ConferenciaPreferenciasResponse
        {
            Preferencias = preferencias,
            Lista = new MinimosListaResponse
            {
                Atende = minimos.Atende,
                Vazios = minimos.Vazios.Select(p => p.ToString()).ToList(),
                AbaixoDoMinimo = minimos.AbaixoDoMinimo.Select(p => p.ToString()).ToList(),
                ItensPorPapel = minimos.ItensPorPapel.ToDictionary(p => p.Key.ToString(), p => p.Value),
                GruposDeProteina = minimos.GruposDeProteina
            }
        });
    }

    /// <summary>
    /// Remove uma preferência alimentar do usuário logado. Busca sempre filtrando
    /// também por UsuarioIdLogado (não só pelo Id) para impedir que um usuário
    /// apague a preferência de outro só adivinhando um Id — se o registro existir
    /// mas pertencer a outro usuário, o resultado é o mesmo de não existir.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Remover(int id)
    {
        var preferencia = await _context.PreferenciasAlimentares
            .FirstOrDefaultAsync(p => p.Id == id && p.UsuarioId == UsuarioIdLogado);

        if (preferencia is null)
        {
            return NotFound(new { mensagem = "Preferência não encontrada." });
        }

        _context.PreferenciasAlimentares.Remove(preferencia);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}