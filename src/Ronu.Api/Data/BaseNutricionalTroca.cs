// Calibração da troca de alimento (2026-10-26), aprovada no passo 0 da feature. Julgamento do projeto, não valor
// de tabela, como a PorcaoMaxima e a classificação da fase 2a.

namespace Ronu.Api.Data;

public static partial class BaseNutricional
{
    /// <summary>
    /// Porção máxima de um substituto na troca, quando difere da PorcaoMaxima. A PorcaoMaxima foi pensada para o
    /// cardápio gerado e é usada pelo ajuste de quantidades (fase 4, medido no g9c); mudá-la mexeria na geração.
    /// Aqui só sobe o que a simulação da troca sobre as 7 dietas do g9c pediu com frequência e é plausível numa
    /// refeição de quem treina: os tubérculos e o cuscuz têm menos carboidrato por grama que o arroz (teto de 400 g),
    /// então precisam de mais gramas para entregar o mesmo. Ficam de fora, de propósito, a batata inglesa (os
    /// 52 kcal da TACO levariam a 700 g no lugar de 300 g de arroz), o milho em conserva e a farinha (acompanhamentos),
    /// as frutas pouco densas (4 kiwis no lugar de 2 bananas) e os itens de passar no pão.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, decimal> PorcoesMaximasTroca = new Dictionary<string, decimal>
    {
        ["batata-doce-cozida"] = 500m,  // mediana pedida na simulação: 505 g
        ["mandioca-cozida"] = 400m,  // 340 g; igual ao teto do arroz
        ["cuscuz-de-milho-cozido"] = 400m,  // 370 g
        ["mandioquinha-cozida"] = 400m,  // 390 g
        ["cara-cozido"] = 400m,  // 390 g
        ["pao-frances"] = 200m,  // 4 unidades; 200 g
        ["pao-de-forma-integral"] = 150m,  // 6 fatias; 150 g
        ["aveia-em-flocos"] = 100m,  // 110 g
        ["oleo-de-soja"] = 20m,  // igual ao azeite
        ["tangerina"] = 405m,  // 3 unidades; 405 g
    };

    /// <summary>
    /// Em que parte do dia o item é usado, só nos grupos em que o grupo de troca junta usos diferentes. Nos
    /// carboidratos, ordena as opções (primeiro as do mesmo uso; o "ver mais" mostra todas): sem isso, o que menos
    /// muda as kcal no lugar do pão francês é o macarrão. Nas gorduras, filtra: azeite e óleo vão no preparo e na
    /// salada, manteiga e requeijão no pão, e um não substitui o outro.
    ///
    /// Mandioca, batata-doce e cuscuz servem nos dois usos: o cuscuz e a macaxeira (aipim) no café da manhã do
    /// Nordeste, e a batata-doce no café de quem treina. É calibração do projeto, decidida na etapa 1 (2026-10-26):
    /// no passo 0 só o cuscuz servia nos dois, e os tubérculos eram só "prato". Os outros tubérculos (batata
    /// inglesa, mandioquinha, cará) continuam só no prato.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, UsoNaTroca> UsosNaTroca = new Dictionary<string, UsoNaTroca>
    {
        ["arroz-branco-cozido"] = UsoNaTroca.Prato,
        ["arroz-integral-cozido"] = UsoNaTroca.Prato,
        ["macarrao-cozido"] = UsoNaTroca.Prato,
        ["milho-verde-em-conserva"] = UsoNaTroca.Prato,
        ["farinha-de-mandioca-torrada"] = UsoNaTroca.Prato,
        ["batata-inglesa-cozida"] = UsoNaTroca.Prato,
        ["mandioquinha-cozida"] = UsoNaTroca.Prato,
        ["cara-cozido"] = UsoNaTroca.Prato,
        ["mandioca-cozida"] = UsoNaTroca.PratoECafe,
        ["batata-doce-cozida"] = UsoNaTroca.PratoECafe,
        ["cuscuz-de-milho-cozido"] = UsoNaTroca.PratoECafe,
        ["aveia-em-flocos"] = UsoNaTroca.CafeELanche,
        ["tapioca"] = UsoNaTroca.CafeELanche,
        ["pao-frances"] = UsoNaTroca.CafeELanche,
        ["pao-de-forma-integral"] = UsoNaTroca.CafeELanche,
        ["pao-de-queijo-assado"] = UsoNaTroca.CafeELanche,
        ["azeite-de-oliva-extra-virgem"] = UsoNaTroca.Preparo,
        ["oleo-de-soja"] = UsoNaTroca.Preparo,
        ["manteiga"] = UsoNaTroca.PassarNoPao,
        ["requeijao-cremoso"] = UsoNaTroca.PassarNoPao,
    };
}

/// <summary>Uso de um item na troca (BaseNutricional.UsosNaTroca).</summary>
public enum UsoNaTroca
{
    Prato,
    CafeELanche,
    PratoECafe,
    Preparo,
    PassarNoPao
}
