// Embalagens da lista de compras (2026-11-16): decisões do passo 0 aprovadas pelo dono do produto. Os tamanhos são
// calibração do projeto com evidência, como a PorcaoMaximaTroca, e não valor de tabela: cada um tem a fonte, a
// amostra e o motivo em docs/compras/embalagens.csv, e o teste ConversaoComprasEmbalagensTests confere o código
// contra esse arquivo. A conta da lista continua em gramas (ou ml, unidades, fatias): a embalagem é só a forma de
// mostrar o que se compra.

namespace Ronu.Api.Data;

public static partial class ConversaoCompras
{
    /// <summary>Acima disto, a linha volta para a quantidade de sempre ("2,4 kg" é mais claro que "5 pacotes").</summary>
    public const int LimiteEmbalagens = 4;

    /// <summary>
    /// Regra do Open Food Facts: o item só ganha tamanho com pelo menos 10 embalagens legíveis na amostra, e cada
    /// tamanho registrado tem pelo menos 15% delas. Quem não passa fica na quantidade de sempre, com o motivo no CSV.
    /// </summary>
    public const int AmostraMinimaOff = 10;
    public const decimal FracaoMinimaOff = 0.15m;

    public const string DataConsultaOff = "2026-11-16";

    private static TamanhoEmbalagem Off(decimal tamanho, string rotulo, string singular, string plural, int amostra, int doTamanho) =>
        new(tamanho, rotulo, singular, plural, FonteEmbalagem.OpenFoodFacts, amostra, doTamanho);

    private static EmbalagensDoItem Pacote(params TamanhoEmbalagem[] tamanhos) => new(GrupoEmbalagem.Embalagem, tamanhos);

    private static readonly EmbalagensDoItem SemTamanho = new(GrupoEmbalagem.Embalagem, []);
    private static readonly EmbalagensDoItem Granel = new(GrupoEmbalagem.Granel, []);
    private static readonly EmbalagensDoItem Contavel = new(GrupoEmbalagem.Contavel, []);
    private static readonly EmbalagensDoItem Pronto = new(GrupoEmbalagem.Pronto, []);

    // As fatias do pão de forma: o pacote é vendido em gramas, e a lista conta fatias de 25 g (a mesma medida da
    // POF que a base usa). 400 g = 16 fatias; 500 g = 20 fatias.
    private static decimal FatiasDoPacote(decimal gramas) =>
        gramas / BaseNutricional.Itens.Single(i => i.Id == "pao-de-forma-integral").MedidaUnidade!.Gramas;

    /// <summary>
    /// O grupo de cada item de compra (todos têm um) e os tamanhos de embalagem, na unidade do item, do menor para o
    /// maior. Grupo Embalagem sem tamanho = item que se compra embalado, mas ficou fora da v1 (o motivo está no CSV).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, EmbalagensDoItem> Embalagens = new Dictionary<string, EmbalagensDoItem>
    {
        // Hortifrúti: por peso, contáveis e prontos. Frutas grandes sem peça mínima na v1.
        ["alface"] = Granel, ["rucula"] = Granel, ["agriao"] = Granel, ["couve"] = Granel, ["repolho"] = Granel,
        ["repolho-roxo"] = Granel, ["acelga"] = Granel, ["beterraba"] = Granel, ["brocolis"] = Granel,
        ["couve-flor"] = Granel, ["abobrinha"] = Granel, ["chuchu"] = Granel, ["abobora"] = Granel,
        ["berinjela"] = Granel, ["batata"] = Granel, ["batata-doce"] = Granel, ["mandioca"] = Granel,
        ["mandioquinha"] = Granel, ["cara"] = Granel, ["banana-nanica"] = Granel, ["mamao-papaia"] = Granel,
        ["mamao-formosa"] = Granel, ["abacaxi"] = Granel, ["melancia"] = Granel, ["melao"] = Granel,
        ["morango"] = Granel, ["uva"] = Granel, ["manga"] = Granel, ["abacate"] = Granel,
        ["tomate"] = Contavel, ["pepino"] = Contavel, ["cebola"] = Contavel, ["cenoura"] = Contavel,
        ["banana-prata"] = Contavel, ["maca"] = Contavel, ["pera"] = Contavel, ["laranja"] = Contavel,
        ["tangerina"] = Contavel, ["kiwi"] = Contavel, ["goiaba"] = Contavel,
        ["abobrinha-refogada"] = Pronto, ["couve-refogada"] = Pronto, ["espinafre-refogado"] = Pronto,

        // Açougue e peixaria: no balcão sai a quantidade pedida. O frango inteiro é vendido por peça.
        ["patinho"] = Granel, ["patinho-moido"] = Granel, ["acem-moido"] = Granel, ["acem"] = Granel,
        ["alcatra"] = Granel, ["contrafile"] = Granel, ["coxao-mole"] = Granel, ["lagarto"] = Granel,
        ["musculo"] = Granel, ["maminha"] = Granel, ["file-mignon"] = Granel, ["peito-de-frango"] = Granel,
        ["coxa-de-frango"] = Granel, ["sobrecoxa-de-frango"] = Granel, ["lombo"] = Granel, ["bisteca"] = Granel,
        ["pernil"] = Granel, ["tilapia"] = Granel, ["salmao"] = Granel, ["sardinha"] = Granel, ["merluza"] = Granel,
        ["corvina"] = Granel,
        // Calibração (2026-11-16), sem fonte pública para o peso mínimo da peça: abaixo de 1 kg, a lista mostra a peça.
        ["frango-inteiro"] = new(GrupoEmbalagem.Peca,
            [new(1000m, "1 kg ou mais", "peça", "peças", FonteEmbalagem.Calibracao, null, null)]),

        // Frios, laticínios e ovos
        ["ovos"] = Pacote(new TamanhoEmbalagem(12m, "", "dúzia", "dúzias", FonteEmbalagem.Convencao, null, null)),
        ["leite-integral"] = Pacote(Off(1000m, "1 L", "caixa", "caixas", 35, 34)),
        ["leite-desnatado"] = Pacote(Off(1000m, "1 L", "caixa", "caixas", 21, 19)),
        ["iogurte-natural"] = SemTamanho,
        ["iogurte-natural-desnatado"] = Pacote(Off(170m, "170 g", "pote", "potes", 48, 14)),
        ["queijo-minas-frescal"] = SemTamanho,
        ["queijo-mucarela"] = SemTamanho,
        ["queijo-prato"] = SemTamanho,
        ["queijo-parmesao"] = Pacote(Off(50m, "50 g", "pacote", "pacotes", 37, 10), Off(100m, "100 g", "pacote", "pacotes", 37, 6)),
        ["ricota"] = SemTamanho,
        ["requeijao"] = Pacote(Off(200m, "200 g", "pote", "potes", 84, 28), Off(400m, "400 g", "pote", "potes", 84, 13)),
        ["manteiga"] = Pacote(Off(200m, "200 g", "pote", "potes", 53, 34), Off(500m, "500 g", "pote", "potes", 53, 10)),

        // Padaria
        ["pao-frances"] = Contavel,
        ["pao-de-forma"] = Pacote(
            Off(FatiasDoPacote(400m), "400 g", "pacote", "pacotes", 60, 21),
            Off(FatiasDoPacote(500m), "500 g", "pacote", "pacotes", 60, 13)),

        // Mercearia
        ["arroz-branco"] = Pacote(Off(1000m, "1 kg", "pacote", "pacotes", 74, 26), Off(5000m, "5 kg", "pacote", "pacotes", 74, 13)),
        ["arroz-integral"] = Pacote(Off(500m, "500 g", "pacote", "pacotes", 33, 5), Off(1000m, "1 kg", "pacote", "pacotes", 33, 18)),
        ["macarrao"] = Pacote(Off(500m, "500 g", "pacote", "pacotes", 96, 80)),
        ["flocao"] = SemTamanho,
        ["fecula-de-mandioca"] = SemTamanho,
        ["farinha-de-mandioca"] = Pacote(Off(500m, "500 g", "pacote", "pacotes", 12, 6), Off(1000m, "1 kg", "pacote", "pacotes", 12, 5)),
        ["aveia"] = Pacote(Off(500m, "500 g", "pacote", "pacotes", 69, 18)),
        ["milho-em-conserva"] = SemTamanho,
        ["feijao-carioca"] = Pacote(Off(500m, "500 g", "pacote", "pacotes", 16, 4), Off(1000m, "1 kg", "pacote", "pacotes", 16, 10)),
        ["feijao-preto"] = SemTamanho,
        ["feijao-fradinho"] = SemTamanho,
        ["lentilha"] = SemTamanho,
        ["atum"] = SemTamanho,
        ["pasta-de-amendoim"] = Pacote(Off(500m, "500 g", "pote", "potes", 41, 17)),
        ["amendoim"] = SemTamanho,
        ["castanha-de-caju"] = SemTamanho,
        ["castanha-do-para"] = SemTamanho,
        ["amendoa"] = SemTamanho,
        ["noz"] = SemTamanho,
        ["linhaca"] = Pacote(Off(200m, "200 g", "pacote", "pacotes", 19, 5)),
        ["azeite"] = SemTamanho,
        ["oleo-de-soja"] = SemTamanho,
        ["acucar-refinado"] = SemTamanho,
        ["acucar-mascavo"] = Pacote(Off(500m, "500 g", "pacote", "pacotes", 14, 9)),
        ["mel"] = Pacote(Off(300m, "300 g", "pote", "potes", 40, 17), Off(500m, "500 g", "pote", "potes", 40, 15)),
        ["cafe-em-po"] = Pacote(Off(250m, "250 g", "pacote", "pacotes", 76, 21), Off(500m, "500 g", "pacote", "pacotes", 76, 42)),
        ["cha-mate"] = SemTamanho,
        ["cha-preto"] = SemTamanho,
        ["cha-de-erva-doce"] = SemTamanho,

        // Bebidas
        ["suco-de-laranja"] = Pronto,
        ["agua-de-coco"] = Pacote(Off(200m, "200 ml", "caixinha", "caixinhas", 22, 14), Off(1000m, "1 L", "caixa", "caixas", 22, 6)),
        ["refrigerante-de-cola"] = Pacote(Off(2000m, "2 L", "garrafa", "garrafas", 42, 7)),
        ["refrigerante-de-guarana"] = Pacote(Off(350m, "350 ml", "lata", "latas", 43, 11), Off(2000m, "2 L", "garrafa", "garrafas", 43, 16)),
        ["cerveja-pilsen"] = Pacote(Off(350m, "350 ml", "lata", "latas", 16, 4)),

        // Congelados
        ["acai"] = SemTamanho,
        ["pao-de-queijo"] = SemTamanho,
    };
}

/// <summary>Como o item se compra. Embalagem, Granel, Contavel e Pronto são os grupos A a D da proposta.</summary>
public enum GrupoEmbalagem
{
    /// <summary>Embalagem padrão clara (pacote, caixa, lata, dúzia).</summary>
    Embalagem,

    /// <summary>Por peso, no balcão ou a granel: fica na quantidade de sempre.</summary>
    Granel,

    /// <summary>Já sai em unidades ("7 bananas").</summary>
    Contavel,

    /// <summary>Quantidade do prato pronto.</summary>
    Pronto,

    /// <summary>Vendido por peça de peso variável, com um peso mínimo (o frango inteiro).</summary>
    Peca
}

public enum FonteEmbalagem
{
    /// <summary>Moda da base brasileira do Open Food Facts (ODbL), com a amostra.</summary>
    OpenFoodFacts,

    /// <summary>Convenção de contagem (a dúzia).</summary>
    Convencao,

    /// <summary>Calibração do projeto, sem fonte pública (o peso mínimo da peça do frango).</summary>
    Calibracao
}

/// <param name="Grupo">Todo item de compra tem um.</param>
/// <param name="Tamanhos">Do menor para o maior. Vazio fora do grupo Embalagem e Peca, e nos itens adiados.</param>
public sealed record EmbalagensDoItem(GrupoEmbalagem Grupo, IReadOnlyList<TamanhoEmbalagem> Tamanhos);

/// <summary>Um tamanho de embalagem.</summary>
/// <param name="Quantidade">Na unidade da linha da lista (g, ml, unidades, fatias): 1000 no pacote de 1 kg, 16 no pão de forma de 400 g.</param>
/// <param name="Rotulo">Como o tamanho aparece depois do nome ("1 kg", "350 ml", "1 kg ou mais" na peça). Vazio na dúzia.</param>
/// <param name="Singular">"pacote", em "1 pacote de 1 kg".</param>
/// <param name="Plural">"pacotes", em "2 pacotes de 500 g".</param>
/// <param name="Amostra">Open Food Facts: embalagens do item com tamanho legível.</param>
/// <param name="DoTamanho">Open Food Facts: quantas delas têm este tamanho.</param>
public sealed record TamanhoEmbalagem(
    decimal Quantidade,
    string Rotulo,
    string Singular,
    string Plural,
    FonteEmbalagem Fonte,
    int? Amostra,
    int? DoTamanho);
