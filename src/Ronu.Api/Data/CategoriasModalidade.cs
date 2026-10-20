namespace Ronu.Api.Data;

/// <summary>
/// Grupos do catálogo de modalidades ("Modalidades"."Categoria"), usados só
/// para agrupar na tela (onboarding e Treino). Não entram em nenhuma conta.
/// A ordem de <see cref="Ordem"/> é a ordem dos grupos no GET /api/modalidades.
/// </summary>
public static class CategoriasModalidade
{
    public const string Lutas = "Lutas";
    public const string Esportes = "Esportes";
    public const string Condicionamento = "Condicionamento";
    public const string CorpoEMovimento = "Corpo e movimento";

    public static readonly IReadOnlyList<string> Ordem = [Lutas, Esportes, Condicionamento, CorpoEMovimento];

    /// <summary>Posição do grupo na tela; categoria desconhecida vai para o fim.</summary>
    public static int Posicao(string categoria)
    {
        var indice = Ordem.ToList().IndexOf(categoria);
        return indice < 0 ? Ordem.Count : indice;
    }
}
