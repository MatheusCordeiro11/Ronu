using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.Troca;

/// <summary>
/// Os substitutos de um item da dieta atual (feature "trocar alimento", etapa 1), calculados só em C#, pela base,
/// sem chamar o Gemini. Regras aprovadas no passo 0 (2026-10-26):
///
/// - Quem pode: itens do mesmo GrupoDeTroca que estão na lista fechada da pessoa (ListaAlimentosPessoa, com as
///   preferências ATUAIS: um "não como" novo vale já na troca), menos o que já está na mesma refeição, o refogado
///   numa refeição com azeite ou óleo (e vice-versa: a TACO já conta o óleo do refogado) e, nas gorduras, o item
///   de outro uso (azeite não substitui manteiga). O "evitar" leve (etapa 3) não tira o item: ele pode ser
///   oferecido, mas vem depois dos outros na ordem.
/// - Quantidade: a que mantém o nutriente principal do original, com as regras de arredondamento da fase 4
///   (contáveis em unidade inteira, gramas em múltiplos legíveis). Por isso o nutriente fica quase igual, e o
///   valor real vai na resposta. Itens sem nutriente principal (hortaliças, café, chás) mantêm o peso ou o volume,
///   limitado à porção máxima do substituto.
/// - Fora: substituto acima da PorcaoMaximaTroca, e o que deixa algum dia afetado mais de 5% fora da meta. Um dia
///   que já estava fora aceita a troca que não piora o desvio.
/// - Só o item trocado muda: os outros alimentos do dia não são reajustados.
///
/// No escopo "semana", vale cada vez que o mesmo AlimentoId aparece, cada uma com a quantidade recalculada a
/// partir da sua, e a regra dos 5% vale para cada dia afetado.
/// </summary>
public static class CalculadoraTroca
{
    // Calibração do projeto: a mesma margem com que a fase 4 conta um dia "na meta".
    public const decimal MargemDoDia = 0.05m;

    // Até 3 das preferências (no máximo um item por gosto) e até 3 parecidos; o resto que passa nas regras vai
    // para o "ver mais".
    public const int OpcoesPorSecao = 3;

    // Teto só para o Legivel: a porção máxima é conferida depois, e uma quantidade acima dela tira a opção.
    private const decimal SemTeto = 100_000m;

    private static readonly IReadOnlyDictionary<string, AlimentoBase> PorId = BaseNutricional.Itens.ToDictionary(i => i.Id);

    /// <summary>
    /// Por que uma dieta não tem troca (nulo quando tem): sem AlimentoId (anterior à lista fechada), ou calculada
    /// com outra versão da base, cujos valores não podem se misturar com os de agora.
    /// </summary>
    public static MotivoSemTroca? MotivoSemTroca(DietaSemanalDto dieta)
    {
        var itens = dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos).ToList();
        if (itens.Any(a => a.AlimentoId is null || !PorId.ContainsKey(a.AlimentoId)))
        {
            return Troca.MotivoSemTroca.DietaAnterior;
        }

        return dieta.VersaoBase == BaseNutricional.VersaoBase ? null : Troca.MotivoSemTroca.OutraVersaoDaBase;
    }

    /// <summary>O item na posição, ou nulo se a posição não existe na dieta.</summary>
    public static AlimentoDto? Encontrar(IReadOnlyList<DiaDietaDto> dias, PosicaoItem posicao)
    {
        if (posicao.Dia < 0 || posicao.Dia >= dias.Count)
        {
            return null;
        }

        var refeicoes = dias[posicao.Dia].Refeicoes;
        if (posicao.Refeicao < 0 || posicao.Refeicao >= refeicoes.Count)
        {
            return null;
        }

        var alimentos = refeicoes[posicao.Refeicao].Alimentos;
        return posicao.Item < 0 || posicao.Item >= alimentos.Count ? null : alimentos[posicao.Item];
    }

    /// <summary>
    /// Os substitutos do item na posição, nos dois escopos. A posição precisa existir e a dieta precisa ter troca
    /// (MotivoSemTroca nulo): quem chama confere antes.
    /// </summary>
    public static ResultadoOpcoesTroca Calcular(IReadOnlyList<DiaDietaDto> dias, PosicaoItem posicao, ListaAlimentos lista)
    {
        var original = Encontrar(dias, posicao) ?? throw new ArgumentOutOfRangeException(nameof(posicao));
        if (original.AlimentoId is null || !PorId.TryGetValue(original.AlimentoId, out var baseOriginal))
        {
            throw new InvalidOperationException("Item sem AlimentoId da base: a dieta não tem troca.");
        }

        // Plano família (etapa 5): o "na semana toda" não cruza a fronteira da casa. Na dieta de quem está numa casa,
        // trocar o arroz do café não pode mudar o arroz do almoço da casa (só o dono troca ali, para todos); na semana
        // da casa, todas as refeições são da casa, então vale para todas.
        var daCasa = dias[posicao.Dia].Refeicoes[posicao.Refeicao].DaCasa;
        var naSemana = Posicoes(dias)
            .Where(p => Encontrar(dias, p)!.AlimentoId == baseOriginal.Id && dias[p.Dia].Refeicoes[p.Refeicao].DaCasa == daCasa)
            .ToList();

        // De cada item preferido, os gostos (índices em lista.Preferencias) que o casaram.
        var preferidos = lista.Preferencias
            .Select((p, indice) => (p, indice))
            .Where(x => x.p.Tipo == TiposPreferencia.Preferido)
            .SelectMany(x => x.p.ItensIds.Select(id => (id, x.indice)))
            .GroupBy(x => x.id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.indice).ToList());
        // Só o "nao_como" saiu da lista; o "evitar" leve continua nela e só muda a ordem (Escopo).
        var naoComo = lista.Preferencias.Where(p => p.Tipo == TiposPreferencia.NaoComo).SelectMany(p => p.ItensIds).ToHashSet();
        var leves = lista.ItensLeves;
        var candidatos = lista.Itens.Where(i => i.Item.Grupo == baseOriginal.Grupo && i.Item.Id != baseOriginal.Id).ToList();
        var naLista = candidatos.Select(c => c.Item.Id).ToHashSet();
        // TACO 2c: o que saiu por alergia conta só como alergia, mesmo que também esteja no "não como". A alergia é o
        // motivo mais forte, e a mensagem não pode mandar a pessoa rever o "não como" por um item que nunca voltaria.
        bool DoGrupo(AlimentoBase i) => i.Grupo == baseOriginal.Grupo && i.Id != baseOriginal.Id;
        var foraPelaAlergia = BaseNutricional.Itens.Count(i => DoGrupo(i) && RegrasAlergias.Tira(i, lista.Alergias));
        var foraPeloEvitar = BaseNutricional.Itens.Count(i =>
            DoGrupo(i) && naoComo.Contains(i.Id) && !naLista.Contains(i.Id) && !RegrasAlergias.Tira(i, lista.Alergias));

        return new ResultadoOpcoesTroca(
            original, baseOriginal, naSemana.Count, foraPeloEvitar,
            Escopo(dias, [posicao], baseOriginal, candidatos, preferidos, leves),
            Escopo(dias, naSemana, baseOriginal, candidatos, preferidos, leves))
        {
            ForaPelaAlergia = foraPelaAlergia
        };
    }

    /// <summary>
    /// O melhor substituto para um item da dieta, na ordem da tela da troca (das preferências, parecidos, ver mais),
    /// só na refeição, sem os itens de "excluir". Nulo quando nenhum passa nas regras. Usado pelo gerador para trocar
    /// as ocorrências de um item leve acima do limite da semana (etapa 3): ali "excluir" são os itens leves, para a
    /// troca não pôr outro item que a pessoa prefere evitar no lugar.
    /// </summary>
    public static OpcaoTroca? MelhorSubstituto(IReadOnlyList<DiaDietaDto> dias, PosicaoItem posicao, ListaAlimentos lista, IReadOnlySet<string> excluir)
    {
        var escopo = Calcular(dias, posicao, lista).Refeicao;
        return escopo.DasPreferencias.Concat(escopo.Parecidos).Concat(escopo.VerMais)
            .FirstOrDefault(o => !excluir.Contains(o.AlimentoId));
    }

    /// <summary>
    /// A quantidade do substituto no lugar de uma quantidade do original, e a contagem quando o substituto é
    /// contável. Nulo quando o substituto não tem o nutriente a manter (não acontece na base de hoje).
    /// </summary>
    public static (decimal Quantidade, int? Contagem)? Quantidade(AlimentoBase original, decimal quantidadeOriginal, AlimentoBase substituto)
    {
        var livre = original.Nutriente == NutrientePrincipal.Livre;
        decimal alvo;
        if (livre)
        {
            alvo = quantidadeOriginal;
        }
        else
        {
            var porGramaSubstituto = Nutriente(substituto, original.Nutriente);
            if (porGramaSubstituto <= 0)
            {
                return null;
            }

            alvo = quantidadeOriginal * Nutriente(original, original.Nutriente) / porGramaSubstituto;
        }

        // Contáveis como na fase 4: unidade inteira mais próxima, pelo menos 1.
        var unidade = substituto.MedidaUnidade?.Gramas;
        if (unidade is not null)
        {
            var quantas = Math.Max(1, Math.Round(alvo / unidade.Value, MidpointRounding.AwayFromZero));
            if (livre)
            {
                quantas = Math.Min(quantas, Math.Max(1, Math.Floor(substituto.PorcaoMaximaTroca / unidade.Value)));
            }

            return (quantas * unidade.Value, (int)quantas);
        }

        var gramas = AjusteQuantidades.Legivel(alvo, 0, SemTeto);
        return (livre ? Math.Min(gramas, substituto.PorcaoMaximaTroca) : gramas, null);
    }

    private static OpcoesDoEscopo Escopo(
        IReadOnlyList<DiaDietaDto> dias, List<PosicaoItem> posicoes, AlimentoBase baseOriginal,
        List<ItemDaLista> candidatos, Dictionary<string, List<int>> preferidos, IReadOnlySet<string> leves)
    {
        var opcoes = candidatos
            .Select(c => Opcao(dias, posicoes, baseOriginal, c, preferidos.ContainsKey(c.Item.Id)))
            .OfType<OpcaoTroca>()
            .ToList();

        // Primeiro o mesmo uso (só muda a ordem nos carboidratos: nas gorduras, o outro uso já ficou de fora),
        // depois o que a pessoa não marcou em "Prefiro evitar" (etapa 3: o leve pode ser oferecido, mas por último),
        // depois o que menos muda as kcal.
        var disponiveis = opcoes
            .Where(o => o.Disponivel)
            .OrderByDescending(o => o.MesmoUso)
            .ThenBy(o => leves.Contains(o.AlimentoId))
            .ThenBy(o => Math.Abs(o.DifKcal))
            .ThenBy(o => o.Nome, StringComparer.Ordinal)
            .ToList();

        // Um item por gosto, o primeiro na ordem acima (o que menos muda as kcal). Um item que casou com um gosto já
        // representado também fica de fora; os de fora seguem para os parecidos e o "ver mais".
        var gostosUsados = new HashSet<int>();
        var dasPreferencias = new List<OpcaoTroca>();
        foreach (var opcao in disponiveis.Where(o => o.Preferido))
        {
            var gostos = preferidos[opcao.AlimentoId];
            if (dasPreferencias.Count == OpcoesPorSecao || gostos.Any(gostosUsados.Contains))
            {
                continue;
            }

            dasPreferencias.Add(opcao);
            gostosUsados.UnionWith(gostos);
        }

        var parecidos = disponiveis.Except(dasPreferencias).Take(OpcoesPorSecao).ToList();
        var verMais = disponiveis.Except(dasPreferencias).Except(parecidos).ToList();

        var fora = opcoes
            .Where(o => o.Motivo is not null)
            .GroupBy(o => o.Motivo!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        return new OpcoesDoEscopo(dasPreferencias, parecidos, verMais, fora);
    }

    private static OpcaoTroca? Opcao(
        IReadOnlyList<DiaDietaDto> dias, List<PosicaoItem> posicoes, AlimentoBase baseOriginal, ItemDaLista candidato, bool preferido)
    {
        var substituto = candidato.Item;
        var mesmoUso = UsoCombina(baseOriginal.Uso, substituto.Uso);
        MotivoForaDaTroca? motivo = baseOriginal.Grupo == GrupoDeTroca.OleosEGorduras && !mesmoUso
            ? MotivoForaDaTroca.UsoDiferente
            : null;

        var ocorrencias = new List<OcorrenciaTroca>();
        foreach (var posicao in posicoes)
        {
            var atual = Encontrar(dias, posicao)!;
            var calculada = Quantidade(baseOriginal, atual.Quantidade, substituto);
            if (calculada is null)
            {
                return null;
            }

            var (quantidade, contagem) = calculada.Value;
            var outros = dias[posicao.Dia].Refeicoes[posicao.Refeicao].Alimentos
                .Where((_, indice) => indice != posicao.Item)
                .Select(a => a.AlimentoId)
                .ToList();

            if (outros.Contains(substituto.Id))
            {
                motivo ??= MotivoForaDaTroca.JaNaRefeicao;
            }
            else if ((GeradorDietaGemini.Refogados.Contains(substituto.Id) && outros.Any(GeradorDietaGemini.OleosDoPreparo.Contains))
                     || (GeradorDietaGemini.OleosDoPreparo.Contains(substituto.Id) && outros.Any(GeradorDietaGemini.Refogados.Contains)))
            {
                motivo ??= MotivoForaDaTroca.RefogadoComOleo;
            }
            else if (quantidade > substituto.PorcaoMaximaTroca)
            {
                motivo ??= MotivoForaDaTroca.PorcaoMaxima;
            }

            var nutriente = baseOriginal.Nutriente;
            ocorrencias.Add(new OcorrenciaTroca(
                posicao, atual.Quantidade, Kcal(baseOriginal, atual.Quantidade),
                quantidade, contagem, RotuloContavel.Nome(candidato.Nome, substituto, quantidade), Kcal(substituto, quantidade),
                nutriente == NutrientePrincipal.Livre ? null : Nutriente(baseOriginal, nutriente) * atual.Quantidade / 100,
                nutriente == NutrientePrincipal.Livre ? null : Nutriente(substituto, nutriente) * quantidade / 100));
        }

        var previas = ocorrencias
            .GroupBy(o => o.Posicao.Dia)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var dia = dias[g.Key];
                var antes = dia.TotalDoDia.Calorias;
                return new PreviaDia(g.Key, dia.DiaSemana, antes, antes + g.Sum(o => o.DifKcal), dia.MetaCalculada.Calorias);
            })
            .ToList();

        if (previas.Any(p => !p.Passa))
        {
            motivo ??= MotivoForaDaTroca.ForaDaMeta;
        }

        return new OpcaoTroca(substituto.Id, candidato.Nome, preferido, mesmoUso, ocorrencias, previas, motivo);
    }

    // PratoECafe combina com os dois; sem uso marcado, combina com tudo.
    private static bool UsoCombina(UsoNaTroca? a, UsoNaTroca? b) =>
        a is null || b is null || a == b
        || (a == UsoNaTroca.PratoECafe && b is UsoNaTroca.Prato or UsoNaTroca.CafeELanche)
        || (b == UsoNaTroca.PratoECafe && a is UsoNaTroca.Prato or UsoNaTroca.CafeELanche);

    private static IEnumerable<PosicaoItem> Posicoes(IReadOnlyList<DiaDietaDto> dias) =>
        dias.SelectMany((dia, d) => dia.Refeicoes.SelectMany((refeicao, r) =>
            refeicao.Alimentos.Select((_, i) => new PosicaoItem(d, r, i))));

    private static decimal Kcal(AlimentoBase item, decimal quantidade) => item.Kcal * quantidade / 100;

    private static decimal Nutriente(AlimentoBase item, NutrientePrincipal nutriente) => nutriente switch
    {
        NutrientePrincipal.Proteina => item.ProteinaG,
        NutrientePrincipal.Carboidrato => item.CarboidratoG,
        NutrientePrincipal.Gordura => item.GorduraG,
        _ => 0
    };

    /// <summary>A margem dos 5%: passa se o dia fica a até 5% da meta, ou se não piora o desvio que já tinha.</summary>
    public static bool PassaNaMeta(decimal antes, decimal depois, decimal meta)
    {
        if (meta <= 0)
        {
            return true;
        }

        var desvioAntes = Math.Abs(antes - meta) / meta;
        var desvioDepois = Math.Abs(depois - meta) / meta;
        return desvioDepois <= MargemDoDia || desvioDepois <= desvioAntes;
    }
}

/// <summary>Um item da dieta: índices do dia (na ordem da dieta salva), da refeição e do alimento.</summary>
public record PosicaoItem(int Dia, int Refeicao, int Item);

public enum MotivoSemTroca
{
    /// <summary>Dieta anterior à lista fechada (itens sem AlimentoId, números declarados pelo Gemini).</summary>
    DietaAnterior,

    /// <summary>Dieta calculada com outra versão da base: só uma dieta nova tem troca.</summary>
    OutraVersaoDaBase
}

public enum MotivoForaDaTroca
{
    UsoDiferente,
    JaNaRefeicao,
    RefogadoComOleo,
    PorcaoMaxima,
    ForaDaMeta
}

/// <summary>Uma vez em que o item aparece, antes e depois da troca. As kcal antes são as da base.</summary>
public record OcorrenciaTroca(
    PosicaoItem Posicao,
    decimal QuantidadeAntes,
    decimal KcalAntes,
    decimal Quantidade,
    int? Contagem,
    string Nome,
    decimal Kcal,
    decimal? NutrienteAntesG,
    decimal? NutrienteG)
{
    public decimal DifKcal => Kcal - KcalAntes;
}

/// <summary>O total de kcal de um dia afetado, antes e depois da troca, contra a MetaCalculada.</summary>
public record PreviaDia(int Dia, string DiaSemana, decimal TotalAntes, decimal TotalDepois, decimal Meta)
{
    public decimal DesvioPct => Meta == 0 ? 0 : (TotalDepois - Meta) / Meta * 100;
    public bool Passa => CalculadoraTroca.PassaNaMeta(TotalAntes, TotalDepois, Meta);
}

/// <summary>Um substituto num escopo. Nome é o regional, sem contagem (a contagem está em cada ocorrência).</summary>
public record OpcaoTroca(
    string AlimentoId,
    string Nome,
    bool Preferido,
    bool MesmoUso,
    IReadOnlyList<OcorrenciaTroca> Ocorrencias,
    IReadOnlyList<PreviaDia> Dias,
    MotivoForaDaTroca? Motivo)
{
    public bool Disponivel => Motivo is null;
    public decimal DifKcal => Ocorrencias.Sum(o => o.DifKcal);
}

/// <summary>As opções de um escopo, já separadas nas seções da tela, e quantas ficaram de fora por motivo.</summary>
public record OpcoesDoEscopo(
    IReadOnlyList<OpcaoTroca> DasPreferencias,
    IReadOnlyList<OpcaoTroca> Parecidos,
    IReadOnlyList<OpcaoTroca> VerMais,
    IReadOnlyDictionary<MotivoForaDaTroca, int> Fora)
{
    public bool Nenhuma => DasPreferencias.Count == 0 && Parecidos.Count == 0 && VerMais.Count == 0;
}

/// <summary>
/// O resultado para um item: o original, quantas vezes ele aparece na semana, quantos do grupo saíram pelo
/// "não como" (não entram em nenhum escopo) e as opções de cada escopo. O nome ForaPeloEvitar ficou de antes da
/// etapa 3, quando o bloqueio se chamava "evitar": a API mantém o campo foraPeloEvitar para não quebrar a tela.
/// </summary>
public record ResultadoOpcoesTroca(
    AlimentoDto Original,
    AlimentoBase Base,
    int OcorrenciasNaSemana,
    int ForaPeloEvitar,
    OpcoesDoEscopo Refeicao,
    OpcoesDoEscopo Semana)
{
    /// <summary>Quantos do grupo saíram pelas alergias da pessoa (TACO 2c); não entram no ForaPeloEvitar.</summary>
    public int ForaPelaAlergia { get; init; }
}
