using Ronu.Api.Data;

namespace Ronu.Api.Services.IA;

/// <summary>
/// A contagem no nome dos itens contáveis ("Ovo de galinha cozido (2 unidades)"), pelo peso da unidade da POF.
/// Usada na geração (GeradorDietaGemini) e na troca de alimento, para o substituto sair com o mesmo rótulo.
/// </summary>
public static class RotuloContavel
{
    // Folga para considerar a quantidade um múltiplo exato do peso da unidade (arredondamento do Gemini).
    public const decimal ToleranciaMultiploG = 0.5m;

    /// <summary>Quantas unidades a quantidade tem, se for múltiplo exato do peso da unidade; nulo nos outros casos.</summary>
    public static int? Contagem(AlimentoBase item, decimal quantidade)
    {
        var unidade = item.MedidaUnidade;
        if (unidade is null)
        {
            return null;
        }

        var quantas = Math.Round(quantidade / unidade.Gramas);
        return quantas >= 1 && Math.Abs(quantidade - quantas * unidade.Gramas) <= ToleranciaMultiploG ? (int)quantas : null;
    }

    /// <summary>O nome com a contagem, quando houver; o nome como veio, quando não.</summary>
    public static string Nome(string nome, AlimentoBase item, decimal quantidade)
    {
        var quantas = Contagem(item, quantidade);
        if (quantas is null)
        {
            return nome;
        }

        var unidade = item.MedidaUnidade!;
        return $"{nome} ({quantas} {(quantas == 1 ? unidade.Nome : unidade.NomePlural)})";
    }
}
