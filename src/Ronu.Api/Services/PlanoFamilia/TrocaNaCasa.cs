using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;
using Ronu.Api.Services.ListaCompras;
using Ronu.Api.Services.Troca;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// A troca de alimento numa refeição da casa (plano família, etapa 5; decisão de 2026-10-26): só o dono troca, e vale
/// para todos. Usa o que a troca individual já tem (CalculadoraTroca e AplicadorTroca, puros), sobre a semana de
/// referência da casa, com a lista da casa (a interseção: o "não como" e as alergias de todos ficam de fora; o
/// "evitar" leve de qualquer um vem por último, como na etapa 3).
///
/// Depois da troca na referência, a porção de cada membro nas refeições afetadas é refeita (PorcoesCasa.Porcao, a
/// mesma conta da semana nova). O substituto precisa servir para TODOS: a porção refeita de cada um precisa ficar a
/// até 5% do alvo da refeição dele (ou não piorar o que já estava), a mesma margem da troca individual. Quem não serve
/// some das opções como "não serve para a casa", sem dizer para quem nem por quê.
///
/// A semana da casa e as dietas de todos são gravadas juntas, num SaveChanges só: a revisão da semana
/// (SemanaCasa.Revisao) faz o papel do par (dietaId, revisao) da troca individual, e a revisão de cada dieta também
/// sobe (uma tela aberta com a dieta antiga recarrega).
///
/// O "Desfazer" e o "Voltar ao original" seguem o padrão da troca individual (UltimaTroca e Original na referência), e
/// refazem a porção de cada um. Eles não trazem de volta um alimento que alguém da casa marcou depois em "não como" ou
/// nas alergias (422, como o da troca individual, sem dizer quem).
/// </summary>
public static class TrocaNaCasa
{
    public const string MensagemSemanaMudou = "A semana da casa mudou desde que a tela foi aberta. Atualize e tente de novo.";
    public const string MensagemBaseMudou = "A base de alimentos do Ronu mudou desde que esta semana foi gerada. Gere uma semana nova para trocar alimentos.";
    public const string MensagemItem = "Alimento não encontrado na refeição da casa.";
    public const string MensagemNaoServe = "Este substituto não serve para a casa (a porção de alguém, a meta do dia ou o que alguém não come). Atualize as opções e escolha outro.";
    public const string MensagemSemDesfazer = "Não há troca para desfazer.";
    public const string MensagemNaoTrocado = "Este alimento não foi trocado.";
    public const string MensagemVoltaComRestricao = "Não dá para voltar a esse alimento: ele não serve mais para todos da casa. Escolha outro substituto em Trocar.";
    public const string MotivoRestricaoDaCasa = "restricao_da_casa";

    // Motivo a mais, só da casa, no "fora" das opções (os outros são os da troca individual).
    public const string MotivoNaoServeParaACasa = "naoServeParaACasa";

    private static readonly IReadOnlyDictionary<string, AlimentoBase> PorId = BaseNutricional.Itens.ToDictionary(i => i.Id);

    public record Erro(int Status, string Mensagem, string? Motivo = null);

    /// <summary>A dieta atual de um membro, quando ela é desta semana da casa (as outras não mudam com a troca).</summary>
    public sealed record MembroNaSemana(int UsuarioId, DietaIA Linha, DietaSemanalDto Dieta, string? Uf);

    /// <summary>A semana da casa, a referência como dieta (para a CalculadoraTroca), a lista da casa e os membros.</summary>
    public sealed class Carga
    {
        public required SemanaCasa Linha { get; init; }
        public required SemanaCasaDto Conteudo { get; init; }
        public required DietaSemanalDto Referencia { get; init; }
        public required ListaAlimentos ListaDaCasa { get; init; }
        public required List<MembroNaSemana> Membros { get; init; }
        public required int DonoId { get; init; }
    }

    public static async Task<(Carga? Carga, Erro? Erro)> CarregarAsync(ApplicationDbContext contexto, int casaId, int donoId, bool rastrear)
    {
        var linha = await GeracaoSemanaCasa.AtualAsync(contexto, casaId, rastrear);
        if (linha is null)
        {
            return (null, new Erro(404, RegrasPedidosCasa.MensagemSemSemana));
        }

        var conteudo = JsonSerializer.Deserialize<SemanaCasaDto>(linha.ConteudoJson)!;
        if (conteudo.VersaoBase != BaseNutricional.VersaoBase)
        {
            return (null, new Erro(422, MensagemBaseMudou));
        }

        // A referência como dieta, com as mesmas listas: o AplicadorTroca muda os itens nelas, e a UltimaTroca volta
        // para o conteúdo da semana no fim (Gravar).
        var referencia = new DietaSemanalDto { Dias = conteudo.Dias, VersaoBase = conteudo.VersaoBase, UltimaTroca = conteudo.UltimaTroca };

        var ids = await contexto.MembrosCasa.AsNoTracking()
            .Where(m => m.CasaId == casaId)
            .OrderBy(m => m.EntrouEm).ThenBy(m => m.Id)
            .Select(m => m.UsuarioId)
            .ToListAsync();
        var lista = await ListaDaCasa.MontarAsync(contexto, ids, donoId);

        var membros = new List<MembroNaSemana>();
        foreach (var id in ids)
        {
            var dietas = rastrear ? contexto.DietasIA : contexto.DietasIA.AsNoTracking();
            var atual = await dietas.Where(d => d.UsuarioId == id)
                .OrderByDescending(d => d.DataGeracao).ThenByDescending(d => d.Id)
                .FirstOrDefaultAsync();
            if (atual is null || atual.SemanaCasaId != linha.Id)
            {
                // Sem dieta desta semana (perfil incompleto, ou uma dieta mais nova de outra origem): nada a refazer.
                continue;
            }

            var uf = await contexto.Usuarios.AsNoTracking().Where(u => u.Id == id).Select(u => u.Estado).FirstOrDefaultAsync();
            membros.Add(new MembroNaSemana(id, atual, JsonSerializer.Deserialize<DietaSemanalDto>(atual.ConteudoJson)!, uf));
        }

        return (new Carga { Linha = linha, Conteudo = conteudo, Referencia = referencia, ListaDaCasa = lista, Membros = membros, DonoId = donoId }, null);
    }

    /// <summary>A posição do item na referência, pela chave da refeição ("qui-jantar") e o índice do item; nula se não existe.</summary>
    public static PosicaoItem? Posicao(DietaSemanalDto referencia, string? chaveCasa, int item)
    {
        for (var d = 0; d < referencia.Dias.Count; d++)
        {
            var r = referencia.Dias[d].Refeicoes.FindIndex(x => x.ChaveCasa == chaveCasa);
            if (r >= 0)
            {
                var posicao = new PosicaoItem(d, r, item);
                return CalculadoraTroca.Encontrar(referencia.Dias, posicao) is null ? null : posicao;
            }
        }

        return null;
    }

    // ---------- Opções ----------

    /// <summary>
    /// Os substitutos de um item da casa. As quantidades, as kcal e a prévia dos dias são só as do dono (na porção
    /// dele): nada da porção de outro membro sai daqui. Um substituto que não serve para alguém sai das opções e conta
    /// em "naoServeParaACasa", sem dizer para quem.
    /// </summary>
    public static OpcoesTrocaCasaResponse Opcoes(Carga carga, PosicaoItem posicao)
    {
        var r = CalculadoraTroca.Calcular(carga.Referencia.Dias, posicao, carga.ListaDaCasa);
        var dono = carga.Membros.FirstOrDefault(m => m.UsuarioId == carga.DonoId);
        var chave = Chave(carga.Referencia, posicao);
        var original = ItemDoDono(dono, chave, posicao.Item) ?? r.Original;
        var nutriente = r.Base.Nutriente;

        // Os do grupo que a lista da casa tirou (o "não como" e as alergias de todos, juntos). Uma contagem só, sem
        // separar alergia: "1 tem alergia de alguém da casa" já seria dado de saúde de alguém.
        var naCasa = carga.ListaDaCasa.Itens.Select(i => i.Item.Id).ToHashSet();
        var foraPelaCasa = BaseNutricional.Itens.Count(i => i.Grupo == r.Base.Grupo && i.Id != r.Base.Id && !naCasa.Contains(i.Id));

        return new OpcoesTrocaCasaResponse
        {
            SemanaId = carga.Linha.Id,
            Revisao = carga.Linha.Revisao,
            ChaveCasa = chave,
            Item = posicao.Item,
            Original = new ItemTrocaDto
            {
                AlimentoId = r.Base.Id,
                Nome = original.Nome,
                Quantidade = original.Quantidade,
                Unidade = r.Base.Medida,
                Kcal = r.Base.Kcal * original.Quantidade / 100,
                NutrienteG = nutriente == NutrientePrincipal.Livre ? null : NutrienteG(r.Base, original.Quantidade)
            },
            Nutriente = nutriente switch
            {
                NutrientePrincipal.Proteina => "proteina",
                NutrientePrincipal.Carboidrato => "carboidrato",
                NutrientePrincipal.Gordura => "gordura",
                _ => "livre"
            },
            OcorrenciasNaSemana = r.OcorrenciasNaSemana,
            ForaPelasRestricoesDaCasa = foraPelaCasa,
            Refeicao = Escopo(carga, posicao, EscopoTroca.Refeicao, r.Refeicao, dono, foraPelaCasa),
            Semana = Escopo(carga, posicao, EscopoTroca.Semana, r.Semana, dono, foraPelaCasa)
        };
    }

    private static EscopoTrocaDto Escopo(Carga carga, PosicaoItem posicao, EscopoTroca escopo, OpcoesDoEscopo calculadas, MembroNaSemana? dono, int foraPelaCasa)
    {
        var servem = new List<OpcaoTrocaDto>();
        var naoServem = 0;
        foreach (var opcao in calculadas.DasPreferencias.Concat(calculadas.Parecidos).Concat(calculadas.VerMais))
        {
            // Simula numa cópia da referência: a troca de verdade só acontece no POST.
            var copia = Clonar(carga.Referencia);
            AplicadorTroca.Trocar(copia, posicao, opcao.AlimentoId, escopo, carga.ListaDaCasa, 0);
            var chaves = ChavesAfetadas(copia, opcao.Ocorrencias.Select(o => o.Posicao));
            var porcoes = carga.Membros.ToDictionary(m => m.UsuarioId, m => NovasPorcoes(m, copia, chaves));
            if (!porcoes.Values.All(ServeParaOMembro))
            {
                naoServem++;
                continue;
            }

            servem.Add(Opcao(opcao, posicao, Chave(copia, posicao), dono is null ? null : porcoes[dono.UsuarioId], dono));
        }

        var fora = calculadas.Fora.ToDictionary(f => Motivo(f.Key), f => f.Value);
        if (naoServem > 0)
        {
            fora[MotivoNaoServeParaACasa] = naoServem;
        }

        // A casa não tem "gosto" (os preferidos de cada um ficam para as refeições individuais, decisão 4): as opções
        // vão em parecidos e "ver mais", na ordem da CalculadoraTroca.
        return new EscopoTrocaDto
        {
            DasPreferencias = [],
            Parecidos = servem.Take(CalculadoraTroca.OpcoesPorSecao).ToList(),
            VerMais = servem.Skip(CalculadoraTroca.OpcoesPorSecao).ToList(),
            Fora = fora,
            MensagemSemOpcoes = servem.Count == 0 ? MensagemSemOpcoes(fora, foraPelaCasa) : null
        };
    }

    // As quantidades e a prévia são as do dono; sem a dieta dele nesta semana, as da referência (que não são de ninguém).
    private static OpcaoTrocaDto Opcao(OpcaoTroca opcao, PosicaoItem posicao, string chave, List<PorcaoRefeita>? doDono, MembroNaSemana? dono)
    {
        var substituto = PorId[opcao.AlimentoId];
        var tocadoRef = opcao.Ocorrencias[0];
        var noPrato = doDono?.FirstOrDefault(p => p.Chave == chave)?.Depois.Alimentos[posicao.Item];
        var quantidade = noPrato?.Quantidade ?? tocadoRef.Quantidade;
        var dias = doDono is null || dono is null
            ? opcao.Dias.Select(d => (d.Dia, d.DiaSemana, d.TotalAntes, d.TotalDepois, d.Meta, d.DesvioPct)).ToList()
            : doDono.GroupBy(p => p.Dia).OrderBy(g => g.Key).Select(g =>
            {
                var dia = dono.Dieta.Dias[g.Key];
                var antes = dia.TotalDoDia.Calorias;
                var depois = antes + g.Sum(p => p.Depois.Macros.Calorias - p.Antes.Macros.Calorias);
                var previa = new PreviaDia(g.Key, dia.DiaSemana, antes, depois, dia.MetaCalculada.Calorias);
                return (previa.Dia, previa.DiaSemana, previa.TotalAntes, previa.TotalDepois, previa.Meta, previa.DesvioPct);
            }).ToList();

        return new OpcaoTrocaDto
        {
            AlimentoId = opcao.AlimentoId,
            Nome = noPrato?.Nome ?? tocadoRef.Nome,
            Quantidade = quantidade,
            Unidade = substituto.Medida,
            Contagem = substituto.MedidaUnidade is { } unidade ? (int)Math.Max(1, Math.Round(quantidade / unidade.Gramas, MidpointRounding.AwayFromZero)) : null,
            Kcal = substituto.Kcal * quantidade / 100,
            DifKcal = doDono is null ? opcao.DifKcal : doDono.Sum(p => p.Depois.Macros.Calorias - p.Antes.Macros.Calorias),
            NutrienteG = substituto.Nutriente == NutrientePrincipal.Livre ? null : NutrienteG(substituto, quantidade),
            OutroUso = !opcao.MesmoUso,
            Ocorrencias = opcao.Ocorrencias.Count,
            Dias = dias.Select(d => new PreviaDiaDto
            {
                Dia = d.Dia, DiaSemana = d.DiaSemana, TotalAntes = d.TotalAntes, TotalDepois = d.TotalDepois, Meta = d.Meta,
                DesvioPct = Math.Round(d.DesvioPct, 2, MidpointRounding.AwayFromZero)
            }).ToList()
        };
    }

    // ---------- Gravações ----------

    /// <summary>
    /// Troca o item (ou, na semana, todas as vezes em que ele aparece na casa) e refaz a porção de cada um. Não grava
    /// (o SaveChanges é do Gravar). Erro: 400 quando o substituto não está entre as opções ou não serve para alguém.
    /// </summary>
    public static Erro? Trocar(Carga carga, PosicaoItem posicao, string alimentoIdNovo, EscopoTroca escopo, out IReadOnlyList<string> chavesAfetadas)
    {
        chavesAfetadas = [];
        var opcao = AplicadorTroca.Trocar(carga.Referencia, posicao, alimentoIdNovo, escopo, carga.ListaDaCasa, carga.Linha.Revisao + 1);
        if (opcao is null)
        {
            return new Erro(400, MensagemNaoServe);
        }

        var chaves = ChavesAfetadas(carga.Referencia, opcao.Ocorrencias.Select(o => o.Posicao));
        chavesAfetadas = chaves;
        var porcoes = carga.Membros.Select(m => (Membro: m, Porcoes: NovasPorcoes(m, carga.Referencia, chaves))).ToList();
        if (!porcoes.All(p => ServeParaOMembro(p.Porcoes)))
        {
            // A referência mudou só em memória: nada é gravado.
            return new Erro(400, MensagemNaoServe);
        }

        foreach (var (membro, novas) in porcoes)
        {
            AplicarNoMembro(membro, novas);
        }

        return null;
    }

    /// <summary>"Desfazer" da casa: volta a referência ao que estava antes da última troca e refaz as porções.</summary>
    public static Erro? Desfazer(Carga carga)
    {
        var ultima = carga.Referencia.UltimaTroca;
        if (ultima is null || ultima.Revisao != carga.Linha.Revisao)
        {
            return new Erro(409, MensagemSemDesfazer);
        }

        var posicoes = ultima.Itens.Select(i => new PosicaoItem(i.Dia, i.Refeicao, i.Item)).ToList();
        if (!AplicadorTroca.Desfazer(carga.Referencia, carga.Linha.Revisao))
        {
            return new Erro(409, MensagemSemDesfazer);
        }

        return RefazerDepoisDeVoltar(carga, posicoes);
    }

    /// <summary>"Voltar ao original" da casa: o item (ou, na semana, todos os trocados do mesmo jeito) volta ao primeiro original.</summary>
    public static Erro? VoltarAoOriginal(Carga carga, PosicaoItem posicao, EscopoTroca escopo)
    {
        if (AplicadorTroca.VoltarAoOriginal(carga.Referencia, posicao, escopo, carga.Linha.Revisao + 1) == 0)
        {
            return new Erro(400, MensagemNaoTrocado);
        }

        var posicoes = carga.Referencia.UltimaTroca!.Itens.Select(i => new PosicaoItem(i.Dia, i.Refeicao, i.Item)).ToList();
        return RefazerDepoisDeVoltar(carga, posicoes);
    }

    // O que volta precisa estar na lista da casa de agora: alguém pode ter marcado "não como" ou uma alergia depois da
    // troca. Sem a regra dos 5%: voltar é desfazer, como na troca individual.
    private static Erro? RefazerDepoisDeVoltar(Carga carga, List<PosicaoItem> posicoes)
    {
        var naCasa = carga.ListaDaCasa.Itens.Select(i => i.Item.Id).ToHashSet();
        if (posicoes.Any(p => CalculadoraTroca.Encontrar(carga.Referencia.Dias, p) is not { AlimentoId: { } id } || !naCasa.Contains(id)))
        {
            return new Erro(422, MensagemVoltaComRestricao, MotivoRestricaoDaCasa);
        }

        var chaves = ChavesAfetadas(carga.Referencia, posicoes);
        foreach (var membro in carga.Membros)
        {
            AplicarNoMembro(membro, NovasPorcoes(membro, carga.Referencia, chaves));
        }

        return null;
    }

    /// <summary>
    /// Grava a semana e as dietas, num SaveChanges só (tudo ou nada). Com as marcações "já tenho" que deixaram de valer
    /// na lista de cada um (como na troca individual) e, na troca, os pedidos de troca daquele alimento naquelas
    /// refeições, que a troca atendeu. Concorrência (outra aba trocou, outro membro gravou a dieta): 409.
    /// </summary>
    public static async Task<Erro?> GravarAsync(ApplicationDbContext contexto, Carga carga, IReadOnlyCollection<string> chavesAfetadas, string? alimentoAtendido)
    {
        foreach (var membro in carga.Membros)
        {
            var antes = JsonSerializer.Deserialize<DietaSemanalDto>(membro.Linha.ConteudoJson)!;
            var caem = MarcacoesAposTroca.QueCaem(antes, membro.Dieta, membro.Uf);
            if (caem.Count > 0)
            {
                contexto.ListaComprasMarcacoes.RemoveRange(
                    await contexto.ListaComprasMarcacoes.Where(m => m.DietaIAId == membro.Linha.Id && caem.Contains(m.Chave)).ToListAsync());
            }

            membro.Linha.ConteudoJson = JsonSerializer.Serialize(membro.Dieta);
            membro.Linha.Revisao++;
        }

        if (alimentoAtendido is not null)
        {
            contexto.PedidosCasa.RemoveRange(await contexto.PedidosCasa
                .Where(p => p.CasaId == carga.Linha.CasaId && p.Tipo == PedidoCasa.TipoTroca && p.AlimentoId == alimentoAtendido
                    && p.ChavesCasa != null && chavesAfetadas.Contains(p.ChavesCasa))
                .ToListAsync());
        }

        carga.Conteudo.UltimaTroca = carga.Referencia.UltimaTroca;
        carga.Linha.ConteudoJson = JsonSerializer.Serialize(carga.Conteudo);
        carga.Linha.Revisao++;
        try
        {
            await contexto.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new Erro(409, MensagemSemanaMudou);
        }

        return null;
    }

    // ---------- Apoio ----------

    /// <summary>A porção de um membro numa refeição da casa, antes e refeita sobre a referência nova.</summary>
    private sealed record PorcaoRefeita(string Chave, int Dia, int Refeicao, RefeicaoDto Antes, RefeicaoDto Depois, decimal AlvoKcal);

    // Só as refeições que ainda são da casa para ele: uma que virou individual (algo que ele não come) não muda.
    private static List<PorcaoRefeita> NovasPorcoes(MembroNaSemana membro, DietaSemanalDto referencia, IEnumerable<string> chaves)
    {
        var porcoes = new List<PorcaoRefeita>();
        foreach (var chave in chaves)
        {
            var refeicaoRef = referencia.Dias.SelectMany(d => d.Refeicoes).First(r => r.ChaveCasa == chave);
            for (var d = 0; d < membro.Dieta.Dias.Count; d++)
            {
                var dia = membro.Dieta.Dias[d];
                var r = dia.Refeicoes.FindIndex(x => x.DaCasa && x.ChaveCasa == chave);
                if (r < 0)
                {
                    continue;
                }

                porcoes.Add(new PorcaoRefeita(chave, d, r, dia.Refeicoes[r], PorcoesCasa.Porcao(refeicaoRef, dia.MetaCalculada),
                    PorcoesCasa.Alvo(dia.MetaCalculada, chave).Calorias));
            }
        }

        return porcoes;
    }

    // A regra dos 5% da troca, na refeição da casa de cada um: a porção refeita fica a até 5% do alvo dele, ou não
    // piora o que já estava (quando a porção máxima de um item segura a escala, por exemplo).
    private static bool ServeParaOMembro(List<PorcaoRefeita> porcoes) =>
        porcoes.All(p => CalculadoraTroca.PassaNaMeta(p.Antes.Macros.Calorias, p.Depois.Macros.Calorias, p.AlvoKcal));

    private static void AplicarNoMembro(MembroNaSemana membro, List<PorcaoRefeita> porcoes)
    {
        foreach (var p in porcoes)
        {
            membro.Dieta.Dias[p.Dia].Refeicoes[p.Refeicao] = p.Depois;
        }

        foreach (var d in porcoes.Select(p => p.Dia).Distinct())
        {
            var dia = membro.Dieta.Dias[d];
            dia.TotalDoDia = CalculoPelaBase.Somar(dia.Refeicoes.Select(r => r.Macros));
        }
    }

    // O item do dono na posição (a porção dele mantém a ordem dos itens da referência), ou nulo sem a dieta desta semana.
    private static AlimentoDto? ItemDoDono(MembroNaSemana? dono, string chave, int item) =>
        dono?.Dieta.Dias.SelectMany(d => d.Refeicoes).FirstOrDefault(r => r.DaCasa && r.ChaveCasa == chave) is { } refeicao
        && item < refeicao.Alimentos.Count
            ? refeicao.Alimentos[item]
            : null;

    private static string Chave(DietaSemanalDto referencia, PosicaoItem posicao) =>
        referencia.Dias[posicao.Dia].Refeicoes[posicao.Refeicao].ChaveCasa!;

    private static List<string> ChavesAfetadas(DietaSemanalDto referencia, IEnumerable<PosicaoItem> posicoes) =>
        posicoes.Select(p => Chave(referencia, p)).Distinct().ToList();

    private static DietaSemanalDto Clonar(DietaSemanalDto dieta) =>
        JsonSerializer.Deserialize<DietaSemanalDto>(JsonSerializer.Serialize(dieta))!;

    private static decimal NutrienteG(AlimentoBase item, decimal quantidade) => item.Nutriente switch
    {
        NutrientePrincipal.Proteina => item.ProteinaG,
        NutrientePrincipal.Carboidrato => item.CarboidratoG,
        _ => item.GorduraG
    } * quantidade / 100;

    private static string Motivo(MotivoForaDaTroca m) => m switch
    {
        MotivoForaDaTroca.UsoDiferente => "usoDiferente",
        MotivoForaDaTroca.JaNaRefeicao => "jaNaRefeicao",
        MotivoForaDaTroca.RefogadoComOleo => "refogadoComOleo",
        MotivoForaDaTroca.PorcaoMaxima => "porcaoMaxima",
        _ => "foraDaMeta"
    };

    /// <summary>Por que nenhum substituto serve, sem dizer de quem é a restrição nem para quem a porção não serve.</summary>
    public static string MensagemSemOpcoes(IReadOnlyDictionary<string, int> fora, int foraPelaCasa)
    {
        var partes = new List<string>();
        void Parte(int n, string um, string varios)
        {
            if (n > 0) partes.Add(n == 1 ? um : $"{n} {varios}");
        }

        Parte(fora.GetValueOrDefault("porcaoMaxima"), "1 passaria da porção máxima", "passariam da porção máxima");
        Parte(fora.GetValueOrDefault("foraDaMeta"), "1 mudaria demais as calorias do prato", "mudariam demais as calorias do prato");
        Parte(fora.GetValueOrDefault(MotivoNaoServeParaACasa), "1 não serve para a porção de alguém da casa", "não servem para a porção de alguém da casa");
        Parte(foraPelaCasa, "1 está no que a casa não come", "estão no que a casa não come");
        Parte(fora.GetValueOrDefault("jaNaRefeicao"), "1 já está nesta refeição", "já estão nesta refeição");
        Parte(fora.GetValueOrDefault("refogadoComOleo") + fora.GetValueOrDefault("usoDiferente"),
            "1 não combina com esta refeição", "não combinam com esta refeição");

        if (partes.Count == 0)
        {
            return "Este alimento não tem substituto entre os alimentos do Ronu.";
        }

        var lista = partes.Count == 1 ? partes[0] : string.Join(", ", partes.SkipLast(1)) + " e " + partes[^1];
        return $"Nenhum substituto serve para a casa: {lista}.";
    }
}
