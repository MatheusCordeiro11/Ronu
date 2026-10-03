using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;
using System.Security.Claims;
using System.Text.Json;

namespace Ronu.Api.Controllers;

/// <summary>
/// Gera e consulta dietas semanais via IA para o usuário logado. Todas as
/// rotas exigem autenticação, pois os dados pertencem sempre a um usuário
/// específico.
/// </summary>
[ApiController]
[Route("api/dietas")]
[Authorize]
public class DietasController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IContextoDietaBuilder _contextoBuilder;
    private readonly IGeradorDietaIA _geradorDieta;
    private readonly IRepositorioDietaIA _repositorioDieta;
    private readonly ILogger<DietasController> _logger;

    public DietasController(
        ApplicationDbContext context,
        IContextoDietaBuilder contextoBuilder,
        IGeradorDietaIA geradorDieta,
        IRepositorioDietaIA repositorioDieta,
        ILogger<DietasController> logger)
    {
        _context = context;
        _contextoBuilder = contextoBuilder;
        _geradorDieta = geradorDieta;
        _repositorioDieta = repositorioDieta;
        _logger = logger;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Limite de gerações por usuário numa janela móvel de 1 hora. Contado na
    // própria tabela DietasIA (nada em memória: o App Service no plano F1
    // reinicia com frequência e perderia o contador). Não pode passar de
    // RepositorioDietaIA.LimiteDietasPorUsuario (3): a tabela guarda só as 3
    // dietas mais recentes, então uma contagem maior nunca seria atingida.
    private const int LimiteGeracoesPorHora = 3;
    private static readonly TimeSpan JanelaLimiteGeracoes = TimeSpan.FromHours(1);

    /// <summary>
    /// Gera uma nova dieta semanal via IA para o usuário logado, com base no
    /// objetivo, modalidades e preferências já cadastrados. Mantém só as 3
    /// dietas mais recentes por usuário. Limitado a 3 gerações por hora (429).
    /// </summary>
    [HttpPost("gerar")]
    public async Task<IActionResult> Gerar()
    {
        // Primeira checagem: a mais barata, e evita gastar cota do Gemini.
        var agora = DateTime.UtcNow;
        var inicioJanela = agora - JanelaLimiteGeracoes;
        var geracoesNaJanela = await _context.DietasIA
            .Where(d => d.UsuarioId == UsuarioIdLogado && d.DataGeracao >= inicioJanela)
            .Select(d => d.DataGeracao)
            .ToListAsync();

        if (geracoesNaJanela.Count >= LimiteGeracoesPorHora)
        {
            // A vaga volta quando a geração mais antiga da janela completa 1 hora.
            var libera = geracoesNaJanela.Min() + JanelaLimiteGeracoes;
            var minutos = Math.Max(1, (int)Math.Ceiling((libera - agora).TotalMinutes));
            var quando = minutos == 1 ? "1 minuto" : $"{minutos} minutos";
            return StatusCode(429, new
            {
                mensagem = $"Você já gerou {LimiteGeracoesPorHora} dietas na última hora, o limite por hora. Tente novamente em {quando}."
            });
        }

        var usuario = await _context.Usuarios
            .FirstAsync(u => u.Id == UsuarioIdLogado);

        // Checagem feita aqui (não dentro de ContextoDietaBuilder) porque é
        // uma regra de negócio da requisição, não um detalhe de como buscar
        // dados no banco — e permite devolver um erro HTTP claro, em vez de
        // uma exceção genérica de null reference mais adiante.
        if (usuario.Altura is null || usuario.Sexo is null || usuario.DataNascimento is null)
        {
            return BadRequest(new { mensagem = "Complete seu perfil (altura, sexo e data de nascimento) antes de gerar uma dieta." });
        }

        var temObjetivo = await _context.ObjetivosUsuario
            .AnyAsync(o => o.UsuarioId == UsuarioIdLogado);

        if (!temObjetivo)
        {
            return BadRequest(new { mensagem = "Registre um objetivo antes de gerar uma dieta." });
        }

        var contexto = await _contextoBuilder.ConstruirAsync(UsuarioIdLogado);
        // Falhas de comunicação com o Gemini (rede, status de erro como 503,
        // timeout) e respostas inutilizáveis mesmo depois da nova tentativa do
        // gerador (bloqueada, cortada, JSON inválido, dias errados) viram um
        // 503 com mensagem amigável. Qualquer outra exceção continua subindo
        // para o GlobalExceptionHandler.
        DietaSemanalDto dieta;
        try
        {
            dieta = await _geradorDieta.GerarDietaAsync(contexto);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Falha ao chamar o Gemini para gerar dieta do usuário {UsuarioId}", UsuarioIdLogado);
            return StatusCode(503, new { mensagem = "Não foi possível gerar sua dieta agora. Tente novamente em alguns instantes." });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Tempo esgotado ao chamar o Gemini para gerar dieta do usuário {UsuarioId}", UsuarioIdLogado);
            return StatusCode(503, new { mensagem = "Não foi possível gerar sua dieta agora. Tente novamente em alguns instantes." });
        }
        catch (RespostaIaInvalidaException ex)
        {
            _logger.LogWarning(ex, "Resposta inválida do Gemini (após nova tentativa) ao gerar dieta do usuário {UsuarioId}", UsuarioIdLogado);
            return StatusCode(503, new { mensagem = "Não foi possível gerar sua dieta agora. Tente novamente em alguns instantes." });
        }

        await _repositorioDieta.SalvarAsync(UsuarioIdLogado, dieta);

        var response = new DietaResponse
        {
            DataGeracao = DateTime.UtcNow,
            Dieta = dieta
        };

        return Ok(response);
    }

    /// <summary>
    /// Retorna a dieta mais recente gerada para o usuário logado.
    /// </summary>
    [HttpGet("atual")]
    public async Task<IActionResult> ObterAtual()
    {
        var dietaIA = await _context.DietasIA
            .Where(d => d.UsuarioId == UsuarioIdLogado)
            .OrderByDescending(d => d.DataGeracao)
            .FirstOrDefaultAsync();

        if (dietaIA is null)
        {
            return NotFound(new { mensagem = "Nenhuma dieta gerada ainda." });
        }

        var response = new DietaResponse
        {
            DataGeracao = dietaIA.DataGeracao,
            Dieta = JsonSerializer.Deserialize<DietaSemanalDto>(dietaIA.ConteudoJson)!
        };

        return Ok(response);
    }

    /// <summary>
    /// Retorna o histórico de dietas geradas para o usuário logado (até 3,
    /// da mais recente para a mais antiga), conforme a política de retenção
    /// aplicada por IRepositorioDietaIA.
    /// </summary>
    [HttpGet("historico")]
    public async Task<IActionResult> ObterHistorico()
    {
        var dietasIA = await _context.DietasIA
            .Where(d => d.UsuarioId == UsuarioIdLogado)
            .OrderByDescending(d => d.DataGeracao)
            .ToListAsync();

        var response = dietasIA.Select(d => new DietaResponse
        {
            DataGeracao = d.DataGeracao,
            Dieta = JsonSerializer.Deserialize<DietaSemanalDto>(d.ConteudoJson)!
        });

        return Ok(response);
    }
}
