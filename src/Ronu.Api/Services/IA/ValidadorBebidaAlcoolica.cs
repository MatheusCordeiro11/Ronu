using System.Text.RegularExpressions;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Proteção da regra 8 do prompt: bebida alcoólica só pode aparecer se estiver
/// entre os alimentos preferidos da pessoa. No g7, uma conta sem nenhuma
/// preferência recebeu "Cereais cerveja lata (Cerveja pilsen)" no jantar de
/// sábado. Na primeira tentativa, resposta com bebida não permitida é tratada
/// como inválida, pelo mesmo caminho da validação estrutural (nova tentativa);
/// na última, a cerveja é tirada da dieta (TirarNaoPermitidas) em vez do 503.
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

    private static readonly Regex Cerveja = new(@"\bcerveja\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Devolve o nome do primeiro alimento com bebida alcoólica que não está
    /// entre os preferidos (a mesma bebida citada em algum preferido libera),
    /// ou null se não houver nenhum.
    /// </summary>
    public static string? EncontrarNaoPermitida(IEnumerable<AlimentoDto> alimentos, IEnumerable<string> preferidos)
    {
        var listaPreferidos = preferidos.ToList();
        return alimentos.FirstOrDefault(a => EhNaoPermitida(a.Nome, listaPreferidos))?.Nome;
    }

    /// <summary>
    /// Se algum preferido é uma bebida alcoólica. Sem nenhuma, o prompt proíbe
    /// bebida alcoólica de vez, em vez de explicar quando ela pode aparecer.
    /// </summary>
    public static bool PreferidosTemBebida(IEnumerable<string> preferidos)
    {
        var listaPreferidos = preferidos.ToList();
        return Padroes.Any(p => listaPreferidos.Any(p.IsMatch));
    }

    /// <summary>
    /// Rede de segurança da última tentativa: tira da dieta a cerveja que a
    /// pessoa não pediu, em vez de descartar a dieta inteira (no g8b, 2026-10-25,
    /// 11 de 16 chamadas ao Gemini de perfis sem bebida nas preferências vieram
    /// com cerveja, e metade dos pedidos virou 503). Desde a fase 3, as kcal da
    /// refeição saem do cálculo pela base depois de tirado o item; as da cerveja
    /// (TACO 474, "Cerveja, pilsen") vão só para o log. Uma refeição que fica sem
    /// nenhum alimento sai do dia.
    ///
    /// Só a cerveja é tirada: é a única bebida alcoólica da base, então é a única
    /// com valor de fonte para o log. Outra bebida, ou cerveja numa unidade
    /// que não seja g/ml, continua tornando a resposta inválida (503), assim
    /// como um dia que fique sem nenhuma refeição.
    /// </summary>
    public static List<BebidaRemovida> TirarNaoPermitidas(List<DiaDietaDto> dias, IEnumerable<string> preferidos)
    {
        var listaPreferidos = preferidos.ToList();
        var cerveja = Ronu.Api.Data.BaseNutricional.Itens.Single(i => i.Id == "cerveja-pilsen");
        var removidas = new List<BebidaRemovida>();

        foreach (var dia in dias)
        {
            var removidasAntes = removidas.Count;
            foreach (var refeicao in dia.Refeicoes)
            {
                foreach (var alimento in refeicao.Alimentos.Where(a => EhNaoPermitida(a.Nome, listaPreferidos)).ToList())
                {
                    if (!Cerveja.IsMatch(alimento.Nome)
                        || alimento.Unidade is not (NormalizadorUnidades.Gramas or NormalizadorUnidades.Mililitros))
                    {
                        throw new RespostaIaInvalidaException(
                            $"Resposta do Gemini com bebida alcoólica fora das preferências da pessoa: '{alimento.Nome}' ({alimento.Quantidade} {alimento.Unidade}), sem valor na base para descontar.");
                    }

                    // Para bebidas, o valor por 100 g da base vale como 100 ml (BaseNutricional). Só para o log: as
                    // kcal da refeição saem do cálculo pela base (CalculoPelaBase), depois de tirado o item.
                    var kcal = cerveja.Kcal * alimento.Quantidade / 100m;
                    refeicao.Alimentos.Remove(alimento);
                    removidas.Add(new BebidaRemovida(dia.DiaSemana, refeicao.Nome, alimento.Nome, alimento.Quantidade, alimento.Unidade, Math.Round(kcal, 1)));
                }
            }

            // Só mexe nos dias de onde saiu alguma bebida: um dia que já veio assim
            // não é problema desta regra.
            if (removidas.Count == removidasAntes)
            {
                continue;
            }

            dia.Refeicoes.RemoveAll(r => r.Alimentos.Count == 0);
            if (dia.Refeicoes.Count == 0)
            {
                throw new RespostaIaInvalidaException(
                    $"Dia '{dia.DiaSemana}' ficou sem nenhuma refeição depois de tirar a bebida alcoólica fora das preferências.");
            }
        }

        return removidas;
    }

    private static bool EhNaoPermitida(string nome, IReadOnlyList<string> preferidos) =>
        Padroes.Any(p => p.IsMatch(nome) && !preferidos.Any(p.IsMatch));
}

/// <summary>Uma bebida tirada da dieta por TirarNaoPermitidas, para o log da geração.</summary>
public record BebidaRemovida(string Dia, string Refeicao, string Alimento, decimal Quantidade, string Unidade, decimal KcalDescontadas);
