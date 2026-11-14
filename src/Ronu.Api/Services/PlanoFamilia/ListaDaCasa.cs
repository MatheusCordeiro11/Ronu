using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// A lista fechada do almoço e do jantar da casa (plano família, etapa 4, decisão D2): só o que TODOS aceitam. Cada
/// membro passa pelo ponto único (ListaDaPessoa, com o "não como" e as alergias dele), e a casa fica com a interseção
/// por AlimentoId. Assim o "não como" de um tira o item para todos, e o alérgeno de um nunca chega à refeição
/// compartilhada (a lista de cada um já veio sem ele, e a regra cruzada amendoim/castanhas vale pela RegrasAlergias).
/// O "prefiro evitar" de qualquer um fica na lista como leve (no máximo 2 vezes na semana), sem dizer de quem.
///
/// O nome regional é o da UF do dono. Nada aqui guarda quem marcou o quê: a preferência leve sai sem o texto que a
/// pessoa escreveu (só os itens da base), e o que não casou com a base não vai para a casa.
/// </summary>
public static class ListaDaCasa
{
    // Almoço e jantar não levam bebida alcoólica (a regra 8 individual depende do gosto de cada um, e na casa ninguém
    // é perguntado) nem bebida com cafeína (o jantar é à noite, e a regra 7 vale para todos). Interno para as restrições
    // da casa (RestricoesDaCasa): o "não como" de um item que a casa nunca usa não é restrição da casa.
    internal static bool ForaDaCasa(AlimentoBase item) =>
        item.Grupo is GrupoDeTroca.BebidasAlcoolicas or GrupoDeTroca.CafesEChas
        || ValidadorCafeina.ComCafeina.Contains(item.Id);

    /// <param name="usuarioIds">Os membros da casa (o dono incluído).</param>
    /// <param name="donoId">De quem sai o nome regional dos itens.</param>
    public static async Task<ListaAlimentos> MontarAsync(ApplicationDbContext contexto, IReadOnlyList<int> usuarioIds, int donoId)
    {
        var listas = new List<ListaAlimentos>();
        ListaAlimentos? doDono = null;
        foreach (var id in usuarioIds)
        {
            var lista = (await ListaDaPessoa.MontarAsync(contexto, id)).Lista;
            listas.Add(lista);
            if (id == donoId)
            {
                doDono = lista;
            }
        }

        return Cruzar(doDono ?? throw new InvalidOperationException("O dono não está entre os membros da casa."), listas);
    }

    /// <summary>A interseção das listas, com os nomes da lista do dono. Pura: os testes cruzam listas montadas à mão.</summary>
    public static ListaAlimentos Cruzar(ListaAlimentos doDono, IReadOnlyList<ListaAlimentos> listas)
    {
        var emTodas = listas
            .Select(l => l.Itens.Select(i => i.Item.Id).ToHashSet())
            .Aggregate((a, b) => { a.IntersectWith(b); return a; });

        var itens = doDono.Itens.Where(i => emTodas.Contains(i.Item.Id) && !ForaDaCasa(i.Item)).ToList();
        var naCasa = itens.Select(i => i.Item.Id).ToHashSet();

        // A união das alergias: a lista já está sem elas, e a geração confere de novo a resposta contra esta união.
        var alergias = listas.Aggregate(Alergenos.Nenhum, (soma, l) => soma | l.Alergias);

        // Um "evitar" leve de qualquer um, sem o texto que a pessoa escreveu (Alimento vazio) e sem trechos sem
        // casamento: só os itens da base, que o prompt cita pelo nome da lista.
        // Um grupo por chip de cada membro (o limite da semana vale por grupo, como na dieta individual); grupos
        // iguais de membros diferentes viram um só no ListaAlimentos.GruposLeves.
        IReadOnlyList<PreferenciaCasada> preferencias = listas
            .SelectMany(l => l.GruposLeves)
            .Select(grupo => grupo.Where(naCasa.Contains).ToHashSet())
            .Where(grupo => grupo.Count > 0)
            .Select(grupo => new PreferenciaCasada(string.Empty, TiposPreferencia.Evitar, true, grupo.Count, []) { ItensIds = grupo })
            .ToList();

        return new ListaAlimentos(itens, preferencias, MinimosDaCasa(itens.Select(i => i.Item))) { Alergias = alergias };
    }

    /// <summary>
    /// Os mínimos por papel (MinimosPorPapel) sem a fruta: almoço e jantar precisam de proteína, carboidrato e
    /// hortaliça, e a sobremesa é opcional. O café e os lanches de cada um ficam com a lista de cada um.
    /// </summary>
    public static ResultadoMinimos MinimosDaCasa(IEnumerable<AlimentoBase> itens)
    {
        var minimos = MinimosPorPapel.Conferir(itens);
        return minimos with
        {
            Vazios = minimos.Vazios.Where(p => p != PapelNoPrato.Fruta).ToList(),
            AbaixoDoMinimo = minimos.AbaixoDoMinimo.Where(p => p != PapelNoPrato.Fruta).ToList()
        };
    }

    /// <summary>O 400 quando a casa não tem um papel do prato: diz o que faltou, nunca de quem é a restrição.</summary>
    public static string MensagemPapeisVazios(IReadOnlyList<PapelNoPrato> vazios)
    {
        var faltas = vazios.Select(p => p switch
        {
            PapelNoPrato.Proteina => "nenhuma fonte de proteína",
            PapelNoPrato.Carboidrato => "nenhum carboidrato (arroz, pães, massas ou tubérculos)",
            _ => "nenhuma hortaliça"
        }).ToList();
        var lista = faltas.Count == 1 ? faltas[0] : string.Join(", ", faltas.SkipLast(1)) + " e " + faltas[^1];
        return $"Com o Não como e as alergias de todos da casa juntos, não sobrou {lista} para o almoço e o jantar da casa. Cada pessoa pode rever o próprio Não como em Alimentação.";
    }
}
