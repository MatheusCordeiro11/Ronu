using Ronu.Api.Data;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Quais marcas da base cada alergia declarada tira (fase 2c, 2026-11-13). É o
/// único lugar com a regra cruzada, para ela poder mudar sem caçar chamadas.
/// </summary>
public static class RegrasAlergias
{
    // Decisão do projeto, aprovada em 2026-11-13: amendoim e castanhas vão juntos nos dois sentidos, por segurança.
    // O beneficiamento costuma ser compartilhado ("pode conter amendoim" nas castanhas) e há reação cruzada em parte
    // das pessoas. Se um nutricionista orientar separar, basta tirar esta linha.
    private const Alergenos FamiliaAmendoimCastanhas = Alergenos.Amendoim | Alergenos.Castanhas;

    /// <summary>As marcas que saem da lista para quem declarou estas alergias.</summary>
    public static Alergenos MarcasQueTira(Alergenos alergias) =>
        (alergias & FamiliaAmendoimCastanhas) != Alergenos.Nenhum ? alergias | FamiliaAmendoimCastanhas : alergias;

    /// <summary>Se o item tem alguma marca que estas alergias tiram.</summary>
    public static bool Tira(AlimentoBase item, Alergenos alergias) =>
        (item.Alergenos & MarcasQueTira(alergias)) != Alergenos.Nenhum;
}
