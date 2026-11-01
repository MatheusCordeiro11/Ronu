using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;
using Ronu.Api.Services.ListaCompras;
using Ronu.Api.Services.Troca;
using System.Security.Claims;
using System.Text.Json;

namespace Ronu.Api.Controllers;

/// <summary>
/// Troca de alimento na dieta atual do usuário logado: as opções, a troca, o "Desfazer" e o "Voltar ao
/// original". A dieta é sempre buscada pelo usuário do token; o dietaId que vem do cliente só é comparado com
/// ela, então ninguém chega à dieta de outra pessoa (cai no 409, sem saber se a dieta existe). Toda gravação
/// confere a revisão: duas abas trocando ao mesmo tempo não sobrescrevem uma à outra.
/// </summary>
[ApiController]
[Route("api/dietas/atual/trocas")]
[Authorize]
public class TrocasController : ControllerBase
{
    private const string MensagemMudou = "Sua dieta mudou desde que a tela foi aberta. Atualize e tente de novo.";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<TrocasController> _logger;

    public TrocasController(ApplicationDbContext context, ILogger<TrocasController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("opcoes")]
    [EnableRateLimiting(LimitesTroca.PoliticaLeitura)]
    public async Task<IActionResult> Opcoes([FromQuery] int dia, [FromQuery] int refeicao, [FromQuery] int item)
    {
        var dietaIA = await DietaAtual();
        if (dietaIA is null)
        {
            return NotFound(new { mensagem = "Nenhuma dieta gerada ainda." });
        }

        var dieta = Ler(dietaIA);
        if (SemTroca(dieta) is { } semTroca)
        {
            return semTroca;
        }

        var posicao = new PosicaoItem(dia, refeicao, item);
        if (CalculadoraTroca.Encontrar(dieta.Dias, posicao) is null)
        {
            return BadRequest(new { mensagem = "Alimento não encontrado na dieta." });
        }

        var (lista, _) = await ListaDaPessoa();
        var r = CalculadoraTroca.Calcular(dieta.Dias, posicao, lista);
        var nutriente = r.Base.Nutriente;

        return Ok(new OpcoesTrocaResponse
        {
            DietaId = dietaIA.Id,
            Revisao = dietaIA.Revisao,
            Original = new ItemTrocaDto
            {
                AlimentoId = r.Base.Id,
                Nome = r.Original.Nome,
                Quantidade = r.Original.Quantidade,
                Unidade = r.Original.Unidade,
                Kcal = r.Base.Kcal * r.Original.Quantidade / 100,
                NutrienteG = nutriente == NutrientePrincipal.Livre ? null : NutrienteG(r.Base, r.Original.Quantidade)
            },
            Nutriente = nutriente switch
            {
                NutrientePrincipal.Proteina => "proteina",
                NutrientePrincipal.Carboidrato => "carboidrato",
                NutrientePrincipal.Gordura => "gordura",
                _ => "livre"
            },
            OcorrenciasNaSemana = r.OcorrenciasNaSemana,
            ForaPeloEvitar = r.ForaPeloEvitar,
            Refeicao = Escopo(r.Refeicao, r.ForaPeloEvitar),
            Semana = Escopo(r.Semana, r.ForaPeloEvitar)
        });
    }

    [HttpPost]
    [EnableRateLimiting(LimitesTroca.PoliticaEscrita)]
    public async Task<IActionResult> Trocar([FromBody] TrocaRequest request)
    {
        if (LerEscopo(request.Escopo) is not { } escopo)
        {
            return BadRequest(new { mensagem = "Escopo inválido." });
        }

        var (dietaIA, dieta, erro) = await CarregarParaGravar(request.DietaId!.Value, request.Revisao!.Value);
        if (erro is not null)
        {
            return erro;
        }

        var posicao = new PosicaoItem(request.Dia!.Value, request.Refeicao!.Value, request.Item!.Value);
        var atual = CalculadoraTroca.Encontrar(dieta!.Dias, posicao);
        if (atual is null)
        {
            return BadRequest(new { mensagem = "Alimento não encontrado na dieta." });
        }

        // A posição pode ter outro alimento se a tela estava desatualizada: confere o item, não só o índice.
        if (atual.AlimentoId != request.AlimentoIdAtual)
        {
            return Conflict(new { mensagem = MensagemMudou });
        }

        var (lista, uf) = await ListaDaPessoa();
        var antes = Ler(dietaIA!);
        var opcao = AplicadorTroca.Trocar(dieta, posicao, request.AlimentoIdNovo!, escopo, lista, dietaIA!.Revisao + 1);
        if (opcao is null)
        {
            return BadRequest(new { mensagem = "Este substituto não serve aqui (porção, meta do dia ou o que você marcou para evitar). Atualize as opções e escolha outro." });
        }

        var (conflito, removidas) = await Gravar(dietaIA, antes, dieta, uf);
        if (conflito is not null)
        {
            return conflito;
        }

        // Só ids e escopo: nenhum dado de saúde no log.
        _logger.LogInformation("Troca de alimento do usuário {UsuarioId}: '{De}' por '{Para}', {Escopo}, {Itens} itens",
            UsuarioIdLogado, atual.AlimentoId, opcao.AlimentoId, escopo, opcao.Ocorrencias.Count);
        return Ok(Resposta(dietaIA, removidas));
    }

    /// <summary>"Desfazer": volta exatamente ao que estava antes da última troca ou volta ao original.</summary>
    [HttpDelete("ultima")]
    [EnableRateLimiting(LimitesTroca.PoliticaEscrita)]
    public async Task<IActionResult> Desfazer([FromQuery] int dietaId, [FromQuery] int revisao)
    {
        var (dietaIA, dieta, erro) = await CarregarParaGravar(dietaId, revisao);
        if (erro is not null)
        {
            return erro;
        }

        var antes = Ler(dietaIA!);
        if (!AplicadorTroca.Desfazer(dieta!, dietaIA!.Revisao))
        {
            return Conflict(new { mensagem = "Não há troca para desfazer." });
        }

        var (_, uf) = await ListaDaPessoa();
        var (conflito, removidas) = await Gravar(dietaIA, antes, dieta!, uf);
        if (conflito is not null)
        {
            return conflito;
        }

        return Ok(Resposta(dietaIA, removidas));
    }

    /// <summary>"Voltar ao original": o item (ou, na semana, todos os trocados do mesmo jeito) volta ao primeiro original.</summary>
    [HttpDelete]
    [EnableRateLimiting(LimitesTroca.PoliticaEscrita)]
    public async Task<IActionResult> VoltarAoOriginal(
        [FromQuery] int dietaId, [FromQuery] int revisao, [FromQuery] int dia, [FromQuery] int refeicao, [FromQuery] int item,
        [FromQuery] string? escopo)
    {
        if (LerEscopo(escopo) is not { } escopoTroca)
        {
            return BadRequest(new { mensagem = "Escopo inválido." });
        }

        var (dietaIA, dieta, erro) = await CarregarParaGravar(dietaId, revisao);
        if (erro is not null)
        {
            return erro;
        }

        var posicao = new PosicaoItem(dia, refeicao, item);
        if (CalculadoraTroca.Encontrar(dieta!.Dias, posicao) is null)
        {
            return BadRequest(new { mensagem = "Alimento não encontrado na dieta." });
        }

        var antes = Ler(dietaIA!);
        if (AplicadorTroca.VoltarAoOriginal(dieta, posicao, escopoTroca, dietaIA!.Revisao + 1) == 0)
        {
            return BadRequest(new { mensagem = "Este alimento não foi trocado." });
        }

        var (_, uf) = await ListaDaPessoa();
        var (conflito, removidas) = await Gravar(dietaIA, antes, dieta, uf);
        if (conflito is not null)
        {
            return conflito;
        }

        return Ok(Resposta(dietaIA, removidas));
    }

    // A mesma regra do GET /api/dietas/atual: a mais recente do usuário do token.
    private Task<DietaIA?> DietaAtual() => _context.DietasIA
        .Where(d => d.UsuarioId == UsuarioIdLogado)
        .OrderByDescending(d => d.DataGeracao)
        .FirstOrDefaultAsync();

    private static DietaSemanalDto Ler(DietaIA dietaIA) => JsonSerializer.Deserialize<DietaSemanalDto>(dietaIA.ConteudoJson)!;

    private async Task<(DietaIA?, DietaSemanalDto?, IActionResult?)> CarregarParaGravar(int dietaId, int revisao)
    {
        var dietaIA = await DietaAtual();
        if (dietaIA is null)
        {
            return (null, null, NotFound(new { mensagem = "Nenhuma dieta gerada ainda." }));
        }

        // Um id de dieta de outra pessoa nunca é a dieta atual de quem chama, então cai aqui também.
        if (dietaId != dietaIA.Id || revisao != dietaIA.Revisao)
        {
            return (null, null, Conflict(new { mensagem = MensagemMudou }));
        }

        var dieta = Ler(dietaIA);
        return SemTroca(dieta) is { } semTroca ? (null, null, semTroca) : (dietaIA, dieta, null);
    }

    private IActionResult? SemTroca(DietaSemanalDto dieta) => CalculadoraTroca.MotivoSemTroca(dieta) switch
    {
        MotivoSemTroca.DietaAnterior => UnprocessableEntity(new
        {
            mensagem = "Esta dieta foi gerada antes da troca de alimentos existir. Gere uma dieta nova para trocar alimentos."
        }),
        MotivoSemTroca.OutraVersaoDaBase => UnprocessableEntity(new
        {
            mensagem = "A base de alimentos do Ronu mudou desde que esta dieta foi gerada. Gere uma dieta nova para trocar alimentos."
        }),
        _ => null
    };

    // As preferências ATUAIS da pessoa (um "evitar" novo vale já na troca) e a UF, para o nome regional.
    private async Task<(ListaAlimentos Lista, string? Uf)> ListaDaPessoa()
    {
        var uf = await _context.Usuarios.Where(u => u.Id == UsuarioIdLogado).Select(u => u.Estado).FirstOrDefaultAsync();
        var preferencias = await _context.PreferenciasAlimentares
            .Where(p => p.UsuarioId == UsuarioIdLogado)
            .OrderBy(p => p.Id)
            .Select(p => new PreferenciaContextoDto { Alimento = p.Alimento, Tipo = p.Tipo })
            .ToListAsync();
        return (ListaAlimentosPessoa.Montar(uf, preferencias), uf);
    }

    // Grava a dieta com a revisão nova e tira as marcações "já tenho" que deixaram de valer, numa gravação só.
    // A revisão é token de concorrência: se outra aba gravou no meio, o UPDATE não passa e nada é gravado.
    // Devolve quantas marcações caíram: as que existiam, não todas as chaves que deixaram de valer.
    private async Task<(IActionResult? Conflito, int MarcacoesRemovidas)> Gravar(
        DietaIA dietaIA, DietaSemanalDto antes, DietaSemanalDto depois, string? uf)
    {
        var caem = MarcacoesAposTroca.QueCaem(antes, depois, uf);
        List<ListaComprasMarcacao> marcacoes = caem.Count == 0
            ? []
            : await _context.ListaComprasMarcacoes.Where(m => m.DietaIAId == dietaIA.Id && caem.Contains(m.Chave)).ToListAsync();
        _context.ListaComprasMarcacoes.RemoveRange(marcacoes);

        dietaIA.ConteudoJson = JsonSerializer.Serialize(depois);
        dietaIA.Revisao++;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (Conflict(new { mensagem = MensagemMudou }), 0);
        }

        return (null, marcacoes.Count);
    }

    private static TrocaResponse Resposta(DietaIA dietaIA, int marcacoesRemovidas)
    {
        var r = DietasController.Resposta(dietaIA);
        return new TrocaResponse
        {
            DataGeracao = r.DataGeracao, Dieta = r.Dieta, DietaId = r.DietaId, Revisao = r.Revisao, SemTroca = r.SemTroca,
            MarcacoesRemovidas = marcacoesRemovidas
        };
    }

    private static EscopoTroca? LerEscopo(string? escopo) => escopo switch
    {
        "refeicao" => EscopoTroca.Refeicao,
        "semana" => EscopoTroca.Semana,
        _ => null
    };

    private static decimal NutrienteG(AlimentoBase item, decimal quantidade) => item.Nutriente switch
    {
        NutrientePrincipal.Proteina => item.ProteinaG,
        NutrientePrincipal.Carboidrato => item.CarboidratoG,
        _ => item.GorduraG
    } * quantidade / 100;

    private static EscopoTrocaDto Escopo(OpcoesDoEscopo e, int foraPeloEvitar) => new()
    {
        DasPreferencias = e.DasPreferencias.Select(Opcao).ToList(),
        Parecidos = e.Parecidos.Select(Opcao).ToList(),
        VerMais = e.VerMais.Select(Opcao).ToList(),
        Fora = e.Fora.ToDictionary(f => Motivo(f.Key), f => f.Value),
        MensagemSemOpcoes = e.Nenhuma ? MensagemSemOpcoes(e.Fora, foraPeloEvitar) : null
    };

    private static OpcaoTrocaDto Opcao(OpcaoTroca o)
    {
        var tocado = o.Ocorrencias[0];
        return new OpcaoTrocaDto
        {
            AlimentoId = o.AlimentoId,
            Nome = tocado.Nome,
            Quantidade = tocado.Quantidade,
            Unidade = BaseNutricional.Itens.Single(i => i.Id == o.AlimentoId).Medida,
            Contagem = tocado.Contagem,
            Kcal = tocado.Kcal,
            DifKcal = o.DifKcal,
            NutrienteG = tocado.NutrienteG,
            OutroUso = !o.MesmoUso,
            Ocorrencias = o.Ocorrencias.Count,
            Dias = o.Dias.Select(d => new PreviaDiaDto
            {
                Dia = d.Dia, DiaSemana = d.DiaSemana, TotalAntes = d.TotalAntes, TotalDepois = d.TotalDepois,
                // Arredondado só na resposta: a regra dos 5% (PreviaDia.Passa) usa o valor inteiro.
                Meta = d.Meta, DesvioPct = Math.Round(d.DesvioPct, 2, MidpointRounding.AwayFromZero)
            }).ToList()
        };
    }

    private static string Motivo(MotivoForaDaTroca m) => m switch
    {
        MotivoForaDaTroca.UsoDiferente => "usoDiferente",
        MotivoForaDaTroca.JaNaRefeicao => "jaNaRefeicao",
        MotivoForaDaTroca.RefogadoComOleo => "refogadoComOleo",
        MotivoForaDaTroca.PorcaoMaxima => "porcaoMaxima",
        _ => "foraDaMeta"
    };

    /// <summary>Por que nenhum substituto passou, em uma frase, para a tela dizer em vez de mostrar uma lista vazia.</summary>
    public static string MensagemSemOpcoes(IReadOnlyDictionary<MotivoForaDaTroca, int> fora, int foraPeloEvitar)
    {
        var partes = new List<string>();
        void Parte(int n, string um, string varios)
        {
            if (n > 0) partes.Add(n == 1 ? um : $"{n} {varios}");
        }

        Parte(fora.GetValueOrDefault(MotivoForaDaTroca.PorcaoMaxima), "1 passaria da porção máxima", "passariam da porção máxima");
        Parte(fora.GetValueOrDefault(MotivoForaDaTroca.ForaDaMeta), "1 deixaria o dia mais de 5% fora da meta", "deixariam o dia mais de 5% fora da meta");
        Parte(foraPeloEvitar, "1 está no que você marcou para evitar", "estão no que você marcou para evitar");
        Parte(fora.GetValueOrDefault(MotivoForaDaTroca.JaNaRefeicao), "1 já está nesta refeição", "já estão nesta refeição");
        Parte(fora.GetValueOrDefault(MotivoForaDaTroca.RefogadoComOleo) + fora.GetValueOrDefault(MotivoForaDaTroca.UsoDiferente),
            "1 não combina com esta refeição", "não combinam com esta refeição");

        if (partes.Count == 0)
        {
            return "Este alimento não tem substituto entre os alimentos do Ronu.";
        }

        var lista = partes.Count == 1 ? partes[0] : string.Join(", ", partes.SkipLast(1)) + " e " + partes[^1];
        return $"Nenhum substituto serve aqui: {lista}.";
    }
}
