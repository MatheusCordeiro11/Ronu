using Ronu.Api.Data;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Os 8 chips de alergia (TACO 2c, aprovados em 2026-11-13) e a marca da base que cada um representa. A lista segue o
/// Anexo III da RDC ANVISA 727/2022 no que existe na base: os itens 8 a 17 (castanhas e nozes) viram um chip só, e o
/// látex fica de fora por não ser alimento. Só estes códigos são aceitos pela API, e a ordem é a da tela.
/// </summary>
public static class CodigosAlergia
{
    public record Chip(string Codigo, Alergenos Marca, string Rotulo);

    public static readonly IReadOnlyList<Chip> Chips =
    [
        new("leite", Alergenos.Lactose, "Leite e derivados"),
        new("gluten", Alergenos.Gluten, "Glúten"),
        new("ovo", Alergenos.Ovo, "Ovo"),
        new("amendoim", Alergenos.Amendoim, "Amendoim"),
        new("castanhas", Alergenos.Castanhas, "Castanhas"),
        new("soja", Alergenos.Soja, "Soja"),
        new("peixe", Alergenos.Peixe, "Peixe"),
        new("crustaceos", Alergenos.Crustaceo, "Crustáceos e frutos do mar"),
    ];

    private static readonly IReadOnlyDictionary<string, Chip> PorCodigo = Chips.ToDictionary(c => c.Codigo, StringComparer.Ordinal);

    // Sem normalização (como o tipo da preferência): o código vem de botões fixos da tela, e aceitar "Soja" ou " soja"
    // só esconderia um erro do cliente.
    public static bool Valido(string? codigo) => codigo is not null && PorCodigo.ContainsKey(codigo);

    public static Chip DoCodigo(string codigo) => PorCodigo[codigo];

    /// <summary>As marcas da base dos códigos marcados (códigos desconhecidos, que não deveriam existir, são ignorados).</summary>
    public static Alergenos Marcas(IEnumerable<string> codigos) =>
        codigos.Where(Valido).Aggregate(Alergenos.Nenhum, (soma, c) => soma | PorCodigo[c].Marca);

    /// <summary>
    /// Os chips marcados que tiram este item, na ordem da tela. Pela regra cruzada (RegrasAlergias), a castanha sai
    /// também para quem marcou só Amendoim: o chip citado é o que a pessoa marcou, não a marca do item.
    /// </summary>
    public static IReadOnlyList<Chip> ChipsQueTiram(AlimentoBase item, IEnumerable<string> codigosMarcados)
    {
        var marcados = codigosMarcados.ToHashSet(StringComparer.Ordinal);
        return Chips.Where(c => marcados.Contains(c.Codigo) && RegrasAlergias.Tira(item, c.Marca)).ToList();
    }
}
