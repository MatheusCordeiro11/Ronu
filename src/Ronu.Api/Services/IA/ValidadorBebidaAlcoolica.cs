using System.Text.RegularExpressions;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Proteção da regra 8 do prompt: bebida alcoólica só pode aparecer se estiver
/// entre os alimentos preferidos da pessoa. No g7, uma conta sem nenhuma
/// preferência recebeu "Cereais cerveja lata (Cerveja pilsen)" no jantar de
/// sábado. Resposta com bebida não permitida é tratada como inválida, pelo
/// mesmo caminho da validação estrutural (nova tentativa, depois 503).
/// </summary>
public static class ValidadorBebidaAlcoolica
{
    // Lista curta, com as grafias comuns. Palavra inteira, sem diferenciar
    // maiúsculas: "Cerveja pilsen" casa, "gengibre" não casa com "gin".
    private static readonly string[] Bebidas =
    {
        "cerveja", "vinho", "vodca", "vodka", "cachaça", "whisky", "uísque",
        "gin", "rum", "licor", "chope", "chopp", "espumante"
    };

    private static readonly Regex[] Padroes = Bebidas
        .Select(b => new Regex($@"\b{b}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        .ToArray();

    /// <summary>
    /// Devolve o nome do primeiro alimento com bebida alcoólica que não está
    /// entre os preferidos (a mesma bebida citada em algum preferido libera),
    /// ou null se não houver nenhum.
    /// </summary>
    public static string? EncontrarNaoPermitida(IEnumerable<AlimentoDto> alimentos, IEnumerable<string> preferidos)
    {
        var listaPreferidos = preferidos.ToList();

        foreach (var alimento in alimentos)
        {
            foreach (var padrao in Padroes)
            {
                if (padrao.IsMatch(alimento.Nome) && !listaPreferidos.Any(padrao.IsMatch))
                {
                    return alimento.Nome;
                }
            }
        }

        return null;
    }
}
