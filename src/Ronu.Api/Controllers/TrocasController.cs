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

        if (await RecusaDaCasa(dieta, posicao) is { } daCasa)
        {
            return daCasa;
        }

        var lista = (await DaPessoa()).Lista;
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
            ForaPelaAlergia = r.ForaPelaAlergia,
            Refeicao = Escopo(r.Refeicao, r.ForaPeloEvitar, r.ForaPelaAlergia),
            Semana = Escopo(r.Semana, r.ForaPeloEvitar, r.ForaPelaAlergia)
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

        if (await RecusaDaCasa(dieta, posicao) is { } daCasa)
        {
            return daCasa;
        }

        // O substituto vem da lista da pessoa, já sem os alérgenos: um id com alergia nunca está entre as opções.
        var daPessoa = await DaPessoa();
        var antes = Ler(dietaIA!);
        var opcao = AplicadorTroca.Trocar(dieta, posicao, request.AlimentoIdNovo!, escopo, daPessoa.Lista, dietaIA!.Revisao + 1);
        if (opcao is null)
        {
            return BadRequest(new { mensagem = "Este substituto não serve aqui (porção, meta do dia, o que você marcou em Não como ou suas alergias). Atualize as opções e escolha outro." });
        }

        var (conflito, removidas) = await Gravar(dietaIA, antes, dieta, daPessoa.Uf);
        if (conflito is not null)
        {
            return conflito;
        }

        // Só ids e escopo: nenhum dado de saúde no log (nem as alergias, nem quantas são).
        _logger.LogInformation("Troca de alimento do usuário {UsuarioId}: '{De}' por '{Para}', {Escopo}, {Itens} itens",
            UsuarioIdLogado, atual.AlimentoId, opcao.AlimentoId, escopo, opcao.Ocorrencias.Count);
        return Ok(Resposta(dietaIA, removidas, daPessoa.Alergias));
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

        var daPessoa = await DaPessoa();
        if (VoltaComAlergia(antes, dieta!, daPessoa.Alergias) is { } recusa)
        {
            return recusa;
        }

        var (conflito, removidas) = await Gravar(dietaIA, antes, dieta!, daPessoa.Uf);
        if (conflito is not null)
        {
            return conflito;
        }

        return Ok(Resposta(dietaIA, removidas, daPessoa.Alergias));
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

        var daPessoa = await DaPessoa();
        if (VoltaComAlergia(antes, dieta, daPessoa.Alergias) is { } recusa)
        {
            return recusa;
        }

        var (conflito, removidas) = await Gravar(dietaIA, antes, dieta, daPessoa.Uf);
        if (conflito is not null)
        {
            return conflito;
        }

        return Ok(Resposta(dietaIA, removidas, daPessoa.Alergias));
    }

    // TACO 2c: o "Desfazer" e o "Voltar ao original" não trazem de volta um alimento com alergia marcada depois da
    // troca. 422, e não 409: a dieta não mudou, o pedido é que não pode ser atendido. Nada é gravado (a mudança só
    // existia no objeto em memória). O motivo "alergia" diz à tela que a dieta não mudou por fora: ela mostra a
    // mensagem no lugar, sem recarregar (os outros 422 da troca são dieta antiga ou base nova).
    private UnprocessableEntityObjectResult? VoltaComAlergia(DietaSemanalDto antes, DietaSemanalDto depois, IReadOnlyList<string> alergias)
    {
        if (AlergiasNaDieta.QueVolta(antes, depois, alergias) is not { } volta)
        {
            return null;
        }

        return UnprocessableEntity(new
        {
            mensagem = $"Não dá para voltar a \"{volta.Alimento.Nome}\": ele tem uma das suas alergias ({volta.Chip.Rotulo}). Escolha outro substituto em Trocar.",
            motivo = "alergia"
        });
    }

    // Plano família (etapa 5): um item do almoço ou do jantar da casa não se troca aqui. A troca na casa é só do dono e
    // vale para todos (POST /api/casa/semana/trocas); o membro tem "Pedir troca" (POST /api/casa/pedidos). Até a
    // etapa 4, esta rota deixava um membro trocar o item só no prato dele. Quem saiu da casa tem DaCasa = false e
    // troca normalmente. 409 com o motivo, para a tela levar cada um ao caminho certo.
    private async Task<IActionResult?> RecusaDaCasa(DietaSemanalDto dieta, PosicaoItem posicao)
    {
        if (!dieta.Dias[posicao.Dia].Refeicoes[posicao.Refeicao].DaCasa)
        {
            return null;
        }

        var souDono = await _context.MembrosCasa.AnyAsync(m => m.UsuarioId == UsuarioIdLogado && m.Papel == MembroCasa.PapelDono);
        return Conflict(new
        {
            mensagem = souDono
                ? "Este alimento é da refeição da casa. Troque pela semana da casa: a troca vale para todos."
                : "Este alimento é da refeição da casa: só quem cuida da casa troca. Use Pedir troca.",
            motivo = MotivoRefeicaoDaCasa
        });
    }

    public const string MotivoRefeicaoDaCasa = "refeicao_da_casa";

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

    // As preferências e alergias ATUAIS da pessoa (um "não como" ou um chip novo vale já na troca) e a UF, para o nome
    // regional. Pelo ponto único, que lê as alergias.
    private Task<ListaDaPessoa.Resultado> DaPessoa() => ListaDaPessoa.MontarAsync(_context, UsuarioIdLogado);

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

    // Com o aviso das alergias recalculado: depois da troca, o painel mostra o que ainda sobrou (ou some).
    private static TrocaResponse Resposta(DietaIA dietaIA, int marcacoesRemovidas, IReadOnlyList<string> alergias)
    {
        var r = DietasController.Resposta(dietaIA);
        return new TrocaResponse
        {
            DataGeracao = r.DataGeracao, Dieta = r.Dieta, DietaId = r.DietaId, Revisao = r.Revisao, SemTroca = r.SemTroca,
            AlergiasNaDieta = AlergiasNaDieta.Calcular(r.Dieta, alergias),
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

    private static EscopoTrocaDto Escopo(OpcoesDoEscopo e, int foraPeloEvitar, int foraPelaAlergia) => new()
    {
        DasPreferencias = e.DasPreferencias.Select(Opcao).ToList(),
        Parecidos = e.Parecidos.Select(Opcao).ToList(),
        VerMais = e.VerMais.Select(Opcao).ToList(),
        Fora = e.Fora.ToDictionary(f => Motivo(f.Key), f => f.Value),
        MensagemSemOpcoes = e.Nenhuma ? MensagemSemOpcoes(e.Fora, foraPeloEvitar, foraPelaAlergia) : null
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
    public static string MensagemSemOpcoes(IReadOnlyDictionary<MotivoForaDaTroca, int> fora, int foraPeloEvitar, int foraPelaAlergia = 0)
    {
        var partes = new List<string>();
        void Parte(int n, string um, string varios)
        {
            if (n > 0) partes.Add(n == 1 ? um : $"{n} {varios}");
        }

        Parte(fora.GetValueOrDefault(MotivoForaDaTroca.PorcaoMaxima), "1 passaria da porção máxima", "passariam da porção máxima");
        Parte(fora.GetValueOrDefault(MotivoForaDaTroca.ForaDaMeta), "1 deixaria o dia mais de 5% fora da meta", "deixariam o dia mais de 5% fora da meta");
        // Etapa 3: o bloqueio se chama "Não como" na tela (o "evitar" leve não tira item da troca).
        Parte(foraPeloEvitar, "1 está no que você marcou em Não como", "estão no que você marcou em Não como");
        Parte(foraPelaAlergia, "1 tem uma das suas alergias", "têm uma das suas alergias");
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
