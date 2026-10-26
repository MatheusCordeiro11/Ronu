namespace Ronu.Api.Data;

/// <summary>
/// Como cada item da base nutricional vira o que se compra no mercado, para a lista de compras
/// semanal: o arroz cozido do cardápio vira arroz cru, o frango assado vira frango inteiro, a banana
/// vira "7 bananas". Só os dados e a conta de cada fator; quem soma a semana e arredonda é a lista.
///
/// O fator nunca é digitado: o código guarda as linhas da fonte (os mesmos números da tabela) e o
/// fator sai da divisão entre elas. As linhas estão em docs/compras/ (taco-tabela1.csv,
/// taco-trechos.csv e usda-sr28.csv, extraídas pelo script de lá), e o teste ConversaoComprasTests
/// confere cada uma contra esses arquivos e o lado cozido contra a própria BaseNutricional.
///
/// Fontes (citações completas em docs/fontes-nutricionais.md):
/// - TACO, Tabela 1 e capítulo de procedimentos, conferidos no PDF oficial. Código = número do alimento.
/// - USDA SR Legacy (o SR28 ASCII do ARS: composição em NUT_DATA, descarte em FOOD_DES, peso por
///   unidade em WEIGHT). Código = NDB, com o fdcId ao lado, que é o código que a base usa.
/// - Peso da unidade: POF 2008-2009/IBGE, a mesma MedidaCaseira da base.
///
/// O que não vem de tabela está marcado como calibração do projeto (PassoCalibracao e o teto de 1 das
/// hortaliças), com o motivo escrito.
///
/// Não mexe em BaseNutricional.VersaoBase: nenhum valor nutricional muda aqui. VersaoConversao é só
/// informativa, porque a lista é recalculada a cada consulta e nada dela é gravado.
/// </summary>
public static class ConversaoCompras
{
    public const int VersaoConversao = 1;

    /// <summary>O que se compra, um por linha da lista.</summary>
    public static IReadOnlyList<ItemCompra> Itens { get; }

    /// <summary>Para cada item da base (pelo Id), de onde ele vem no mercado.</summary>
    public static IReadOnlyDictionary<string, ConversaoItem> PorAlimento { get; }

    static ConversaoCompras()
    {
        Itens = ItensCompra;
        PorAlimento = Conversoes.ToDictionary(c => c.AlimentoId);
    }

    // ---------- Linhas das fontes (iguais aos CSVs de docs/compras/) ----------

    /// <summary>TACO, Tabela 1: umidade, proteína, lipídeos e carboidrato por 100 g. "Tr" (traço) = nulo.</summary>
    public static readonly IReadOnlyDictionary<int, LinhaTaco> Taco = new Dictionary<int, LinhaTaco>
    {
        [1] = new(1, "Arroz, integral, cozido", 29, 70.1m, 2.6m, 1.0m, 25.8m),
        [2] = new(2, "Arroz, integral, cru", 29, 12.2m, 7.3m, 1.9m, 77.5m),
        [3] = new(3, "Arroz, tipo 1, cozido", 29, 69.1m, 2.5m, 0.2m, 28.1m),
        [4] = new(4, "Arroz, tipo 1, cru", 29, 13.2m, 7.2m, 0.3m, 78.8m),
        [33] = new(33, "Farinha, de milho, amarela", 31, 11.8m, 7.2m, 1.5m, 79.1m),
        [64] = new(64, "Abóbora, cabotian, cozida", 33, 86.4m, 1.4m, 0.7m, 10.8m),
        [65] = new(65, "Abóbora, cabotian, crua", 33, 88.5m, 1.7m, 0.5m, 8.4m),
        [70] = new(70, "Abobrinha, italiana, cozida", 33, 95.3m, 1.1m, 0.2m, 3.0m),
        [71] = new(71, "Abobrinha, italiana, crua", 33, 93.9m, 1.1m, 0.1m, 4.3m),
        [86] = new(86, "Batata, baroa, cozida", 33, 79.3m, 0.9m, 0.2m, 18.9m),
        [87] = new(87, "Batata, baroa, crua", 33, 73.7m, 1.0m, 0.2m, 24.0m),
        [88] = new(88, "Batata, doce, cozida", 33, 80.4m, 0.6m, 0.1m, 18.4m),
        [89] = new(89, "Batata, doce, crua", 33, 69.5m, 1.3m, 0.1m, 28.2m),
        [91] = new(91, "Batata, inglesa, cozida", 33, 86.4m, 1.2m, null, 11.9m),
        [92] = new(92, "Batata, inglesa, crua", 33, 82.9m, 1.8m, null, 14.7m),
        [95] = new(95, "Berinjela, cozida", 35, 94.4m, 0.7m, 0.1m, 4.5m),
        [96] = new(96, "Berinjela, crua", 35, 93.8m, 1.2m, 0.1m, 4.4m),
        [97] = new(97, "Beterraba, cozida", 35, 90.6m, 1.3m, 0.1m, 7.2m),
        [98] = new(98, "Beterraba, crua", 35, 86.0m, 1.9m, 0.1m, 11.1m),
        [100] = new(100, "Brócolis, cozido", 35, 92.6m, 2.1m, 0.5m, 4.4m),
        [101] = new(101, "Brócolis, cru", 35, 91.2m, 3.6m, 0.3m, 4.0m),
        [102] = new(102, "Cará, cozido", 35, 78.9m, 1.5m, 0.1m, 18.9m),
        [103] = new(103, "Cará, cru", 35, 73.7m, 2.3m, 0.1m, 23.0m),
        [109] = new(109, "Cenoura, cozida", 35, 91.7m, 0.8m, 0.2m, 6.7m),
        [110] = new(110, "Cenoura, crua", 35, 90.1m, 1.3m, 0.2m, 7.7m),
        [112] = new(112, "Chuchu, cozido", 35, 94.6m, 0.4m, null, 4.8m),
        [113] = new(113, "Chuchu, cru", 35, 94.8m, 0.7m, 0.1m, 4.1m),
        [117] = new(117, "Couve-flor, crua", 35, 92.8m, 1.9m, 0.2m, 4.5m),
        [118] = new(118, "Couve-flor, cozida", 35, 94.3m, 1.2m, 0.3m, 3.9m),
        [124] = new(124, "Fécula, de mandioca", 35, 17.8m, 0.5m, 0.3m, 81.1m),
        [129] = new(129, "Mandioca, cozida", 37, 68.7m, 0.6m, 0.3m, 30.1m),
        [130] = new(130, "Mandioca, crua", 37, 61.8m, 1.1m, 0.3m, 36.2m),
        [140] = new(140, "Pão, de queijo, assado", 37, 33.7m, 5.1m, 24.6m, 34.2m),
        [141] = new(141, "Pão, de queijo, cru", 37, 41.8m, 3.6m, 14.0m, 38.5m),
        [261] = new(261, "Manteiga, com sal", 45, 15.8m, 0.4m, 82.4m, 0.1m),
        [292] = new(292, "Corvina do mar, crua", 47, 79.4m, 18.6m, 1.6m, 0.0m),
        [293] = new(293, "Corvina grande, assada", 47, 69.0m, 26.8m, 3.6m, 0.0m),
        [301] = new(301, "Merluza, filé, assado", 47, 70.7m, 26.6m, 0.9m, 0.0m),
        [302] = new(302, "Merluza, filé, cru", 47, 82.1m, 16.6m, 2.0m, 0.0m),
        [316] = new(316, "Salmão, sem pele, fresco, cru", 49, 69.0m, 19.3m, 9.7m, 0.0m),
        [317] = new(317, "Salmão, sem pele, fresco, grelhado", 49, 58.1m, 26.1m, 14.5m, 0.0m),
        [318] = new(318, "Sardinha, assada", 49, 60.1m, 32.2m, 3.0m, 0.0m),
        [321] = new(321, "Sardinha, inteira, crua", 49, 76.6m, 21.1m, 2.7m, 0.0m),
        [326] = new(326, "Carne, bovina, acém, moído, cozido", 49, 61.6m, 26.7m, 10.9m, 0.0m),
        [327] = new(327, "Carne, bovina, acém, moído, cru", 49, 72.7m, 19.4m, 5.9m, 0.0m),
        [328] = new(328, "Carne, bovina, acém, sem gordura, cozido", 49, 60.4m, 27.3m, 10.9m, 0.0m),
        [329] = new(329, "Carne, bovina, acém, sem gordura, cru", 49, 71.5m, 20.8m, 6.1m, 0.0m),
        [345] = new(345, "Carne, bovina, contra-filé, sem gordura, cru", 51, 69.1m, 24.0m, 6.0m, 0.0m),
        [346] = new(346, "Carne, bovina, contra-filé, sem gordura, grelhado", 51, 57.5m, 35.9m, 4.5m, 0.0m),
        [351] = new(351, "Carne, bovina, coxão mole, sem gordura, cozido", 51, 58.0m, 32.4m, 8.9m, 0.0m),
        [352] = new(352, "Carne, bovina, coxão mole, sem gordura, cru", 51, 68.6m, 21.2m, 8.7m, 0.0m),
        [357] = new(357, "Carne, bovina, filé mingnon, sem gordura, cru", 51, 71.9m, 21.6m, 5.6m, 0.0m),
        [358] = new(358, "Carne, bovina, filé mingnon, sem gordura, grelhado", 51, 57.0m, 32.8m, 8.8m, 0.0m),
        [363] = new(363, "Carne, bovina, lagarto, cozido", 51, 57.6m, 32.9m, 9.1m, 0.0m),
        [364] = new(364, "Carne, bovina, lagarto, cru", 51, 71.0m, 20.5m, 5.2m, 0.0m),
        [367] = new(367, "Carne, bovina, maminha, crua", 51, 70.0m, 20.9m, 7.0m, 0.0m),
        [368] = new(368, "Carne, bovina, maminha, grelhada", 51, 65.3m, 30.7m, 2.4m, 0.0m),
        [369] = new(369, "Carne, bovina, miolo de alcatra, sem gordura, cru", 51, 69.5m, 21.6m, 7.8m, 0.0m),
        [370] = new(370, "Carne, bovina, miolo de alcatra, sem gordura, grelhado", 51, 52.4m, 31.9m, 11.6m, 0.0m),
        [371] = new(371, "Carne, bovina, músculo, sem gordura, cozido", 51, 62.8m, 31.2m, 6.7m, 0.0m),
        [372] = new(372, "Carne, bovina, músculo, sem gordura, cru", 51, 72.4m, 21.6m, 5.5m, 0.0m),
        [376] = new(376, "Carne, bovina, patinho, sem gordura, cru", 53, 72.9m, 21.7m, 4.5m, 0.0m),
        [377] = new(377, "Carne, bovina, patinho, sem gordura, grelhado", 53, 55.2m, 35.9m, 7.3m, 0.0m),
        [398] = new(398, "Frango, coxa, sem pele, cozida", 53, 66.7m, 26.9m, 5.8m, 0.0m),
        [399] = new(399, "Frango, coxa, sem pele, crua", 53, 76.4m, 17.8m, 4.9m, 0.0m),
        [403] = new(403, "Frango, inteiro, sem pele, assado", 53, 63.2m, 28.0m, 7.5m, 0.0m),
        [405] = new(405, "Frango, inteiro, sem pele, cru", 55, 74.9m, 20.6m, 4.6m, 0.0m),
        [408] = new(408, "Frango, peito, sem pele, cozido", 55, 65.6m, 31.5m, 3.2m, 0.0m),
        [409] = new(409, "Frango, peito, sem pele, cru", 55, 74.8m, 21.5m, 3.0m, 0.0m),
        [410] = new(410, "Frango, peito, sem pele, grelhado", 55, 63.8m, 32.0m, 2.5m, 0.0m),
        [413] = new(413, "Frango, sobrecoxa, sem pele, assada", 55, 55.6m, 29.2m, 12.0m, 0.0m),
        [414] = new(414, "Frango, sobrecoxa, sem pele, crua", 55, 72.7m, 17.6m, 9.6m, 0.0m),
        [427] = new(427, "Porco, bisteca, crua", 55, 67.7m, 21.5m, 8.0m, 0.0m),
        [429] = new(429, "Porco, bisteca, grelhada", 55, 51.8m, 28.9m, 17.4m, 0.0m),
        [432] = new(432, "Porco, lombo, assado", 55, 56.6m, 35.7m, 6.4m, 0.0m),
        [433] = new(433, "Porco, lombo, cru", 55, 67.7m, 22.6m, 8.8m, 0.0m),
        [435] = new(435, "Porco, pernil, assado", 55, 49.3m, 32.1m, 13.9m, 0.0m),
        [436] = new(436, "Porco, pernil, cru", 55, 67.1m, 20.1m, 11.1m, 0.0m),
        [486] = new(486, "Ovo, de galinha, clara, cozida/10minutos", 59, 85.2m, 13.4m, 0.1m, 0.0m),
        [533] = new(533, "Cuscuz, de milho, cozido com sal", 63, 71.1m, 2.2m, 0.7m, 25.3m),
        [551] = new(551, "Tapioca, com manteiga", 65, 24.9m, 0.1m, 10.9m, 63.6m),
        [561] = new(561, "Feijão, carioca, cozido", 65, 80.4m, 4.8m, 0.5m, 13.6m),
        [562] = new(562, "Feijão, carioca, cru", 65, 14.0m, 20.0m, 1.3m, 61.2m),
        [563] = new(563, "Feijão, fradinho, cozido", 65, 80.0m, 5.1m, 0.6m, 13.5m),
        [564] = new(564, "Feijão, fradinho, cru", 65, 12.7m, 20.2m, 2.4m, 61.2m),
        [567] = new(567, "Feijão, preto, cozido", 65, 80.2m, 4.5m, 0.5m, 14.0m),
        [568] = new(568, "Feijão, preto, cru", 65, 14.9m, 21.3m, 1.2m, 58.8m),
        [577] = new(577, "Lentilha, cozida", 65, 76.3m, 6.3m, 0.5m, 16.3m),
        [578] = new(578, "Lentilha, crua", 67, 11.5m, 23.2m, 0.8m, 62.0m),
    };

    /// <summary>USDA SR Legacy (SR28): água, proteína e carboidrato por 100 g, descarte e peso da unidade.</summary>
    public static readonly IReadOnlyDictionary<string, LinhaUsda> Usda = new Dictionary<string, LinhaUsda>
    {
        ["20121"] = new("20121", "169737", "Pasta, cooked, enriched, without added salt", 62.13m, 5.80m, 30.86m, 0, "", null, null),
        ["20120"] = new("20120", "169736", "Pasta, dry, enriched", 9.90m, 13.04m, 74.67m, 0, "", null, null),
        ["15262"] = new("15262", "175177", "Fish, tilapia, cooked, dry heat", 71.59m, 26.15m, 0.00m, 0, "", null, null),
        ["15261"] = new("15261", "175176", "Fish, tilapia, raw", 78.08m, 20.08m, 0.00m, 0, "", null, null),
        ["09040"] = new("09040", "173944", "Bananas, raw", 74.91m, 1.09m, 22.84m, 36, "Skin", null, null),
        ["09226"] = new("09226", "169926", "Papayas, raw", 88.06m, 0.47m, 10.82m, 38, "Seeds and skin", null, null),
        ["09266"] = new("09266", "169124", "Pineapple, raw, all varieties", 86.00m, 0.54m, 13.12m, 49, "8% core, 16% crown, 26% parings", null, null),
        ["09326"] = new("09326", "167765", "Watermelon, raw", 91.45m, 0.61m, 7.55m, 48, "Rind, seeds, and cutting loss", null, null),
        ["09184"] = new("09184", "169911", "Melons, honeydew, raw", 89.82m, 0.54m, 9.09m, 54, "5% cavity contents, rind 49%", null, null),
        ["09176"] = new("09176", "169910", "Mangos, raw", 83.46m, 0.82m, 14.98m, 29, "Seeds and skin", null, null),
        ["09037"] = new("09037", "171705", "Avocados, raw, all commercial varieties", 73.23m, 2.00m, 8.53m, 26, "Seed and skin", null, null),
        ["11352"] = new("11352", "170026", "Potatoes, flesh and skin, raw", 79.25m, 2.05m, 17.49m, 25, "Parings and trimmings", null, null),
        ["11507"] = new("11507", "168482", "Sweet potato, raw, unprepared", 77.28m, 1.57m, 20.12m, 28, "Parings and trimmings", null, null),
        ["11134"] = new("11134", "169985", "Cassava, raw", 59.68m, 1.36m, 38.06m, 16, "Skin", null, null),
        ["05011"] = new("05011", "171052", "Chicken, broilers or fryers, meat only, raw", 75.46m, 21.39m, 0.00m, 52, "32% bone, 12% skin, 8% separable fat", null, null),
        ["01124"] = new("01124", "172183", "Egg, white, raw, fresh", 87.57m, 10.90m, 0.73m, 0, "", "large", 33m),
    };

    /// <summary>Trechos de texto da TACO (receitas do capítulo de procedimentos e lista de espécies).</summary>
    public static readonly IReadOnlyList<TrechoTaco> Trechos =
    [
        new(471, 117, "1. Pesar 400g de pó de café."),
        new(471, 117, "2. Esquentar 4 litros de água, sem deixar ferver. Desligar o fogo."),
        new(475, 121, "1. Pesar 20 gramas de chá; ou 20 saches de erva doce."),
        new(475, 121, "2. Esquentar 4 litros de água, sem deixar ferver."),
        new(476, 121, "1. Pesar 20g ou 20 saches de chá mate."),
        new(476, 121, "2. Esquentar 4 litros de água, sem deixar ferver."),
        new(477, 122, "1. Pesar 20g ou 20 saches de chá preto."),
        new(477, 122, "2. Esquentar 4 litros de água, sem deixar ferver."),
        new(533, 125, "1. Pesar 2,5kg de farinha de milho floculada (amarela), 1,5 colher de sopa de sal (60g) e 2,5 litros de"),
        new(292, 24, "Corvina do mar Micropogonias furnieri"),
        new(293, 24, "Corvina grande Micropogonias furnieri"),
    ];

    private static TrechoTaco Trecho(int codigo, string inicio) =>
        Trechos.Single(t => t.Codigo == codigo && t.Texto.StartsWith(inicio, StringComparison.Ordinal));

    // ---------- Passos reutilizados ----------

    private static PassoParCruCozido ParTaco(int cozido, int cru, NutrienteDoPar nutriente, bool tetoUm = false) =>
        new(Taco[cozido], Taco[cru], nutriente, tetoUm);

    private static PassoDescarte Descarte(string ndb) => new(Usda[ndb]);

    private static PassoUnidade UnidadePof(string alimentoId)
    {
        var medida = BaseNutricional.Itens.Single(i => i.Id == alimentoId).MedidaUnidade!;
        return new(medida.Gramas, $"POF 2008-2009/IBGE, alimento {medida.CodigoPof}, p. {medida.PaginaPof}: \"{medida.DescricaoNaFonte}\"");
    }

    // D1 (2026-10-26): a hortaliça cozida usa a matéria seca, que não se perde no cozimento, e o fator nunca
    // passa de 1. No chuchu e na abóbora a TACO dá mais de 1, porque as amostras crua e cozida são de lotes
    // diferentes; comprar menos cru do que se come não faz sentido.
    private const string MotivoTetoHortalica =
        "Calibração do projeto: a conta da TACO dá mais de 1, porque as amostras crua e cozida são de lotes "
        + "diferentes. A hortaliça não ganha peso seco ao cozinhar, então a lista usa 1 (o mesmo peso cru).";

    // D3 (2026-10-26): a clara quase não perde peso ao cozinhar (cozida na casca). Dividir a proteína da
    // TACO (cozida) pela do USDA (crua) daria 1,23, e a diferença vem das amostras, não do cozimento.
    private const string MotivoClara =
        "Calibração do projeto: a clara cozida na casca quase não perde peso, então 1 g de clara cozida = 1 g "
        + "de clara crua. Sobram as gemas.";

    // ---------- O que se compra ----------

    private static readonly ItemCompra[] ItensCompra =
    [
        // Hortifrúti
        new("alface", "Alface crespa", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("rucula", "Rúcula", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("agriao", "Agrião", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("couve", "Couve manteiga", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("repolho", "Repolho branco", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("repolho-roxo", "Repolho roxo", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("acelga", "Acelga", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("tomate", "Tomate", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "tomate", "tomates"),
        new("pepino", "Pepino", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "pepino", "pepinos"),
        new("cebola", "Cebola", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "cebola", "cebolas"),
        new("cenoura", "Cenoura", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "cenoura", "cenouras"),
        new("beterraba", "Beterraba", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("brocolis", "Brócolis", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("couve-flor", "Couve-flor", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("abobrinha", "Abobrinha italiana", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("chuchu", "Chuchu", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("abobora", "Abóbora cabotiá", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("berinjela", "Berinjela", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("abobrinha-refogada", "Abobrinha refogada", SecaoMercado.Hortifruti, UnidadeCompra.Gramas, Pronto: true),
        new("couve-refogada", "Couve refogada", SecaoMercado.Hortifruti, UnidadeCompra.Gramas, Pronto: true),
        new("espinafre-refogado", "Espinafre refogado", SecaoMercado.Hortifruti, UnidadeCompra.Gramas, Pronto: true),
        new("batata", "Batata inglesa", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("batata-doce", "Batata-doce", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        // As mesmas UFs do apelido da mandioca cozida na BaseNutricional (o teste confere).
        new("mandioca", "Mandioca", SecaoMercado.Hortifruti, UnidadeCompra.Gramas,
            Apelidos: [new("Aipim", ["RJ"]), new("Macaxeira", ["AM", "PA", "AC", "RO", "RR", "AP", "TO", "MA", "PI", "CE", "RN", "PB", "PE", "AL", "SE", "BA"])]),
        new("mandioquinha", "Mandioquinha", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("cara", "Cará", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("banana-prata", "Banana prata", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "banana", "bananas"),
        new("banana-nanica", "Banana nanica", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("maca", "Maçã fuji", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "maçã", "maçãs"),
        new("pera", "Pera williams", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "pera", "peras"),
        new("laranja", "Laranja pera", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "laranja", "laranjas"),
        new("tangerina", "Tangerina poncã", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "tangerina", "tangerinas"),
        new("kiwi", "Kiwi", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "kiwi", "kiwis"),
        new("goiaba", "Goiaba vermelha", SecaoMercado.Hortifruti, UnidadeCompra.Unidades, "goiaba", "goiabas"),
        new("mamao-papaia", "Mamão papaia", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("mamao-formosa", "Mamão formosa", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("abacaxi", "Abacaxi", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("melancia", "Melancia", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("melao", "Melão", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("morango", "Morango", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("uva", "Uva itália", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("manga", "Manga palmer", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),
        new("abacate", "Abacate", SecaoMercado.Hortifruti, UnidadeCompra.Gramas),

        // Açougue e peixaria
        new("patinho", "Patinho", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("patinho-moido", "Patinho moído", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("acem-moido", "Acém moído", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("acem", "Acém", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("alcatra", "Miolo de alcatra", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("contrafile", "Contrafilé", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("coxao-mole", "Coxão mole", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("lagarto", "Lagarto", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("musculo", "Músculo", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("maminha", "Maminha", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("file-mignon", "Filé mignon", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("peito-de-frango", "Peito de frango sem pele", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        // D5 (2026-10-26): o peso é de carne sem osso (e sem pele, no frango): a TACO não tem o par com osso,
        // e o descarte do USDA para coxa é inconsistente. Ninguém compra assim, então a linha avisa.
        new("coxa-de-frango", "Coxa de frango", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas,
            NotaNaLinha: "de carne, sem osso e sem pele · compre um pouco mais"),
        new("sobrecoxa-de-frango", "Sobrecoxa de frango", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas,
            NotaNaLinha: "de carne, sem osso e sem pele · compre um pouco mais"),
        new("frango-inteiro", "Frango inteiro", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("lombo", "Lombo suíno", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("bisteca", "Bisteca suína", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas,
            NotaNaLinha: "de carne, sem osso · compre um pouco mais"),
        new("pernil", "Pernil suíno", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("tilapia", "Filé de tilápia", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("salmao", "Salmão sem pele", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("sardinha", "Sardinha", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("merluza", "Filé de merluza", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),
        new("corvina", "Corvina", SecaoMercado.AcougueEPeixaria, UnidadeCompra.Gramas),

        // Frios, laticínios e ovos
        new("ovos", "Ovos", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Unidades, "ovo", "ovos"),
        new("leite-integral", "Leite integral", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Mililitros),
        new("leite-desnatado", "Leite desnatado", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Mililitros),
        new("iogurte-natural", "Iogurte natural", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),
        new("iogurte-natural-desnatado", "Iogurte natural desnatado", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),
        new("queijo-minas-frescal", "Queijo minas frescal", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),
        new("queijo-mucarela", "Queijo muçarela", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),
        new("queijo-prato", "Queijo prato", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),
        new("queijo-parmesao", "Queijo parmesão", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),
        new("ricota", "Ricota", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),
        new("requeijao", "Requeijão cremoso", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),
        new("manteiga", "Manteiga com sal", SecaoMercado.FriosLaticiniosEOvos, UnidadeCompra.Gramas),

        // Padaria
        new("pao-frances", "Pão francês", SecaoMercado.Padaria, UnidadeCompra.Unidades, "pão", "pães"),
        new("pao-de-forma", "Pão de forma integral", SecaoMercado.Padaria, UnidadeCompra.Fatias, "fatia", "fatias"),

        // Mercearia
        new("arroz-branco", "Arroz branco tipo 1", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("arroz-integral", "Arroz integral", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("macarrao", "Macarrão", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("flocao", "Flocão de milho", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("fecula-de-mandioca", "Fécula de mandioca (polvilho doce)", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("farinha-de-mandioca", "Farinha de mandioca torrada", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("aveia", "Aveia em flocos", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("milho-em-conserva", "Milho verde em conserva", SecaoMercado.Mercearia, UnidadeCompra.Gramas, NotaNaLinha: "peso drenado"),
        new("feijao-carioca", "Feijão carioca", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("feijao-preto", "Feijão preto", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("feijao-fradinho", "Feijão fradinho", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("lentilha", "Lentilha", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("atum", "Atum em conserva em água", SecaoMercado.Mercearia, UnidadeCompra.Gramas, NotaNaLinha: "peso drenado"),
        new("pasta-de-amendoim", "Pasta de amendoim integral", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("amendoim", "Amendoim torrado", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("castanha-de-caju", "Castanha-de-caju", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("castanha-do-para", "Castanha-do-pará", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("amendoa", "Amêndoa torrada", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("noz", "Noz", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("linhaca", "Linhaça", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("azeite", "Azeite de oliva extra virgem", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("oleo-de-soja", "Óleo de soja", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("acucar-refinado", "Açúcar refinado", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("acucar-mascavo", "Açúcar mascavo", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("mel", "Mel", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("cafe-em-po", "Café em pó", SecaoMercado.Mercearia, UnidadeCompra.Gramas),
        new("cha-mate", "Chá mate", SecaoMercado.Mercearia, UnidadeCompra.Saches, "sachê", "sachês"),
        new("cha-preto", "Chá preto", SecaoMercado.Mercearia, UnidadeCompra.Saches, "sachê", "sachês"),
        new("cha-de-erva-doce", "Chá de erva-doce", SecaoMercado.Mercearia, UnidadeCompra.Saches, "sachê", "sachês"),

        // Bebidas
        new("suco-de-laranja", "Suco de laranja natural", SecaoMercado.Bebidas, UnidadeCompra.Mililitros, Pronto: true),
        new("agua-de-coco", "Água de coco", SecaoMercado.Bebidas, UnidadeCompra.Mililitros),
        new("refrigerante-de-cola", "Refrigerante de cola", SecaoMercado.Bebidas, UnidadeCompra.Mililitros),
        new("refrigerante-de-guarana", "Refrigerante de guaraná", SecaoMercado.Bebidas, UnidadeCompra.Mililitros),
        new("cerveja-pilsen", "Cerveja pilsen", SecaoMercado.Bebidas, UnidadeCompra.Mililitros),

        // Congelados
        new("acai", "Açaí sem açúcar (polpa)", SecaoMercado.Congelados, UnidadeCompra.Gramas),
        new("pao-de-queijo", "Pão de queijo", SecaoMercado.Congelados, UnidadeCompra.Unidades, "pão de queijo", "pães de queijo"),
    ];

    // ---------- De cada item da base para o que se compra ----------

    private static readonly ConversaoItem[] Conversoes =
    [
        new("arroz-branco-cozido", "arroz-branco", [ParTaco(3, 4, NutrienteDoPar.Carboidrato)]),
        new("arroz-integral-cozido", "arroz-integral", [ParTaco(1, 2, NutrienteDoPar.Carboidrato)]),
        new("aveia-em-flocos", "aveia", []),
        new("macarrao-cozido", "macarrao", [new PassoParCruCozido(Usda["20121"], Usda["20120"], NutrienteDoPar.Carboidrato)]),
        // A receita da TACO (533) usa "farinha de milho floculada (amarela)": o par é a farinha de milho amarela (33).
        new("cuscuz-de-milho-cozido", "flocao", [ParTaco(533, 33, NutrienteDoPar.Carboidrato)]),
        new("milho-verde-em-conserva", "milho-em-conserva", []),
        new("tapioca", "fecula-de-mandioca", [new PassoFeculaDaTapioca(Taco[551], Taco[261], Taco[124])]),
        new("farinha-de-mandioca-torrada", "farinha-de-mandioca", []),
        new("pao-frances", "pao-frances", [UnidadePof("pao-frances")]),
        new("pao-de-forma-integral", "pao-de-forma", [UnidadePof("pao-de-forma-integral")]),
        // A peça crua congelada é a mesma que sai assada: conta-se em unidades, com o peso assado da POF.
        new("pao-de-queijo-assado", "pao-de-queijo", [UnidadePof("pao-de-queijo-assado")]),
        new("feijao-cozido", "feijao-carioca", [ParTaco(561, 562, NutrienteDoPar.Carboidrato)]),
        new("feijao-preto-cozido", "feijao-preto", [ParTaco(567, 568, NutrienteDoPar.Carboidrato)]),
        new("feijao-fradinho-cozido", "feijao-fradinho", [ParTaco(563, 564, NutrienteDoPar.Carboidrato)]),
        new("lentilha-cozida", "lentilha", [ParTaco(577, 578, NutrienteDoPar.Carboidrato)]),
        new("batata-inglesa-cozida", "batata", [ParTaco(91, 92, NutrienteDoPar.Carboidrato), Descarte("11352")]),
        new("batata-doce-cozida", "batata-doce", [ParTaco(88, 89, NutrienteDoPar.Carboidrato), Descarte("11507")]),
        new("mandioca-cozida", "mandioca", [ParTaco(129, 130, NutrienteDoPar.Carboidrato), Descarte("11134")]),
        new("mandioquinha-cozida", "mandioquinha", [ParTaco(86, 87, NutrienteDoPar.Carboidrato)]),
        new("cara-cozido", "cara", [ParTaco(102, 103, NutrienteDoPar.Carboidrato)]),
        new("alface-crua", "alface", []),
        new("rucula-crua", "rucula", []),
        new("agriao-cru", "agriao", []),
        new("couve-crua", "couve", []),
        new("tomate-cru", "tomate", [UnidadePof("tomate-cru")]),
        new("pepino-cru", "pepino", [UnidadePof("pepino-cru")]),
        new("cenoura-crua", "cenoura", [UnidadePof("cenoura-crua")]),
        new("cenoura-cozida", "cenoura", [ParTaco(109, 110, NutrienteDoPar.MateriaSeca, tetoUm: true), UnidadePof("cenoura-crua")]),
        new("beterraba-crua", "beterraba", []),
        new("beterraba-cozida", "beterraba", [ParTaco(97, 98, NutrienteDoPar.MateriaSeca, tetoUm: true)]),
        new("brocolis-cozido", "brocolis", [ParTaco(100, 101, NutrienteDoPar.MateriaSeca, tetoUm: true)]),
        new("couve-flor-cozida", "couve-flor", [ParTaco(118, 117, NutrienteDoPar.MateriaSeca, tetoUm: true)]),
        new("abobrinha-cozida", "abobrinha", [ParTaco(70, 71, NutrienteDoPar.MateriaSeca, tetoUm: true)]),
        // D2: nos refogados a TACO põe óleo e cebola na receita, e os nutrientes discordam entre si.
        new("abobrinha-refogada", "abobrinha-refogada", [], LevaOleoNoPreparo: true),
        new("chuchu-cozido", "chuchu", [ParTaco(112, 113, NutrienteDoPar.MateriaSeca, tetoUm: true)], Observacao: MotivoTetoHortalica),
        new("couve-refogada", "couve-refogada", [], LevaOleoNoPreparo: true),
        new("espinafre-refogado", "espinafre-refogado", [], LevaOleoNoPreparo: true),
        new("repolho-cru", "repolho", []),
        new("repolho-roxo-cru", "repolho-roxo", []),
        new("acelga-crua", "acelga", []),
        new("cebola-crua", "cebola", [UnidadePof("cebola-crua")]),
        new("abobora-cozida", "abobora", [ParTaco(64, 65, NutrienteDoPar.MateriaSeca, tetoUm: true)], Observacao: MotivoTetoHortalica),
        new("berinjela-cozida", "berinjela", [ParTaco(95, 96, NutrienteDoPar.MateriaSeca, tetoUm: true)]),
        // Frutas com unidade na POF: a contagem é a mesma da dieta ("2 unidades"), sem descarte.
        new("banana-prata", "banana-prata", [UnidadePof("banana-prata")]),
        new("banana-nanica", "banana-nanica", [Descarte("09040")]),
        new("maca-fuji", "maca", [UnidadePof("maca-fuji")]),
        new("pera-williams", "pera", [UnidadePof("pera-williams")]),
        new("laranja-pera", "laranja", [UnidadePof("laranja-pera")]),
        new("tangerina", "tangerina", [UnidadePof("tangerina")]),
        new("kiwi", "kiwi", [UnidadePof("kiwi")]),
        new("mamao-papaia", "mamao-papaia", [Descarte("09226")]),
        new("mamao-formosa", "mamao-formosa", [Descarte("09226")]),
        new("abacaxi", "abacaxi", [Descarte("09266")]),
        new("melancia", "melancia", [Descarte("09326")]),
        // D4: o melão amarelo é do mesmo grupo do honeydew (inodorus), não do cantaloupe.
        new("melao", "melao", [Descarte("09184")]),
        // Morango e uva: descarte de 6% e 4% no USDA, desprezível (D4 e D10).
        new("morango", "morango", []),
        new("uva-italia", "uva", []),
        new("manga-palmer", "manga", [Descarte("09176")]),
        new("abacate", "abacate", [Descarte("09037")]),
        new("goiaba-vermelha", "goiaba", [UnidadePof("goiaba-vermelha")]),
        new("acai-sem-acucar", "acai", []),
        new("patinho-bovino-grelhado", "patinho", [ParTaco(377, 376, NutrienteDoPar.Proteina)]),
        // Derivado do patinho grelhado (377): o par cru é o mesmo.
        new("patinho-bovino-moido-refogado", "patinho-moido", [ParTaco(377, 376, NutrienteDoPar.Proteina)]),
        new("carne-moida-de-acem-cozida", "acem-moido", [ParTaco(326, 327, NutrienteDoPar.Proteina)]),
        new("acem-bovino-cozido", "acem", [ParTaco(328, 329, NutrienteDoPar.Proteina)]),
        new("alcatra-bovina-grelhada", "alcatra", [ParTaco(370, 369, NutrienteDoPar.Proteina)]),
        new("contrafile-bovino-grelhado", "contrafile", [ParTaco(346, 345, NutrienteDoPar.Proteina)]),
        new("coxao-mole-bovino-cozido", "coxao-mole", [ParTaco(351, 352, NutrienteDoPar.Proteina)]),
        new("lagarto-bovino-cozido", "lagarto", [ParTaco(363, 364, NutrienteDoPar.Proteina)]),
        new("musculo-bovino-cozido", "musculo", [ParTaco(371, 372, NutrienteDoPar.Proteina)]),
        new("maminha-bovina-grelhada", "maminha", [ParTaco(368, 367, NutrienteDoPar.Proteina)]),
        new("file-mignon-grelhado", "file-mignon", [ParTaco(358, 357, NutrienteDoPar.Proteina)]),
        new("peito-de-frango-sem-pele-grelhado", "peito-de-frango", [ParTaco(410, 409, NutrienteDoPar.Proteina)]),
        new("peito-de-frango-sem-pele-cozido", "peito-de-frango", [ParTaco(408, 409, NutrienteDoPar.Proteina)]),
        new("coxa-de-frango-sem-pele-cozida", "coxa-de-frango", [ParTaco(398, 399, NutrienteDoPar.Proteina)]),
        new("sobrecoxa-de-frango-sem-pele-assada", "sobrecoxa-de-frango", [ParTaco(413, 414, NutrienteDoPar.Proteina)]),
        // A TACO tira a pele antes de assar; o USDA dá o descarte da carne crua (osso, pele e gordura).
        new("frango-sem-pele-assado", "frango-inteiro", [ParTaco(403, 405, NutrienteDoPar.Proteina), Descarte("05011")]),
        new("lombo-suino-assado", "lombo", [ParTaco(432, 433, NutrienteDoPar.Proteina)]),
        new("bisteca-suina-grelhada", "bisteca", [ParTaco(429, 427, NutrienteDoPar.Proteina)]),
        new("pernil-suino-assado", "pernil", [ParTaco(435, 436, NutrienteDoPar.Proteina)]),
        new("file-de-tilapia-grelhado", "tilapia", [new PassoParCruCozido(Usda["15262"], Usda["15261"], NutrienteDoPar.Proteina)]),
        new("salmao-grelhado", "salmao", [ParTaco(317, 316, NutrienteDoPar.Proteina)]),
        new("sardinha-assada", "sardinha", [ParTaco(318, 321, NutrienteDoPar.Proteina)]),
        new("atum-em-conserva-em-agua", "atum", []),
        new("merluza-assada", "merluza", [ParTaco(301, 302, NutrienteDoPar.Proteina)]),
        // A TACO não tem corvina grande crua; pela lista de espécies dela (p. 24), a corvina grande e a
        // corvina do mar são a mesma espécie (Micropogonias furnieri).
        new("corvina-assada", "corvina", [ParTaco(293, 292, NutrienteDoPar.Proteina)]),
        // Ovos contam pela unidade da POF de cada preparo: a contagem é a mesma da dieta.
        new("ovo-de-galinha-cozido", "ovos", [UnidadePof("ovo-de-galinha-cozido")]),
        new("ovo-de-galinha-frito", "ovos", [UnidadePof("ovo-de-galinha-frito")], LevaOleoNoPreparo: true),
        new("clara-de-ovo-cozida", "ovos",
            [new PassoCalibracao(1m, MotivoClara), new PassoUnidade(Usda["01124"].GramasUnidade!.Value, "USDA SR Legacy, Egg, white, raw, fresh (NDB 01124): 1 large = 33 g")],
            Observacao: "Sobram as gemas."),
        new("leite-integral", "leite-integral", []),
        new("leite-desnatado", "leite-desnatado", []),
        new("iogurte-natural", "iogurte-natural", []),
        new("iogurte-natural-desnatado", "iogurte-natural-desnatado", []),
        new("queijo-minas-frescal", "queijo-minas-frescal", []),
        new("queijo-mucarela", "queijo-mucarela", []),
        new("queijo-prato", "queijo-prato", []),
        new("queijo-parmesao", "queijo-parmesao", []),
        new("ricota", "ricota", []),
        new("requeijao-cremoso", "requeijao", []),
        new("pasta-de-amendoim-integral", "pasta-de-amendoim", []),
        new("amendoim-torrado", "amendoim", []),
        new("castanha-de-caju", "castanha-de-caju", []),
        new("castanha-do-para", "castanha-do-para", []),
        new("amendoa-torrada", "amendoa", []),
        new("noz", "noz", []),
        new("linhaca", "linhaca", []),
        new("azeite-de-oliva-extra-virgem", "azeite", []),
        new("oleo-de-soja", "oleo-de-soja", []),
        new("manteiga", "manteiga", []),
        new("acucar-refinado", "acucar-refinado", []),
        new("acucar-mascavo", "acucar-mascavo", []),
        new("mel", "mel", []),
        // Receita da TACO (471): 400 g de pó para 4 litros de água.
        new("cafe-coado", "cafe-em-po", [new PassoReceita(Trecho(471, "1."), Trecho(471, "2."), 400m, 4000m)]),
        // Receitas da TACO (475 a 477): 20 g ou 20 sachês para 4 litros de água; a lista conta em sachês.
        new("cha-mate", "cha-mate", [new PassoReceita(Trecho(476, "1."), Trecho(476, "2."), 20m, 4000m)]),
        new("cha-preto", "cha-preto", [new PassoReceita(Trecho(477, "1."), Trecho(477, "2."), 20m, 4000m)]),
        new("cha-de-erva-doce", "cha-de-erva-doce", [new PassoReceita(Trecho(475, "1."), Trecho(475, "2."), 20m, 4000m)]),
        // Sem fonte para converter suco em laranjas: aparece pronto.
        new("suco-de-laranja-natural", "suco-de-laranja", []),
        new("agua-de-coco", "agua-de-coco", []),
        new("refrigerante-de-cola", "refrigerante-de-cola", []),
        new("refrigerante-de-guarana", "refrigerante-de-guarana", []),
        new("cerveja-pilsen", "cerveja-pilsen", []),
    ];
}

public enum SecaoMercado
{
    Hortifruti,
    AcougueEPeixaria,
    Padaria,
    FriosLaticiniosEOvos,
    Mercearia,
    Bebidas,
    Congelados
}

/// <summary>Em que a lista mostra a quantidade. Gramas e mililitros vão em passos de 10 até 100 e de 50 acima disso.</summary>
public enum UnidadeCompra
{
    Gramas,
    Mililitros,
    Unidades,
    Fatias,
    Saches
}

/// <summary>Uma linha da lista de compras.</summary>
/// <param name="Chave">Identificador estável: é o que a marcação "já tenho" guarda.</param>
/// <param name="UnidadeSingular">Para contáveis: "banana", na frase "1 banana".</param>
/// <param name="UnidadePlural">"bananas", na frase "7 bananas".</param>
/// <param name="Pronto">A quantidade é do prato pronto, sem conversão para o que se compra (etiqueta "pronto").</param>
/// <param name="NotaNaLinha">Texto curto que aparece na própria linha, não só no detalhe.</param>
public record ItemCompra(
    string Chave,
    string Nome,
    SecaoMercado Secao,
    UnidadeCompra Unidade,
    string? UnidadeSingular = null,
    string? UnidadePlural = null,
    bool Pronto = false,
    string? NotaNaLinha = null,
    IReadOnlyList<ApelidoRegional>? Apelidos = null)
{
    public IReadOnlyList<ApelidoRegional> Apelidos { get; init; } = Apelidos ?? [];
}

/// <summary>
/// Como um item da base vira o item de compra: a quantidade da dieta (g ou ml) vezes o Fator dá a
/// quantidade na unidade do item de compra. Sem passos = só soma.
/// </summary>
/// <param name="LevaOleoNoPreparo">Refogados e ovo frito: a lista avisa que o óleo do preparo não está na conta.</param>
/// <param name="Observacao">Texto para o "Ver detalhe".</param>
public record ConversaoItem(
    string AlimentoId,
    string ChaveCompra,
    IReadOnlyList<PassoConversao> Passos,
    bool LevaOleoNoPreparo = false,
    string? Observacao = null)
{
    public decimal Fator => Passos.Aggregate(1m, (fator, passo) => fator * passo.Fator);
}

/// <summary>Um passo da conta, com a fonte. O fator do item é o produto dos passos.</summary>
public abstract record PassoConversao
{
    public abstract decimal Fator { get; }
}

public enum NutrienteDoPar
{
    /// <summary>Grãos, massas e tubérculos.</summary>
    Carboidrato,

    /// <summary>Carnes, aves e peixes.</summary>
    Proteina,

    /// <summary>Hortaliças: 100 − umidade. Os nutrientes delas ficam abaixo de 5 g e variam demais entre amostras.</summary>
    MateriaSeca
}

/// <summary>
/// Cozido → cru pelo nutriente que não se perde no preparo: cru = cozido × (valor no cozido ÷ valor no
/// cru). Com TetoUm, o fator fica em no máximo 1 (calibração D1 das hortaliças).
/// </summary>
public sealed record PassoParCruCozido(ILinhaComposicao Cozido, ILinhaComposicao Cru, NutrienteDoPar Nutriente, bool TetoUm = false)
    : PassoConversao
{
    public decimal FatorDaFonte => Valor(Cozido) / Valor(Cru);

    public bool TetoAplicado => TetoUm && FatorDaFonte > 1m;

    public override decimal Fator => TetoAplicado ? 1m : FatorDaFonte;

    private decimal Valor(ILinhaComposicao linha) => Nutriente switch
    {
        NutrienteDoPar.Carboidrato => linha.Carboidrato,
        NutrienteDoPar.Proteina => linha.Proteina,
        _ => 100m - linha.Umidade
    };
}

/// <summary>Casca, osso, caroço: peso comprado = peso comestível ÷ (1 − descarte).</summary>
public sealed record PassoDescarte(LinhaUsda Fonte) : PassoConversao
{
    public override decimal Fator => 100m / (100m - Fonte.DescartePct);
}

/// <summary>Divide pelo peso de uma unidade: gramas → unidades (POF, ou USDA para a clara).</summary>
public sealed record PassoUnidade(decimal Gramas, string Fonte) : PassoConversao
{
    public override decimal Fator => 1m / Gramas;
}

/// <summary>Receita da TACO: quanto do ingrediente vai em quanto de água (café, chás).</summary>
public sealed record PassoReceita(TrechoTaco TrechoIngrediente, TrechoTaco TrechoAgua, decimal Quantidade, decimal MlDeAgua)
    : PassoConversao
{
    public override decimal Fator => Quantidade / MlDeAgua;
}

/// <summary>Número que não vem de tabela, com o motivo escrito.</summary>
public sealed record PassoCalibracao(decimal Valor, string Motivo) : PassoConversao
{
    public override decimal Fator => Valor;
}

/// <summary>
/// A derivação da tapioca da BaseNutricional, ao contrário: quanto de fécula vai em 1 g de tapioca. A
/// tapioca da TACO (551) leva manteiga (261); tirada a manteiga pela gordura, sobra a matéria seca da
/// tapioca sem recheio, que se divide pela da fécula (124).
/// </summary>
public sealed record PassoFeculaDaTapioca(LinhaTaco Tapioca, LinhaTaco Manteiga, LinhaTaco Fecula) : PassoConversao
{
    // Gramas de manteiga em 100 g de tapioca: toda a gordura da tapioca vem dela.
    public decimal Manteiga100g => 100m * Tapioca.Lipideos!.Value / Manteiga.Lipideos!.Value;

    public decimal MateriaSecaSemManteiga =>
        ((100m - Tapioca.Umidade) - Manteiga100g * (100m - Manteiga.Umidade) / 100m) / (100m - Manteiga100g);

    public override decimal Fator => MateriaSecaSemManteiga / ((100m - Fecula.Umidade) / 100m);
}

/// <summary>Uma linha de tabela com umidade, proteína e carboidrato por 100 g.</summary>
public interface ILinhaComposicao
{
    string Codigo { get; }
    string Nome { get; }
    decimal Umidade { get; }
    decimal Proteina { get; }
    decimal Carboidrato { get; }
}

/// <summary>Linha da Tabela 1 da TACO. Lipídeos nulo = "Tr" (traço).</summary>
public sealed record LinhaTaco(int Numero, string Nome, int Pagina, decimal Umidade, decimal Proteina, decimal? Lipideos, decimal Carboidrato)
    : ILinhaComposicao
{
    public string Codigo => Numero.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Linha do SR Legacy (SR28). Umidade = água (nutriente 255).</summary>
public sealed record LinhaUsda(string Ndb, string FdcId, string Nome, decimal Umidade, decimal Proteina, decimal Carboidrato,
    int DescartePct, string DescarteDescricao, string? Unidade, decimal? GramasUnidade) : ILinhaComposicao
{
    public string Codigo => Ndb;
}

/// <summary>Uma linha de texto da TACO, como está no PDF.</summary>
public sealed record TrechoTaco(int Codigo, int Pagina, string Texto);
