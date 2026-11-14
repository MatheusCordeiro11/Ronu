using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;
using Ronu.Api.Services.PlanoFamilia;
using Ronu.Api.Services.Troca;
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

    /// <summary>
    /// Gera uma nova dieta semanal via IA para o usuário logado, com base no
    /// objetivo, modalidades e preferências já cadastrados. Mantém só as 3
    /// dietas mais recentes por usuário. Limitado a 3 gerações por hora (429,
    /// LimiteGeracoes).
    ///
    /// Plano família: quem está numa casa que já tem semana gera só as refeições
    /// individuais (café da manhã e lanches), com o que sobra da meta depois das
    /// porções da casa, que entram na dieta calculadas em C#.
    /// </summary>
    [HttpPost("gerar")]
    public async Task<IActionResult> Gerar()
    {
        // Primeira checagem: a mais barata, e evita gastar cota do Gemini. A dieta que a semana da casa criou para um
        // membro não gasta a cota dele; a do dono, sim (foi ele quem pediu).
        var agora = DateTime.UtcNow;
        var membroCasa = await _context.MembrosCasa.AsNoTracking().FirstOrDefaultAsync(m => m.UsuarioId == UsuarioIdLogado);
        var limite = await LimiteGeracoes.ConferirAsync(_context, UsuarioIdLogado, membroCasa?.Papel == MembroCasa.PapelDono, agora);
        if (limite is not null)
        {
            return StatusCode(429, new { mensagem = limite });
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

        // A semana da casa entra só se for da versão da base de hoje (os AlimentoId dela precisam existir). Sem semana,
        // ou com uma de outra versão, quem está na casa gera a semana inteira, como fora dela.
        var semanaCasa = membroCasa is null ? null : await GeracaoSemanaCasa.AtualAsync(_context, membroCasa.CasaId, rastrear: true);
        if (semanaCasa is not null && semanaCasa.VersaoBase == BaseNutricional.VersaoBase)
        {
            contexto.SemanaCasa = JsonSerializer.Deserialize<SemanaCasaDto>(semanaCasa.ConteudoJson);
        }
        else
        {
            semanaCasa = null;
        }

        // Lista fechada sem nenhum item em algum papel no prato (ex.: nenhuma
        // proteína depois do "não como"): não dá para montar uma semana, então nem
        // chama o Gemini. Abaixo do mínimo, mas sem papel vazio, a dieta é gerada
        // com o aviso gravado (GeradorDietaGemini).
        var vazios = contexto.Lista?.Minimos.Vazios ?? [];
        if (vazios.Count > 0)
        {
            return BadRequest(new { mensagem = MinimosPorPapel.MensagemPapeisVazios(vazios) });
        }
        // Falhas de comunicação com o Gemini (rede, status de erro como 503) e
        // respostas inutilizáveis mesmo depois da nova tentativa do gerador
        // (bloqueada, cortada, JSON inválido, dias errados, ou sem resposta no
        // limite de cada tentativa) viram um 503 com mensagem amigável. O
        // TaskCanceledException fica como rede de segurança: o limite por
        // tentativa do gerador dispara antes do Timeout do HttpClient. Qualquer
        // outra exceção continua subindo para o GlobalExceptionHandler.
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

        // Só o id do usuário, o item e as kcal: nenhum dado pessoal no log.
        foreach (var bebida in dieta.BebidasRemovidas)
        {
            _logger.LogWarning(
                "Bebida alcoólica fora das preferências tirada da dieta do usuário {UsuarioId}: '{Alimento}' ({Quantidade} {Unidade}), {KcalDescontadas} kcal descontadas, {Dia} {Refeicao}",
                UsuarioIdLogado, bebida.Alimento, bebida.Quantidade, bebida.Unidade, bebida.KcalDescontadas, bebida.Dia, bebida.Refeicao);
        }

        // Uma refeição da casa com algo que a pessoa marcou depois (não como ou alergia) virou individual só para ela:
        // o dono vê o aviso na semana da casa, sem dizer quem nem qual.
        if (semanaCasa is not null && contexto.Lista is not null
            && PorcoesCasa.RefeicoesQueNaoServem(contexto.SemanaCasa!, contexto.Lista).Count > 0)
        {
            semanaCasa.RefeicaoIndividualEm = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        await _repositorioDieta.SalvarAsync(UsuarioIdLogado, dieta, semanaCasa?.Id);

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

        // TACO 2c: o aviso do painel. Só leitura: a dieta salva não muda quando a pessoa marca um chip.
        var resposta = Resposta(dietaIA);
        resposta.AlergiasNaDieta = AlergiasNaDieta.Calcular(resposta.Dieta, await ListaDaPessoa.AlergiasAsync(_context, UsuarioIdLogado));
        return Ok(resposta);
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

        return Ok(dietasIA.Select(Resposta));
    }

    /// <summary>A dieta salva com o id, a revisão e, quando não tem troca de alimento, o porquê.</summary>
    public static DietaResponse Resposta(DietaIA dietaIA)
    {
        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(dietaIA.ConteudoJson)!;
        return new DietaResponse
        {
            DataGeracao = dietaIA.DataGeracao,
            Dieta = dieta,
            DietaId = dietaIA.Id,
            Revisao = dietaIA.Revisao,
            SemTroca = CalculadoraTroca.MotivoSemTroca(dieta) switch
            {
                MotivoSemTroca.DietaAnterior => "dieta-anterior",
                MotivoSemTroca.OutraVersaoDaBase => "outra-versao-da-base",
                _ => null
            }
        };
    }
}
