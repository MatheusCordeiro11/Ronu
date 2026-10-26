using Ronu.Api.Data;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// A lista fechada de alimentos de uma pessoa (fase 2b, 2026-10-25): a base
/// nutricional com o nome regional da UF, sem o que ela marcou para evitar, sem
/// os alérgenos dela e sem bebida alcoólica que ela não pediu. É a lista que vai
/// para o prompt e para o enum do Gemini (2b-2); por enquanto nada a usa.
/// </summary>
public static class ListaAlimentosPessoa
{
    // Decisão da 2b (2026-10-25): os refogados e frituras da TACO já incluem o óleo do preparo, e o Gemini
    // costuma pôr azeite junto, o que contaria a gordura duas vezes. Saem os que têm versão sem óleo na base
    // (abobrinha cozida, ovo cozido). Couve e espinafre refogados ficam, porque não há versão sem óleo comum.
    private static readonly HashSet<string> ForaDaLista = ["abobrinha-refogada", "ovo-de-galinha-frito"];

    private static readonly AlimentoDto CervejaDaBase = new() { Nome = "Cerveja pilsen", Quantidade = 0, Unidade = "ml" };

    /// <param name="uf">Sigla da UF, para o nome regional (aipim, macaxeira); vazia usa o nome padrão.</param>
    /// <param name="alergias">Alérgenos declarados pela pessoa (a declaração vem na fase 2c).</param>
    public static ListaAlimentos Montar(string? uf, IEnumerable<PreferenciaContextoDto> preferencias, Alergenos alergias = Alergenos.Nenhum)
    {
        var prefs = preferencias.ToList();
        var base_ = BaseNutricional.Itens.Where(i => !ForaDaLista.Contains(i.Id)).ToList();

        var casadas = prefs
            .Select(p => (Preferencia: p, Resultado: CasamentoPreferencias.Casar(p.Alimento, base_)))
            .ToList();
        var evitados = casadas.Where(c => c.Preferencia.Tipo == "evitar").SelectMany(c => c.Resultado.ItensIds).ToHashSet();

        // A cerveja só entra para quem a marcou como "gosto", pela mesma regra que o
        // ValidadorBebidaAlcoolica confere na resposta (regra 8).
        var preferidos = prefs.Where(p => p.Tipo == "preferido").Select(p => p.Alimento).ToList();
        var cervejaPermitida = ValidadorBebidaAlcoolica.EncontrarNaoPermitida([CervejaDaBase], preferidos) is null;

        var itens = base_
            .Where(i => !evitados.Contains(i.Id))
            .Where(i => (i.Alergenos & alergias) == Alergenos.Nenhum)
            .Where(i => i.Categoria != CategoriaAlimento.BebidasAlcoolicas || cervejaPermitida)
            .Select(i => new ItemDaLista(i, NomeNaUf(i, uf)))
            .ToList();

        if (itens.Count > BaseNutricional.TetoDeNomes)
        {
            throw new InvalidOperationException($"A lista tem {itens.Count} nomes, acima do teto de {BaseNutricional.TetoDeNomes} do enum do Gemini.");
        }

        var resumo = casadas
            .Select(c => new PreferenciaCasada(c.Preferencia.Alimento, c.Preferencia.Tipo, c.Resultado.Casou,
                c.Resultado.ItensIds.Count, c.Resultado.TrechosSemCasamento) { ItensIds = c.Resultado.ItensIds, LidoComo = c.Resultado.LidoComo })
            .ToList();

        return new ListaAlimentos(itens, resumo, MinimosPorPapel.Conferir(itens.Select(i => i.Item)));
    }

    private static string NomeNaUf(AlimentoBase item, string? uf)
    {
        if (string.IsNullOrWhiteSpace(uf))
        {
            return item.Nome;
        }

        var sigla = uf.Trim().ToUpperInvariant();
        return item.Apelidos.FirstOrDefault(a => a.Ufs.Contains(sigla))?.Nome ?? item.Nome;
    }
}

/// <summary>Um item da lista, com o nome que vai para o Gemini (o regional, se houver).</summary>
public record ItemDaLista(AlimentoBase Item, string Nome);

/// <summary>Como cada preferência casou com a base (para o aviso na tela, na 2b-3).</summary>
public record PreferenciaCasada(string Alimento, string Tipo, bool Casou, int ItensCasados, IReadOnlyList<string> TrechosSemCasamento)
{
    /// <summary>Os itens da base que a preferência casou (o "gosto" vai assim para o prompt).</summary>
    public IReadOnlySet<string> ItensIds { get; init; } = new HashSet<string>();

    /// <summary>Como a preferência foi lida quando casou pela tolerância a 1 letra ("lactoze" -> "lactose").</summary>
    public IReadOnlyList<string> LidoComo { get; init; } = [];
}

public record ListaAlimentos(IReadOnlyList<ItemDaLista> Itens, IReadOnlyList<PreferenciaCasada> Preferencias, ResultadoMinimos Minimos);

public enum PapelNoPrato
{
    Proteina,
    Carboidrato,
    Fruta,
    Hortalica
}

/// <summary>
/// Confere se a lista ainda dá para montar uma semana depois dos filtros. Os
/// mínimos são calibração do projeto, não valor de tabela: proteína com pelo
/// menos 4 itens de pelo menos 2 grupos (a regra 3 do prompt pede proteína
/// diferente em dias seguidos, impossível com uma fonte só) e 3 itens nos outros
/// papéis. O filtro nunca devolve um item evitado para cumprir o mínimo: o que
/// fazer com o resultado (gerar com aviso, ou recusar quando um papel fica
/// vazio) é decidido na 2b-2.
/// </summary>
public static class MinimosPorPapel
{
    public const int MinimoProteina = 4;
    public const int MinimoGruposProteina = 2;
    public const int MinimoOutros = 3;

    private static readonly GrupoDeTroca[] GruposProteina =
        [GrupoDeTroca.CarnesEPeixes, GrupoDeTroca.Ovos, GrupoDeTroca.Leguminosas, GrupoDeTroca.LeiteEIogurte, GrupoDeTroca.Queijos];

    public static ResultadoMinimos Conferir(IEnumerable<AlimentoBase> itens)
    {
        var lista = itens.ToList();
        var proteinas = lista.Where(i => GruposProteina.Contains(i.Grupo)).ToList();
        var contagens = new Dictionary<PapelNoPrato, int>
        {
            [PapelNoPrato.Proteina] = proteinas.Count,
            [PapelNoPrato.Carboidrato] = lista.Count(i => i.Grupo == GrupoDeTroca.Carboidratos),
            [PapelNoPrato.Fruta] = lista.Count(i => i.Grupo == GrupoDeTroca.Frutas),
            [PapelNoPrato.Hortalica] = lista.Count(i => i.Grupo == GrupoDeTroca.Hortalicas),
        };

        var vazios = contagens.Where(c => c.Value == 0).Select(c => c.Key).ToList();
        var abaixo = contagens
            .Where(c => c.Value > 0 && c.Value < (c.Key == PapelNoPrato.Proteina ? MinimoProteina : MinimoOutros))
            .Select(c => c.Key)
            .ToList();
        var gruposProteina = proteinas.Select(i => i.Grupo).Distinct().Count();
        if (proteinas.Count > 0 && gruposProteina < MinimoGruposProteina && !abaixo.Contains(PapelNoPrato.Proteina))
        {
            abaixo.Add(PapelNoPrato.Proteina);
        }

        return new ResultadoMinimos(contagens, gruposProteina, vazios, abaixo);
    }

    /// <summary>
    /// A mensagem do 400 quando algum papel ficou vazio (DietasController): diz o
    /// que faltou e onde corrigir, sem citar as preferências da pessoa.
    /// </summary>
    public static string MensagemPapeisVazios(IReadOnlyList<PapelNoPrato> vazios)
    {
        var faltas = vazios.Select(p => p switch
        {
            PapelNoPrato.Proteina => "nenhuma fonte de proteína",
            PapelNoPrato.Carboidrato => "nenhum carboidrato (arroz, pães, massas ou tubérculos)",
            PapelNoPrato.Fruta => "nenhuma fruta",
            _ => "nenhuma hortaliça"
        }).ToList();
        var lista = faltas.Count == 1 ? faltas[0] : string.Join(", ", faltas.SkipLast(1)) + " e " + faltas[^1];
        return $"Com o que você marcou para evitar, não sobrou {lista} entre os alimentos do Ronu. Revise a lista de Evitar em Alimentação.";
    }
}

public record ResultadoMinimos(
    IReadOnlyDictionary<PapelNoPrato, int> ItensPorPapel,
    int GruposDeProteina,
    IReadOnlyList<PapelNoPrato> Vazios,
    IReadOnlyList<PapelNoPrato> AbaixoDoMinimo)
{
    public bool Atende => Vazios.Count == 0 && AbaixoDoMinimo.Count == 0;
}
