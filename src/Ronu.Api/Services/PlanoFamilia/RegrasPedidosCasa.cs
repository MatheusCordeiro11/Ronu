using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// Os pedidos e avisos da casa (plano família, etapa 5). O pedido não diz quem pediu, nem na resposta nem no banco
/// (PedidoCasa não tem UsuarioId). Sem o autor, três coisas seguram o abuso:
/// 1. o limite de requisições por usuário (LimitesCasa.PoliticaPedido), que fica só na memória;
/// 2. um pedido igual a um que já está aberto (mesma semana, mesmo tipo, mesma refeição e alimento) não vira linha
///    nova, e a resposta é a mesma (202) nos dois casos: quem pede não descobre se outra pessoa já pediu o mesmo;
/// 3. um teto de pedidos abertos por casa (MaximoAbertos).
/// O "Pedido enviado" do membro fica na tela dele (guardado por semana), e não no servidor: guardar "quem já pediu"
/// seria guardar o autor (decisão listada para o usuário).
/// </summary>
public static class RegrasPedidosCasa
{
    // Calibração do projeto: cobre "uma troca por refeição da casa" (14) com folga, e segura um membro pedindo em laço
    // além do limite por hora (que é por pessoa, e a casa tem até 5 membros).
    public const int MaximoAbertos = 20;

    public const string MensagemDono = "Você cuida da casa: gere a semana nova ou troque o alimento direto.";
    public const string MensagemTipo = "Tipo de pedido inválido.";
    public const string MensagemSemSemana = "A casa ainda não tem uma semana gerada.";
    public const string MensagemSemanaMudou = "A semana da casa mudou desde que a tela foi aberta. Atualize e veja de novo.";
    public const string MensagemItem = "Alimento não encontrado na refeição da casa.";
    public const string MensagemCheia = "A casa já tem muitos pedidos abertos. Espere quem cuida da casa responder.";
    public const string MensagemNaoEncontrado = "Pedido não encontrado.";

    private static readonly string[] DiasPorExtenso = ["segunda", "terça", "quarta", "quinta", "sexta", "sábado", "domingo"];

    public record Erro(int Status, string Mensagem);

    /// <summary>
    /// Um pedido de membro. Devolve o erro (400, 404 sem semana, 409 semana mudou ou casa cheia de pedidos) ou nulo.
    /// Não grava: o SaveChanges é de quem chama. Para a troca, a posição é a da refeição da casa (a mesma na semana da
    /// casa e na porção de cada um), e o alimento atual, quando vem, precisa ser o que está lá.
    /// </summary>
    public static async Task<Erro?> PedirAsync(ApplicationDbContext contexto, int casaId, PedidoCasaRequest pedido, DateTime agora)
    {
        if (pedido.Tipo is not (PedidoCasa.TipoNovaSemana or PedidoCasa.TipoTroca))
        {
            return new Erro(400, MensagemTipo);
        }

        var semana = await GeracaoSemanaCasa.AtualAsync(contexto, casaId);
        // A tela pediu sobre uma semana que já saiu: o pedido não faz mais sentido (a semana nova já é a resposta).
        if (pedido.SemanaId is { } semanaId && semanaId != semana?.Id)
        {
            return new Erro(409, MensagemSemanaMudou);
        }

        var novo = new PedidoCasa { CasaId = casaId, SemanaCasaId = semana?.Id, Tipo = pedido.Tipo!, CriadoEm = agora };
        if (pedido.Tipo == PedidoCasa.TipoTroca)
        {
            if (semana is null)
            {
                return new Erro(404, MensagemSemSemana);
            }

            if (pedido.SemanaId is null)
            {
                // Na troca, a semana é obrigatória: o item é uma posição dela.
                return new Erro(400, MensagemSemanaMudou);
            }

            var conteudo = JsonSerializer.Deserialize<SemanaCasaDto>(semana.ConteudoJson)!;
            var refeicao = conteudo.Dias.SelectMany(d => d.Refeicoes).FirstOrDefault(r => r.ChaveCasa == pedido.ChaveCasa);
            if (refeicao is null || pedido.Item is not { } item || item < 0 || item >= refeicao.Alimentos.Count)
            {
                return new Erro(400, MensagemItem);
            }

            var alimentoId = refeicao.Alimentos[item].AlimentoId;
            if (pedido.AlimentoId is not null && pedido.AlimentoId != alimentoId)
            {
                return new Erro(409, MensagemSemanaMudou);
            }

            novo.ChavesCasa = refeicao.ChaveCasa;
            novo.Item = item;
            novo.AlimentoId = alimentoId;
        }

        var abertos = await contexto.PedidosCasa.Where(p => p.CasaId == casaId).ToListAsync();
        if (abertos.Any(p => p.Tipo == novo.Tipo && p.SemanaCasaId == novo.SemanaCasaId && p.ChavesCasa == novo.ChavesCasa && p.AlimentoId == novo.AlimentoId))
        {
            // O mesmo pedido já está aberto: nada muda, e a resposta é igual à de um pedido novo.
            return null;
        }

        if (abertos.Count >= MaximoAbertos)
        {
            return new Erro(409, MensagemCheia);
        }

        contexto.PedidosCasa.Add(novo);
        return null;
    }

    /// <summary>O aviso de entrada no meio da semana: só as refeições, sem nome nem o que a pessoa não come. Não grava.</summary>
    public static void AvisarEntrada(ApplicationDbContext contexto, int casaId, int semanaId, IReadOnlyCollection<string> chaves, DateTime agora) =>
        contexto.PedidosCasa.Add(new PedidoCasa
        {
            CasaId = casaId,
            SemanaCasaId = semanaId,
            Tipo = PedidoCasa.TipoAvisoEntrada,
            ChavesCasa = string.Join(',', Ordenar(chaves)),
            CriadoEm = agora
        });

    /// <summary>
    /// Os pedidos e avisos abertos, para o dono (o CasaResponse). Sem data: o horário exato de um pedido ajudaria a
    /// adivinhar quem pediu ("foi logo depois do almoço, quando só a Bia estava em casa"). Na ordem de chegada.
    /// </summary>
    public static async Task<List<PedidoCasaResponse>> AbertosAsync(ApplicationDbContext contexto, int casaId)
    {
        var pedidos = await contexto.PedidosCasa.AsNoTracking()
            .Where(p => p.CasaId == casaId)
            .OrderBy(p => p.CriadoEm).ThenBy(p => p.Id)
            .ToListAsync();
        if (pedidos.Count == 0)
        {
            return [];
        }

        // O nome do alimento é o da semana da casa (o regional do dono); sem semana, o da base.
        var semana = await GeracaoSemanaCasa.AtualAsync(contexto, casaId);
        var nomes = semana is null
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<SemanaCasaDto>(semana.ConteudoJson)!.Dias
                .SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos)
                .Where(a => a.AlimentoId is not null)
                .GroupBy(a => a.AlimentoId!)
                .ToDictionary(g => g.Key, g => g.First().Nome);

        return pedidos.Select(p =>
        {
            var chaves = string.IsNullOrEmpty(p.ChavesCasa) ? [] : p.ChavesCasa.Split(',').ToList();
            string? alimento = null;
            if (p.AlimentoId is not null)
            {
                alimento = nomes.TryGetValue(p.AlimentoId, out var nome)
                    ? nome
                    : BaseNutricional.Itens.FirstOrDefault(i => i.Id == p.AlimentoId)?.Nome;
            }

            return new PedidoCasaResponse
            {
                Id = p.Id,
                Tipo = p.Tipo,
                SemanaId = p.SemanaCasaId,
                ChavesCasa = chaves,
                Item = p.Item,
                AlimentoId = p.AlimentoId,
                Alimento = alimento,
                Texto = Texto(p.Tipo, chaves, alimento)
            };
        }).ToList();
    }

    /// <summary>A frase do aviso, sempre com "Alguém": nunca o nome, o papel ou qualquer pista de quem foi.</summary>
    public static string Texto(string tipo, IReadOnlyList<string> chaves, string? alimento)
    {
        switch (tipo)
        {
            case PedidoCasa.TipoNovaSemana:
                return "Alguém pediu uma semana nova.";
            case PedidoCasa.TipoTroca:
                var nome = string.IsNullOrEmpty(alimento) ? "um alimento" : char.ToLowerInvariant(alimento[0]) + alimento[1..];
                return $"Alguém pediu trocar {nome} {Refeicao(chaves[0], "no")}.";
            default:
                var refeicoes = chaves.Select(c => Refeicao(c, null)).ToList();
                var onde = refeicoes.Count == 1
                    ? $"um item {Refeicao(chaves[0], "do")}"
                    : $"um item de {refeicoes.Count} refeições da casa ({Juntar(refeicoes)})";
                var essa = refeicoes.Count == 1 ? "Essa refeição ficou individual" : "Essas refeições ficaram individuais";
                return $"Alguém que entrou esta semana não come {onde}. {essa} para essa pessoa; uma semana nova resolve.";
        }
    }

    // "no jantar de quinta", "do almoço de segunda", "almoço de segunda".
    private static string Refeicao(string chave, string? preposicao)
    {
        var indice = Array.IndexOf(["seg", "ter", "qua", "qui", "sex", "sab", "dom"], chave.Split('-')[0]);
        var dia = indice >= 0 ? DiasPorExtenso[indice] : chave;
        var refeicao = PorcoesCasa.EAlmoco(chave) ? "almoço" : "jantar";
        return preposicao is null ? $"{refeicao} de {dia}" : $"{preposicao} {refeicao} de {dia}";
    }

    private static string Juntar(IReadOnlyList<string> partes) =>
        partes.Count == 1 ? partes[0] : string.Join(", ", partes.SkipLast(1)) + " e " + partes[^1];

    // Na ordem da semana (segunda a domingo, almoço antes do jantar), como a tela mostra.
    private static IEnumerable<string> Ordenar(IEnumerable<string> chaves)
    {
        string[] dias = ["seg", "ter", "qua", "qui", "sex", "sab", "dom"];
        return chaves.Distinct().OrderBy(c => Array.IndexOf(dias, c.Split('-')[0])).ThenBy(c => PorcoesCasa.EAlmoco(c) ? 0 : 1);
    }
}
