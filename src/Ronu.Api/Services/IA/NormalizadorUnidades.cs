using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Padroniza a unidade de cada alimento da resposta do Gemini para "g" ou "ml"
/// (as únicas permitidas pela regra 10 do prompt), para que cada alimento seja
/// calculável contra uma tabela nutricional (TACO, por 100 g). Sinônimos viram
/// a forma curta ("gramas" -> "g") e kg/litro são convertidos. Qualquer outra
/// unidade ("unidades", "fatia", "colher") fica como veio: a dieta não é
/// rejeitada por isso, só registrada no log da geração.
/// </summary>
public static class NormalizadorUnidades
{
    public const string Gramas = "g";
    public const string Mililitros = "ml";

    // Unidade escrita (minúscula, sem espaços nas pontas) -> (unidade padrão, fator).
    private static readonly Dictionary<string, (string Unidade, decimal Fator)> Sinonimos = new()
    {
        ["g"] = (Gramas, 1), ["g."] = (Gramas, 1), ["gr"] = (Gramas, 1), ["grs"] = (Gramas, 1),
        ["grama"] = (Gramas, 1), ["gramas"] = (Gramas, 1),
        ["kg"] = (Gramas, 1000), ["quilo"] = (Gramas, 1000), ["quilos"] = (Gramas, 1000),
        ["quilograma"] = (Gramas, 1000), ["quilogramas"] = (Gramas, 1000),
        ["ml"] = (Mililitros, 1), ["ml."] = (Mililitros, 1),
        ["mililitro"] = (Mililitros, 1), ["mililitros"] = (Mililitros, 1),
        ["l"] = (Mililitros, 1000), ["litro"] = (Mililitros, 1000), ["litros"] = (Mililitros, 1000),
    };

    /// <summary>
    /// Normaliza a unidade de cada alimento (no próprio objeto) e devolve o
    /// resumo para o log: quantos foram corrigidos e as unidades fora do permitido.
    /// </summary>
    public static ResultadoNormalizacao Normalizar(IEnumerable<AlimentoDto> alimentos)
    {
        var total = 0;
        var corrigidos = 0;
        var foraDoPadrao = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var alimento in alimentos)
        {
            total++;
            var escrita = (alimento.Unidade ?? string.Empty).Trim();

            if (Sinonimos.TryGetValue(escrita.ToLowerInvariant(), out var padrao))
            {
                if (alimento.Unidade != padrao.Unidade || padrao.Fator != 1)
                {
                    alimento.Unidade = padrao.Unidade;
                    alimento.Quantidade *= padrao.Fator;
                    corrigidos++;
                }
            }
            else
            {
                foraDoPadrao[escrita] = foraDoPadrao.GetValueOrDefault(escrita) + 1;
            }
        }

        return new ResultadoNormalizacao(total, corrigidos, foraDoPadrao);
    }
}

/// <param name="Total">Alimentos da dieta.</param>
/// <param name="Corrigidos">Alimentos cuja unidade (ou quantidade, em kg/litro) foi reescrita.</param>
/// <param name="ForaDoPadrao">Unidades que não viram "g" nem "ml", com a contagem de cada.</param>
public record ResultadoNormalizacao(int Total, int Corrigidos, IReadOnlyDictionary<string, int> ForaDoPadrao);
