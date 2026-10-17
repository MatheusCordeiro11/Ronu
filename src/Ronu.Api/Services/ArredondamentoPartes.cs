namespace Ronu.Api.Services;

/// <summary>
/// Arredonda parcelas em kcal inteiras de modo que as partes mostradas na tela
/// sempre somem exatamente o total mostrado (decisão de 2026-10-16: nunca
/// aparecer diferença de 1 kcal entre a soma e o total). Arredondar cada parte
/// sozinha não garante isso: 0,5 + 0,5 dá 1, mas arredondadas dão 1 + 1 = 2.
/// </summary>
public static class ArredondamentoPartes
{
    /// <summary>Arredondamento comum da tela: meio para longe do zero.</summary>
    public static int Arredondar(decimal valor) =>
        (int)Math.Round(valor, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Método do maior resto: cada parte vai para o piso e as unidades que
    /// faltam para chegar ao total arredondado vão para as partes com a maior
    /// fração. Partes já inteiras (o "+5" do sexo na TMB, por exemplo) nunca
    /// mudam, porque a falta nunca passa do número de partes com fração.
    /// Usado quando nenhuma parte é "a última" (as parcelas da TMB).
    /// </summary>
    public static int[] MaiorResto(IReadOnlyList<decimal> partes)
    {
        var total = Arredondar(partes.Sum());
        var pisos = partes.Select(p => (int)Math.Floor(p)).ToArray();
        var falta = total - pisos.Sum();

        var ordem = Enumerable.Range(0, partes.Count)
            .OrderByDescending(i => partes[i] - Math.Floor(partes[i]))
            .ThenBy(i => i)
            .ToList();

        for (var k = 0; k < falta; k++)
        {
            pisos[ordem[k]]++;
        }

        return pisos;
    }

    /// <summary>
    /// Cada parte arredondada sozinha e a diferença para o total jogada na
    /// última parte diferente de zero. Usado quando as primeiras partes se
    /// repetem em várias telas (gasto em repouso, dia a dia, treino) e
    /// precisam mostrar sempre o mesmo número; a diferença cai no fim da conta
    /// (objetivo, ajuste ou piso), que é específico daquele dia.
    /// </summary>
    public static int[] AjustandoUltima(IReadOnlyList<decimal> partes, int total)
    {
        var arredondadas = partes.Select(Arredondar).ToArray();
        var diferenca = total - arredondadas.Sum();

        if (diferenca != 0 && arredondadas.Length > 0)
        {
            var ultima = Array.FindLastIndex(arredondadas, p => p != 0);
            arredondadas[ultima < 0 ? arredondadas.Length - 1 : ultima] += diferenca;
        }

        return arredondadas;
    }
}
