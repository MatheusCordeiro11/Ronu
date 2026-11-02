using System.Globalization;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Proteção da regra 7 do prompt: bebida com cafeína só de manhã e no início da tarde. O prompt sozinho não basta: no
/// g8, um cardápio trouxe refrigerante de cola no jantar das 20:00, e no g10d, chá mate na ceia das 21:30. Como a
/// regra 8 (ValidadorBebidaAlcoolica), na primeira tentativa uma resposta com cafeína à noite é inválida (nova
/// tentativa); na última, o café ou o chá vira chá de erva-doce, se ele está na lista da pessoa, e o resto sai da
/// refeição, em vez do 503.
///
/// À noite = refeição chamada de jantar ou ceia, ou com horário a partir das 18:00. Roda depois da lista
/// (AplicarLista): o item é reconhecido pelo id da base, não pelo nome.
/// </summary>
public static class ValidadorCafeina
{
    // Os itens da base com cafeína que importa à noite (tabela completa, com as fontes, em docs/fontes-nutricionais.md):
    // café coado 30 a 40 mg/100 ml, chá preto 18 a 20, refrigerante de cola 9 (USDA SR Legacy; BraCaffT: Rocha et al.,
    // Nutrients 2022, 14, 4417). O chá mate em infusão tem 5 mg/100 ml, mas fica: chimarrão e tereré têm 24 a 26
    // mg/100 ml (BraCaffT), e a base não separa o preparo. O refrigerante de guaraná fica de fora: 1 mg/100 ml, 4 mg
    // por lata de 350 ml (BraCaffT), menos que um café descafeinado.
    public static readonly IReadOnlySet<string> ComCafeina = new HashSet<string>
    {
        "cafe-coado", "cha-mate", "cha-preto", "refrigerante-de-cola"
    };

    // Café e chás viram o chá sem cafeína da base, na mesma quantidade; refrigerante só sai.
    private static readonly IReadOnlySet<string> Trocaveis = new HashSet<string> { "cafe-coado", "cha-mate", "cha-preto" };
    public const string SemCafeina = "cha-de-erva-doce";

    private static readonly TimeOnly Noite = new(18, 0);

    public static bool ENoite(RefeicaoDto refeicao) =>
        refeicao.Nome.Contains("jant", StringComparison.OrdinalIgnoreCase)
        || refeicao.Nome.Contains("ceia", StringComparison.OrdinalIgnoreCase)
        || (TimeOnly.TryParseExact(refeicao.Horario?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h) && h >= Noite);

    /// <summary>"Sexta-feira, Ceia 21:30: Chá mate" para cada item com cafeína à noite; vazia se não há nenhum.</summary>
    public static List<string> Encontrar(IEnumerable<DiaDietaDto> dias) => dias
        .SelectMany(d => d.Refeicoes.Where(ENoite).SelectMany(r => r.Alimentos
            .Where(a => a.AlimentoId is not null && ComCafeina.Contains(a.AlimentoId))
            .Select(a => $"{d.DiaSemana}, {r.Nome} {r.Horario}: {a.Nome}")))
        .ToList();

    /// <summary>
    /// Última tentativa: troca o café e o chá à noite pelo chá sem cafeína (se ele está na lista da pessoa) e tira o
    /// resto. Uma refeição que fica vazia sai do dia. Devolve o que mudou, para o log. As kcal saem do cálculo pela
    /// base, depois.
    /// </summary>
    public static List<string> Corrigir(List<DiaDietaDto> dias, ListaAlimentos lista)
    {
        var semCafeina = lista.Itens.FirstOrDefault(i => i.Item.Id == SemCafeina);
        var mudancas = new List<string>();

        foreach (var dia in dias)
        {
            foreach (var refeicao in dia.Refeicoes.Where(ENoite))
            {
                foreach (var alimento in refeicao.Alimentos.Where(a => a.AlimentoId is not null && ComCafeina.Contains(a.AlimentoId)).ToList())
                {
                    var rotulo = $"{dia.DiaSemana}, {refeicao.Nome} {refeicao.Horario}: {alimento.Nome}";
                    if (semCafeina is not null && Trocaveis.Contains(alimento.AlimentoId!))
                    {
                        alimento.Nome = semCafeina.Nome;
                        alimento.AlimentoId = semCafeina.Item.Id;
                        alimento.Unidade = semCafeina.Item.Medida;
                        mudancas.Add($"{rotulo} → {semCafeina.Nome}");
                    }
                    else
                    {
                        refeicao.Alimentos.Remove(alimento);
                        mudancas.Add($"{rotulo} (tirado)");
                    }
                }
            }

            dia.Refeicoes.RemoveAll(r => r.Alimentos.Count == 0);
        }

        return mudancas;
    }
}
