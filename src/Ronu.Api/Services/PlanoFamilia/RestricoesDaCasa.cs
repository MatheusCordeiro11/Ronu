using System.Globalization;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// "Nesta casa, não entra" e "Evitamos quando dá" (shape S2): as restrições de todos os membros, juntas, para qualquer
/// membro ver. Dizer "alguém tem alergia a amendoim" já é dado de saúde de alguém, então a resposta é montada para não
/// dizer nada além do que sai do cardápio:
///
/// - Só nomes da base nutricional: o nome do item (o regional da UF do dono, o mesmo do cardápio da casa) ou, quando
///   todos os itens de uma categoria saem, o nome da categoria ("Peixes"). Nunca o texto que a pessoa escreveu (ele
///   pode ser "tenho alergia a camarão") e nunca o nome de um chip de alergia. O que não casou com a base fica de
///   fora, como fica de fora da lista da casa (ListaDaCasa).
/// - O "não como" e as alergias viram uma lista só. A tabela do casamento (CasamentoPreferencias) tira, para "leite",
///   "ovo", "soja", "glúten" e "amendoim", os mesmos itens que o chip; então a lista não tem como dizer se veio de um
///   chip ou de um texto.
/// - Ordem alfabética (pt-BR) e sem repetição: a ordem não segue membro, chip nem data de cadastro.
/// - Igual para todos os membros, dono incluído: nada depende de quem pergunta.
///
/// Os itens que a casa nunca usa (bebida alcoólica, café e chás, os refogados com versão sem óleo) não entram: não são
/// restrição de ninguém.
/// </summary>
public static class RestricoesDaCasa
{
    private static readonly StringComparer OrdemPtBr = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);

    // O nome da categoria quando ela sai inteira. Mais curto que listar cada corte, e continua sendo só alimento.
    private static readonly IReadOnlyDictionary<CategoriaAlimento, string> NomesCategorias = new Dictionary<CategoriaAlimento, string>
    {
        [CategoriaAlimento.CereaisEMassas] = "Cereais e massas",
        [CategoriaAlimento.Paes] = "Pães",
        [CategoriaAlimento.Leguminosas] = "Leguminosas",
        [CategoriaAlimento.TuberculosERaizes] = "Tubérculos e raízes",
        [CategoriaAlimento.Hortalicas] = "Hortaliças",
        [CategoriaAlimento.Frutas] = "Frutas",
        [CategoriaAlimento.CarneBovina] = "Carne bovina",
        [CategoriaAlimento.Aves] = "Aves",
        [CategoriaAlimento.CarneSuina] = "Carne suína",
        [CategoriaAlimento.Peixes] = "Peixes",
        [CategoriaAlimento.Ovos] = "Ovos",
        [CategoriaAlimento.Laticinios] = "Laticínios",
        [CategoriaAlimento.OleaginosasESementes] = "Oleaginosas e sementes",
        [CategoriaAlimento.OleosEGorduras] = "Óleos e gorduras",
        [CategoriaAlimento.AcucaresEMel] = "Açúcares e mel",
        [CategoriaAlimento.Bebidas] = "Bebidas",
        [CategoriaAlimento.BebidasAlcoolicas] = "Bebidas alcoólicas"
    };

    // Os itens que podem estar no almoço e no jantar da casa: os únicos em que uma restrição muda alguma coisa.
    private static readonly IReadOnlyList<AlimentoBase> Candidatos = BaseNutricional.Itens
        .Where(i => !ListaAlimentosPessoa.ForaDaLista.Contains(i.Id) && !ListaDaCasa.ForaDaCasa(i))
        .ToList();

    /// <summary>As restrições da casa, lidas pelo ponto único de cada membro (ListaDaPessoa).</summary>
    public static async Task<RestricoesCasaResponse> MontarAsync(ApplicationDbContext contexto, IReadOnlyList<int> usuarioIds, int? donoId)
    {
        var listas = new List<ListaAlimentos>();
        string? ufDoDono = null;
        foreach (var id in usuarioIds)
        {
            var resultado = await ListaDaPessoa.MontarAsync(contexto, id);
            listas.Add(resultado.Lista);
            if (id == donoId)
            {
                ufDoDono = resultado.Uf;
            }
        }

        return Montar(listas, ufDoDono);
    }

    /// <summary>Pura: os testes passam listas montadas à mão, em qualquer ordem.</summary>
    public static RestricoesCasaResponse Montar(IReadOnlyList<ListaAlimentos> listas, string? ufDoDono)
    {
        var candidatos = Candidatos.Select(i => i.Id).ToHashSet();

        // Só os ItensIds do casamento: o Alimento (o texto da pessoa) e os trechos sem casamento nem são lidos.
        var naoEntra = listas
            .SelectMany(l => l.Preferencias.Where(p => p.Tipo == TiposPreferencia.NaoComo).SelectMany(p => p.ItensIds)
                .Concat(Candidatos.Where(i => RegrasAlergias.Tira(i, l.Alergias)).Select(i => i.Id)))
            .Where(candidatos.Contains)
            .ToHashSet();

        // Um item que alguém não come já não entra: não aparece de novo como "evitamos".
        var evitamos = listas
            .SelectMany(l => l.Preferencias.Where(p => p.Tipo == TiposPreferencia.Evitar).SelectMany(p => p.ItensIds))
            .Where(id => candidatos.Contains(id) && !naoEntra.Contains(id))
            .ToHashSet();

        return new RestricoesCasaResponse
        {
            NaoEntra = Nomes(naoEntra, ufDoDono),
            EvitamosQuandoDa = Nomes(evitamos, ufDoDono)
        };
    }

    // A categoria inteira vira um nome só (se tem mais de um item); o resto, o nome de cada item.
    private static List<string> Nomes(IReadOnlySet<string> ids, string? uf)
    {
        var nomes = new HashSet<string>(OrdemPtBr);
        foreach (var categoria in Candidatos.GroupBy(i => i.Categoria))
        {
            var dentro = categoria.Where(i => ids.Contains(i.Id)).ToList();
            if (dentro.Count == 0)
            {
                continue;
            }

            if (dentro.Count == categoria.Count() && dentro.Count > 1 && NomesCategorias.TryGetValue(categoria.Key, out var nomeCategoria))
            {
                nomes.Add(nomeCategoria);
                continue;
            }

            foreach (var item in dentro)
            {
                nomes.Add(ListaAlimentosPessoa.NomeNaUf(item, uf));
            }
        }

        return nomes.Order(OrdemPtBr).ToList();
    }
}
