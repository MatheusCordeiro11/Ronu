namespace Ronu.Api.Data;

/// <summary>
/// Base nutricional própria: a lista fechada de alimentos que o Gemini poderá
/// usar no cardápio (fase 2) e os valores de cada um por 100 g, a partir dos
/// quais o C# calculará kcal e macros (fase 3), no lugar dos números que o
/// Gemini declara. Por enquanto só os dados: nada no código ainda usa a lista.
///
/// Fontes (citações completas em docs/fontes-nutricionais.md):
/// - TACO: NEPA/UNICAMP, Tabela Brasileira de Composição de Alimentos, 4ª ed.
///   rev. e ampl., 2011. Valores conferidos contra o PDF oficial (Tabela 1),
///   não contra transcrições. Código = número do alimento na TACO.
/// - USDA: FoodData Central, SR Legacy (domínio público, CC0), só para o que a
///   TACO não tem. Código = fdcId.
/// - DERIVADO: valor de outro item da TACO, com a justificativa na Observacao.
/// - Peso por unidade: IBGE, POF 2008-2009, Tabela de Medidas Referidas para os
///   Alimentos Consumidos no Brasil ("unidade média").
///
/// Kcal, proteína, carboidrato e gordura são por 100 g. Para os itens medidos
/// em "ml" (bebidas), o valor por 100 g é usado como 100 ml. O carboidrato da
/// TACO e do USDA é "por diferença" e inclui as fibras.
///
/// A porção máxima (o maior valor plausível numa refeição) é calibração do
/// projeto, não valor de tabela.
///
/// Ficam de fora os itens de composição incerta, que variam demais entre
/// marcas e receitas (granola, bolo, molhos industrializados, whey protein,
/// embutidos como peito de peru e presunto), e as preparações com vários
/// ingredientes (salada, vitamina, omelete), que o cardápio monta item a item.
///
/// Para mudar um valor: confira na fonte, altere aqui e incremente VersaoBase.
/// </summary>
public static class BaseNutricional
{
    /// <summary>
    /// Versão dos valores. Cada dieta guardará a versão usada no cálculo, para
    /// que uma mudança aqui não reescreva o passado.
    /// </summary>
    public const int VersaoBase = 1;

    /// <summary>
    /// Limite da lista: o enum do schema do Gemini passou com até 180 nomes na
    /// fase 0 (2026-10-07); 150 deixa folga. O teto vale para nomes e apelidos
    /// juntos, que é o que vai para o enum.
    /// </summary>
    public const int TetoDeNomes = 150;

    public static IReadOnlyList<AlimentoBase> Itens { get; } =
    [
        new("arroz-branco-cozido", "Arroz branco cozido", CategoriaAlimento.CereaisEMassas, "g", FonteNutricional.Taco, "3", "Arroz, tipo 1, cozido", 128m, 2.5m, 28.1m, 0.2m, true, 400m, Alergenos.Nenhum,
            Observacao: "Arroz tipo 1. A TACO cozinha sem sal e sem óleo: o óleo do preparo caseiro não está incluído."),
        new("arroz-integral-cozido", "Arroz integral cozido", CategoriaAlimento.CereaisEMassas, "g", FonteNutricional.Taco, "1", "Arroz, integral, cozido", 124m, 2.6m, 25.8m, 1m, true, 400m, Alergenos.Nenhum,
            Observacao: "A TACO cozinha sem sal e sem óleo."),
        new("aveia-em-flocos", "Aveia em flocos", CategoriaAlimento.CereaisEMassas, "g", FonteNutricional.Taco, "7", "Aveia, flocos, crua", 394m, 13.9m, 66.6m, 8.5m, true, 80m, Alergenos.Gluten,
            Observacao: "Glúten marcado por precaução: contaminação cruzada (a aveia comum no Brasil não é certificada sem glúten)."),
        new("macarrao-cozido", "Macarrão cozido", CategoriaAlimento.CereaisEMassas, "g", FonteNutricional.Usda, "169737", "Pasta, cooked, enriched, without added salt", 158m, 5.8m, 30.9m, 0.93m, true, 400m, Alergenos.Gluten | Alergenos.Ovo,
            Observacao: "A TACO só tem o macarrão cru. Ovo marcado por precaução: macarrão com ovos é comum no Brasil."),
        new("cuscuz-de-milho-cozido", "Cuscuz de milho cozido", CategoriaAlimento.CereaisEMassas, "g", FonteNutricional.Taco, "533", "Cuscuz, de milho, cozido com sal", 113m, 2.2m, 25.3m, 0.7m, true, 300m, Alergenos.Gluten,
            Observacao: "Glúten marcado por precaução: flocos de milho podem ter contaminação cruzada."),
        new("milho-verde-em-conserva", "Milho verde em conserva", CategoriaAlimento.CereaisEMassas, "g", FonteNutricional.Taco, "45", "Milho, verde, enlatado, drenado", 98m, 3.2m, 17.1m, 2.4m, true, 150m, Alergenos.Nenhum,
            Observacao: "Drenado."),
        new("tapioca", "Tapioca", CategoriaAlimento.CereaisEMassas, "g", FonteNutricional.Derivado, "124", "Fécula, de mandioca", 297m, 0.4m, 72.7m, 0.3m, true, 150m, Alergenos.Nenhum,
            Observacao: "DERIVADO: tapioca pronta, sem recheio, da fécula de mandioca da TACO (124) hidratada. A hidratação vem da tapioca medida pela própria TACO (551, \"Tapioca, com manteiga\", 24,9% de umidade): tirada a manteiga (10,9 g de gordura em 100 g = 13,2% de manteiga), a tapioca tem 73,7% de matéria seca, contra 82,2% da fécula. Logo, 100 g de tapioca = 89,7 g de fécula + 10,3 g de água (11,5 g de água para cada 100 g de fécula). Tirar a manteiga direto do 551 dá 290 kcal e 73,3 g de carboidrato, perto deste valor."),
        new("farinha-de-mandioca-torrada", "Farinha de mandioca torrada", CategoriaAlimento.CereaisEMassas, "g", FonteNutricional.Taco, "122", "Farinha, de mandioca, torrada", 365m, 1.2m, 89.2m, 0.3m, false, 50m, Alergenos.Nenhum),
        new("pao-frances", "Pão francês", CategoriaAlimento.Paes, "g", FonteNutricional.Taco, "53", "Pão, trigo, francês", 300m, 8m, 58.6m, 3.1m, false, 150m, Alergenos.Gluten,
            PesoUnidade: new(50m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Pão francês - unidade")),
        new("pao-de-forma-integral", "Pão de forma integral", CategoriaAlimento.Paes, "g", FonteNutricional.Taco, "52", "Pão, trigo, forma, integral", 253m, 9.4m, 49.9m, 3.7m, false, 100m, Alergenos.Gluten | Alergenos.Lactose,
            PesoUnidade: new(25m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Pão de forma de aveia - unidade (fatia)"),
            Observacao: "Lactose marcada por precaução: pães de forma industrializados costumam ter leite ou soro de leite."),
        new("pao-de-queijo-assado", "Pão de queijo assado", CategoriaAlimento.Paes, "g", FonteNutricional.Taco, "140", "Pão, de queijo, assado", 363m, 5.1m, 34.2m, 24.6m, false, 120m, Alergenos.Lactose | Alergenos.Ovo | Alergenos.Gluten,
            PesoUnidade: new(20m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Pão de queijo - unidade média"),
            Observacao: "Glúten marcado por precaução: versões industrializadas podem conter ou ter contaminação cruzada."),
        new("feijao-cozido", "Feijão cozido", CategoriaAlimento.Leguminosas, "g", FonteNutricional.Taco, "561", "Feijão, carioca, cozido", 76m, 4.8m, 13.6m, 0.5m, true, 300m, Alergenos.Nenhum,
            Observacao: "Feijão carioca. A TACO cozinha só com água (sem tempero nem óleo)."),
        new("feijao-preto-cozido", "Feijão preto cozido", CategoriaAlimento.Leguminosas, "g", FonteNutricional.Taco, "567", "Feijão, preto, cozido", 77m, 4.5m, 14m, 0.5m, true, 300m, Alergenos.Nenhum,
            Observacao: "A TACO cozinha só com água (sem tempero nem óleo)."),
        new("feijao-fradinho-cozido", "Feijão fradinho cozido", CategoriaAlimento.Leguminosas, "g", FonteNutricional.Taco, "563", "Feijão, fradinho, cozido", 78m, 5.1m, 13.5m, 0.6m, true, 300m, Alergenos.Nenhum),
        new("lentilha-cozida", "Lentilha cozida", CategoriaAlimento.Leguminosas, "g", FonteNutricional.Taco, "577", "Lentilha, cozida", 93m, 6.3m, 16.3m, 0.5m, true, 300m, Alergenos.Nenhum),
        new("batata-inglesa-cozida", "Batata inglesa cozida", CategoriaAlimento.TuberculosERaizes, "g", FonteNutricional.Taco, "91", "Batata, inglesa, cozida", 52m, 1.2m, 11.9m, 0m, true, 400m, Alergenos.Nenhum,
            Observacao: "O valor da TACO (52 kcal) fica bem abaixo do USDA para batata cozida (~87 kcal); mantido por ser a fonte oficial."),
        new("batata-doce-cozida", "Batata doce cozida", CategoriaAlimento.TuberculosERaizes, "g", FonteNutricional.Taco, "88", "Batata, doce, cozida", 77m, 0.6m, 18.4m, 0.1m, true, 400m, Alergenos.Nenhum),
        new("mandioca-cozida", "Mandioca cozida", CategoriaAlimento.TuberculosERaizes, "g", FonteNutricional.Taco, "129", "Mandioca, cozida", 125m, 0.6m, 30.1m, 0.3m, true, 300m, Alergenos.Nenhum,
            Apelidos: [new("Aipim cozido", ["RJ"]), new("Macaxeira cozida", ["AM", "PA", "AC", "RO", "RR", "AP", "TO", "MA", "PI", "CE", "RN", "PB", "PE", "AL", "SE", "BA"])]),
        new("mandioquinha-cozida", "Mandioquinha cozida", CategoriaAlimento.TuberculosERaizes, "g", FonteNutricional.Taco, "86", "Batata, baroa, cozida", 80m, 0.9m, 18.9m, 0.2m, true, 300m, Alergenos.Nenhum,
            Observacao: "Batata baroa, na TACO."),
        new("cara-cozido", "Cará cozido", CategoriaAlimento.TuberculosERaizes, "g", FonteNutricional.Taco, "102", "Cará, cozido", 78m, 1.5m, 18.9m, 0.1m, true, 300m, Alergenos.Nenhum),
        new("alface-crua", "Alface crua", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "78", "Alface, crespa, crua", 11m, 1.3m, 1.7m, 0.2m, false, 150m, Alergenos.Nenhum,
            Observacao: "Alface crespa."),
        new("rucula-crua", "Rúcula crua", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "152", "Rúcula, crua", 13m, 1.8m, 2.2m, 0.1m, false, 100m, Alergenos.Nenhum),
        new("agriao-cru", "Agrião cru", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "75", "Agrião, cru", 17m, 2.7m, 2.3m, 0.2m, false, 100m, Alergenos.Nenhum),
        new("couve-crua", "Couve crua", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "115", "Couve, manteiga, crua", 27m, 2.9m, 4.3m, 0.5m, false, 100m, Alergenos.Nenhum,
            Observacao: "Couve manteiga."),
        new("tomate-cru", "Tomate cru", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "157", "Tomate, com semente, cru", 15m, 1.1m, 3.1m, 0.2m, false, 200m, Alergenos.Nenhum),
        new("pepino-cru", "Pepino cru", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "142", "Pepino, cru", 10m, 0.9m, 2m, 0m, false, 200m, Alergenos.Nenhum),
        new("cenoura-crua", "Cenoura crua", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "110", "Cenoura, crua", 34m, 1.3m, 7.7m, 0.2m, false, 150m, Alergenos.Nenhum),
        new("cenoura-cozida", "Cenoura cozida", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "109", "Cenoura, cozida", 30m, 0.8m, 6.7m, 0.2m, false, 200m, Alergenos.Nenhum),
        new("beterraba-crua", "Beterraba crua", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "98", "Beterraba, crua", 49m, 1.9m, 11.1m, 0.1m, false, 150m, Alergenos.Nenhum),
        new("beterraba-cozida", "Beterraba cozida", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "97", "Beterraba, cozida", 32m, 1.3m, 7.2m, 0.1m, false, 200m, Alergenos.Nenhum),
        new("brocolis-cozido", "Brócolis cozido", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "100", "Brócolis, cozido", 25m, 2.1m, 4.4m, 0.5m, false, 200m, Alergenos.Nenhum),
        new("couve-flor-cozida", "Couve-flor cozida", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "118", "Couve-flor, cozida", 19m, 1.2m, 3.9m, 0.3m, false, 200m, Alergenos.Nenhum),
        new("abobrinha-cozida", "Abobrinha cozida", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "70", "Abobrinha, italiana, cozida", 15m, 1.1m, 3m, 0.2m, false, 200m, Alergenos.Nenhum,
            Observacao: "Abobrinha italiana."),
        new("abobrinha-refogada", "Abobrinha refogada", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "72", "Abobrinha, italiana, refogada", 24m, 1.1m, 4.2m, 0.8m, false, 200m, Alergenos.Nenhum,
            Observacao: "Abobrinha italiana. O óleo do refogado já está no valor da TACO."),
        new("chuchu-cozido", "Chuchu cozido", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "112", "Chuchu, cozido", 19m, 0.4m, 4.8m, 0m, false, 200m, Alergenos.Nenhum),
        new("couve-refogada", "Couve refogada", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "116", "Couve, manteiga, refogada", 90m, 1.7m, 8.7m, 6.6m, false, 150m, Alergenos.Nenhum,
            Observacao: "Couve manteiga. O óleo do refogado já está no valor da TACO (6,6 g de gordura em 100 g)."),
        new("espinafre-refogado", "Espinafre refogado", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "120", "Espinafre, Nova Zelândia, refogado", 67m, 2.7m, 4.2m, 5.4m, false, 150m, Alergenos.Nenhum,
            Observacao: "Espinafre Nova Zelândia. O óleo do refogado já está no valor da TACO."),
        new("repolho-cru", "Repolho cru", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "149", "Repolho, branco, cru", 17m, 0.9m, 3.9m, 0.1m, false, 150m, Alergenos.Nenhum,
            Observacao: "Repolho branco."),
        new("repolho-roxo-cru", "Repolho roxo cru", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "150", "Repolho, roxo, cru", 31m, 1.9m, 7.2m, 0.1m, false, 150m, Alergenos.Nenhum),
        new("acelga-crua", "Acelga crua", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "74", "Acelga, crua", 21m, 1.4m, 4.6m, 0.1m, false, 150m, Alergenos.Nenhum),
        new("cebola-crua", "Cebola crua", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "107", "Cebola, crua", 39m, 1.7m, 8.9m, 0.1m, false, 100m, Alergenos.Nenhum),
        new("abobora-cozida", "Abóbora cozida", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "64", "Abóbora, cabotian, cozida", 48m, 1.4m, 10.8m, 0.7m, true, 250m, Alergenos.Nenhum,
            Observacao: "Abóbora cabotiá."),
        new("berinjela-cozida", "Berinjela cozida", CategoriaAlimento.Hortalicas, "g", FonteNutricional.Taco, "95", "Berinjela, cozida", 19m, 0.7m, 4.5m, 0.1m, false, 200m, Alergenos.Nenhum),
        new("banana-prata", "Banana prata", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "182", "Banana, prata, crua", 98m, 1.3m, 26m, 0.1m, false, 225m, Alergenos.Nenhum,
            PesoUnidade: new(75m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Banana-prata - unidade")),
        new("banana-nanica", "Banana nanica", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "179", "Banana, nanica, crua", 92m, 1.4m, 23.8m, 0.1m, true, 250m, Alergenos.Nenhum,
            Observacao: "Sem peso por unidade: o IBGE só traz a unidade da banana-prata."),
        new("maca-fuji", "Maçã fuji", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "222", "Maçã, Fuji, com casca, crua", 56m, 0.3m, 15.2m, 0m, false, 300m, Alergenos.Nenhum,
            PesoUnidade: new(150m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Maçã - unidade média"),
            Observacao: "Com casca."),
        new("pera-williams", "Pera williams", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "243", "Pêra, Williams, crua", 53m, 0.6m, 14m, 0.1m, false, 260m, Alergenos.Nenhum,
            PesoUnidade: new(130m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: pera - unidade média")),
        new("laranja-pera", "Laranja pera", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "214", "Laranja, pêra, crua", 37m, 1m, 8.9m, 0.1m, false, 360m, Alergenos.Nenhum,
            PesoUnidade: new(180m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Laranja - unidade média")),
        new("tangerina", "Tangerina", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "251", "Tangerina, Poncã, crua", 38m, 0.8m, 9.6m, 0.1m, false, 270m, Alergenos.Nenhum,
            PesoUnidade: new(135m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Tangerina - unidade média"),
            Observacao: "Tangerina poncã."),
        new("kiwi", "Kiwi", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "207", "Kiwi, cru", 51m, 1.3m, 11.5m, 0.6m, false, 188m, Alergenos.Nenhum,
            PesoUnidade: new(94m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Kiwi - unidade média")),
        new("mamao-papaia", "Mamão papaia", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "226", "Mamão, Papaia, cru", 40m, 0.5m, 10.4m, 0.1m, true, 300m, Alergenos.Nenhum),
        new("mamao-formosa", "Mamão formosa", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "225", "Mamão, Formosa, cru", 45m, 0.8m, 11.6m, 0.1m, true, 300m, Alergenos.Nenhum),
        new("abacaxi", "Abacaxi", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "164", "Abacaxi, cru", 48m, 0.9m, 12.3m, 0.1m, true, 300m, Alergenos.Nenhum),
        new("melancia", "Melancia", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "235", "Melancia, crua", 33m, 0.9m, 8.1m, 0m, true, 400m, Alergenos.Nenhum),
        new("melao", "Melão", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "236", "Melão, cru", 29m, 0.7m, 7.5m, 0m, true, 300m, Alergenos.Nenhum),
        new("morango", "Morango", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "239", "Morango, cru", 30m, 0.9m, 6.8m, 0.3m, true, 250m, Alergenos.Nenhum),
        new("uva-italia", "Uva itália", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "256", "Uva, Itália, crua", 53m, 0.7m, 13.6m, 0.2m, true, 200m, Alergenos.Nenhum),
        new("manga-palmer", "Manga palmer", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "229", "Manga, Palmer, crua", 72m, 0.4m, 19.4m, 0.2m, true, 300m, Alergenos.Nenhum,
            Observacao: "Sem peso por unidade: o IBGE traz a manga espada, outra variedade."),
        new("abacate", "Abacate", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "163", "Abacate, cru", 96m, 1.2m, 6m, 8.4m, true, 200m, Alergenos.Nenhum),
        new("goiaba-vermelha", "Goiaba vermelha", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "200", "Goiaba, vermelha, com casca, crua", 54m, 1.1m, 13m, 0.4m, true, 250m, Alergenos.Nenhum,
            Observacao: "Com casca."),
        new("acai-sem-acucar", "Açaí sem açúcar", CategoriaAlimento.Frutas, "g", FonteNutricional.Taco, "168", "Açaí, polpa, congelada", 58m, 0.8m, 6.2m, 3.9m, true, 300m, Alergenos.Nenhum,
            Observacao: "Polpa congelada pura, sem xarope de guaraná."),
        new("patinho-bovino-grelhado", "Patinho bovino grelhado", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "377", "Carne, bovina, patinho, sem gordura, grelhado", 219m, 35.9m, 0m, 7.3m, true, 300m, Alergenos.Nenhum,
            Observacao: "Sem gordura."),
        new("patinho-bovino-moido-refogado", "Patinho bovino moído refogado", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Derivado, "377", "Carne, bovina, patinho, sem gordura, grelhado", 219m, 35.9m, 0m, 7.3m, true, 300m, Alergenos.Nenhum,
            Observacao: "DERIVADO: a TACO não tem patinho moído; usa o valor do patinho sem gordura grelhado (mesmo corte). O óleo do refogado não está incluído."),
        new("carne-moida-de-acem-cozida", "Carne moída de acém cozida", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "326", "Carne, bovina, acém, moído, cozido", 212m, 26.7m, 0m, 10.9m, true, 300m, Alergenos.Nenhum),
        new("acem-bovino-cozido", "Acém bovino cozido", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "328", "Carne, bovina, acém, sem gordura, cozido", 215m, 27.3m, 0m, 10.9m, true, 300m, Alergenos.Nenhum,
            Observacao: "Sem gordura."),
        new("alcatra-bovina-grelhada", "Alcatra bovina grelhada", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "370", "Carne, bovina, miolo de alcatra, sem gordura, g relhado", 241m, 31.9m, 0m, 11.6m, true, 300m, Alergenos.Nenhum,
            Observacao: "Miolo de alcatra sem gordura."),
        new("contrafile-bovino-grelhado", "Contrafilé bovino grelhado", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "346", "Carne, bovina, contra-filé, sem gordura, grelhado", 194m, 35.9m, 0m, 4.5m, true, 300m, Alergenos.Nenhum,
            Observacao: "Sem gordura."),
        new("coxao-mole-bovino-cozido", "Coxão mole bovino cozido", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "351", "Carne, bovina, coxão mole, sem gordura, cozido", 219m, 32.4m, 0m, 8.9m, true, 300m, Alergenos.Nenhum,
            Observacao: "Sem gordura."),
        new("lagarto-bovino-cozido", "Lagarto bovino cozido", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "363", "Carne, bovina, lagarto, cozido", 222m, 32.9m, 0m, 9.1m, true, 300m, Alergenos.Nenhum),
        new("musculo-bovino-cozido", "Músculo bovino cozido", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "371", "Carne, bovina, músculo, sem gordura, cozido", 194m, 31.2m, 0m, 6.7m, true, 300m, Alergenos.Nenhum,
            Observacao: "Sem gordura."),
        new("maminha-bovina-grelhada", "Maminha bovina grelhada", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "368", "Carne, bovina, maminha, grelhada", 153m, 30.7m, 0m, 2.4m, true, 300m, Alergenos.Nenhum),
        new("file-mignon-grelhado", "Filé mignon grelhado", CategoriaAlimento.CarneBovina, "g", FonteNutricional.Taco, "358", "Carne, bovina, filé mingnon, sem gordura, grelhado", 220m, 32.8m, 0m, 8.8m, true, 300m, Alergenos.Nenhum,
            Observacao: "Sem gordura."),
        new("peito-de-frango-sem-pele-grelhado", "Peito de frango sem pele grelhado", CategoriaAlimento.Aves, "g", FonteNutricional.Taco, "410", "Frango, peito, sem pele, grelhado", 159m, 32m, 0m, 2.5m, true, 300m, Alergenos.Nenhum),
        new("peito-de-frango-sem-pele-cozido", "Peito de frango sem pele cozido", CategoriaAlimento.Aves, "g", FonteNutricional.Taco, "408", "Frango, peito, sem pele, cozido", 163m, 31.5m, 0m, 3.2m, true, 300m, Alergenos.Nenhum,
            Observacao: "Serve também para o frango desfiado."),
        new("coxa-de-frango-sem-pele-cozida", "Coxa de frango sem pele cozida", CategoriaAlimento.Aves, "g", FonteNutricional.Taco, "398", "Frango, coxa, sem pele, cozida", 167m, 26.9m, 0m, 5.8m, true, 300m, Alergenos.Nenhum),
        new("sobrecoxa-de-frango-sem-pele-assada", "Sobrecoxa de frango sem pele assada", CategoriaAlimento.Aves, "g", FonteNutricional.Taco, "413", "Frango, sobrecoxa, sem pele, assada", 233m, 29.2m, 0m, 12m, true, 300m, Alergenos.Nenhum),
        new("frango-sem-pele-assado", "Frango sem pele assado", CategoriaAlimento.Aves, "g", FonteNutricional.Taco, "403", "Frango, inteiro, sem pele, assado", 187m, 28m, 0m, 7.5m, true, 300m, Alergenos.Nenhum,
            Observacao: "Frango inteiro."),
        new("lombo-suino-assado", "Lombo suíno assado", CategoriaAlimento.CarneSuina, "g", FonteNutricional.Taco, "432", "Porco, lombo, assado", 210m, 35.7m, 0m, 6.4m, true, 300m, Alergenos.Nenhum),
        new("bisteca-suina-grelhada", "Bisteca suína grelhada", CategoriaAlimento.CarneSuina, "g", FonteNutricional.Taco, "429", "Porco, bisteca, grelhada", 280m, 28.9m, 0m, 17.4m, true, 300m, Alergenos.Nenhum),
        new("pernil-suino-assado", "Pernil suíno assado", CategoriaAlimento.CarneSuina, "g", FonteNutricional.Taco, "435", "Porco, pernil, assado", 262m, 32.1m, 0m, 13.9m, true, 300m, Alergenos.Nenhum),
        new("file-de-tilapia-grelhado", "Filé de tilápia grelhado", CategoriaAlimento.Peixes, "g", FonteNutricional.Usda, "175177", "Fish, tilapia, cooked, dry heat", 128m, 26.2m, 0m, 2.65m, true, 300m, Alergenos.Peixe,
            Observacao: "A TACO não tem tilápia. \"Dry heat\" é grelhado ou assado."),
        new("salmao-grelhado", "Salmão grelhado", CategoriaAlimento.Peixes, "g", FonteNutricional.Taco, "317", "Salmão, sem pele, fresco, grelhado", 243m, 26.1m, 0m, 14.5m, true, 250m, Alergenos.Peixe,
            Observacao: "Sem pele."),
        new("sardinha-assada", "Sardinha assada", CategoriaAlimento.Peixes, "g", FonteNutricional.Taco, "318", "Sardinha, assada", 164m, 32.2m, 0m, 3m, true, 250m, Alergenos.Peixe),
        new("atum-em-conserva-em-agua", "Atum em conserva em água", CategoriaAlimento.Peixes, "g", FonteNutricional.Usda, "171986", "Fish, tuna, light, canned in water, without salt, drained solids", 116m, 25.5m, 0m, 0.82m, true, 200m, Alergenos.Peixe,
            Observacao: "A TACO só tem o atum em óleo. Drenado; o sal não muda os macros."),
        new("merluza-assada", "Merluza assada", CategoriaAlimento.Peixes, "g", FonteNutricional.Taco, "301", "Merluza, filé, assado", 122m, 26.6m, 0m, 0.9m, true, 300m, Alergenos.Peixe,
            Observacao: "Filé."),
        new("corvina-assada", "Corvina assada", CategoriaAlimento.Peixes, "g", FonteNutricional.Taco, "293", "Corvina grande, assada", 147m, 26.8m, 0m, 3.6m, true, 300m, Alergenos.Peixe),
        new("ovo-de-galinha-cozido", "Ovo de galinha cozido", CategoriaAlimento.Ovos, "g", FonteNutricional.Taco, "488", "Ovo, de galinha, inteiro, cozido/10minutos", 146m, 13.3m, 0.6m, 9.5m, false, 270m, Alergenos.Ovo,
            PesoUnidade: new(45m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Ovo de galinha cozido - unidade média")),
        new("ovo-de-galinha-frito", "Ovo de galinha frito", CategoriaAlimento.Ovos, "g", FonteNutricional.Taco, "490", "Ovo, de galinha, inteiro, frito", 240m, 15.6m, 1.2m, 18.6m, false, 200m, Alergenos.Ovo,
            PesoUnidade: new(50m, "IBGE, POF 2008-2009, Tabela de Medidas Referidas: Ovo de galinha frito - unidade média"),
            Observacao: "O óleo da fritura já está no valor da TACO."),
        new("clara-de-ovo-cozida", "Clara de ovo cozida", CategoriaAlimento.Ovos, "g", FonteNutricional.Taco, "486", "Ovo, de galinha, clara, cozida/10minutos", 59m, 13.4m, 0m, 0.1m, true, 300m, Alergenos.Ovo),
        new("leite-integral", "Leite integral", CategoriaAlimento.Laticinios, "ml", FonteNutricional.Usda, "171265", "Milk, whole, 3.25% milkfat, with added vitamin D", 61m, 3.15m, 4.8m, 3.25m, false, 500m, Alergenos.Lactose,
            Observacao: "A TACO não tem os valores do leite fluido (\"as análises estão sendo reavaliadas\"). Valor por 100 g, usado como 100 ml."),
        new("leite-desnatado", "Leite desnatado", CategoriaAlimento.Laticinios, "ml", FonteNutricional.Usda, "171269", "Milk, nonfat, fluid, with added vitamin A and vitamin D (fat free or skim)", 34m, 3.37m, 4.96m, 0.08m, false, 500m, Alergenos.Lactose,
            Observacao: "A TACO não tem os valores do leite fluido (\"as análises estão sendo reavaliadas\"). Valor por 100 g, usado como 100 ml."),
        new("iogurte-natural", "Iogurte natural", CategoriaAlimento.Laticinios, "g", FonteNutricional.Taco, "448", "Iogurte, natural", 51m, 4.1m, 1.9m, 3m, true, 300m, Alergenos.Lactose,
            Observacao: "Integral. O carboidrato da TACO (1,9 g) é baixo para iogurte natural; mantido por ser a fonte oficial."),
        new("iogurte-natural-desnatado", "Iogurte natural desnatado", CategoriaAlimento.Laticinios, "g", FonteNutricional.Taco, "449", "Iogurte, natural, desnatado", 41m, 3.8m, 5.8m, 0.3m, true, 300m, Alergenos.Lactose),
        new("queijo-minas-frescal", "Queijo minas frescal", CategoriaAlimento.Laticinios, "g", FonteNutricional.Taco, "461", "Queijo, minas, frescal", 264m, 17.4m, 3.2m, 20.2m, true, 100m, Alergenos.Lactose),
        new("queijo-mucarela", "Queijo muçarela", CategoriaAlimento.Laticinios, "g", FonteNutricional.Taco, "463", "Queijo, mozarela", 330m, 22.6m, 3m, 25.2m, true, 80m, Alergenos.Lactose),
        new("queijo-prato", "Queijo prato", CategoriaAlimento.Laticinios, "g", FonteNutricional.Taco, "467", "Queijo, prato", 360m, 22.7m, 1.9m, 29.1m, true, 80m, Alergenos.Lactose),
        new("queijo-parmesao", "Queijo parmesão", CategoriaAlimento.Laticinios, "g", FonteNutricional.Taco, "464", "Queijo, parmesão", 453m, 35.6m, 1.7m, 33.5m, false, 30m, Alergenos.Lactose),
        new("ricota", "Ricota", CategoriaAlimento.Laticinios, "g", FonteNutricional.Taco, "469", "Queijo, ricota", 140m, 12.6m, 3.8m, 8.1m, true, 150m, Alergenos.Lactose),
        new("requeijao-cremoso", "Requeijão cremoso", CategoriaAlimento.Laticinios, "g", FonteNutricional.Taco, "468", "Queijo, requeijão, cremoso", 257m, 9.6m, 2.4m, 23.4m, false, 60m, Alergenos.Lactose),
        new("pasta-de-amendoim-integral", "Pasta de amendoim integral", CategoriaAlimento.OleaginosasESementes, "g", FonteNutricional.Usda, "172470", "Peanut butter, smooth style, without salt", 598m, 22.2m, 22.3m, 51.4m, false, 40m, Alergenos.Amendoim,
            Observacao: "100% amendoim, sem açúcar nem sal."),
        new("amendoim-torrado", "Amendoim torrado", CategoriaAlimento.OleaginosasESementes, "g", FonteNutricional.Taco, "558", "Amendoim, torrado, salgado", 606m, 22.5m, 18.7m, 54m, false, 50m, Alergenos.Amendoim,
            Observacao: "Torrado e salgado."),
        new("castanha-de-caju", "Castanha-de-caju", CategoriaAlimento.OleaginosasESementes, "g", FonteNutricional.Taco, "588", "Castanha-de-caju, torrada, salgada", 570m, 18.5m, 29.1m, 46.3m, false, 50m, Alergenos.Amendoim,
            Observacao: "Torrada e salgada. Amendoim marcado por precaução: beneficiamento comum (\"pode conter amendoim\")."),
        new("castanha-do-para", "Castanha-do-pará", CategoriaAlimento.OleaginosasESementes, "g", FonteNutricional.Taco, "589", "Castanha-do-Brasil, crua", 643m, 14.5m, 15.1m, 63.5m, false, 40m, Alergenos.Amendoim,
            Observacao: "Amendoim marcado por precaução: beneficiamento comum."),
        new("amendoa-torrada", "Amêndoa torrada", CategoriaAlimento.OleaginosasESementes, "g", FonteNutricional.Taco, "587", "Amêndoa, torrada, salgada", 581m, 18.6m, 29.5m, 47.3m, false, 50m, Alergenos.Amendoim,
            Observacao: "Torrada e salgada. Amendoim marcado por precaução: beneficiamento comum."),
        new("noz", "Noz", CategoriaAlimento.OleaginosasESementes, "g", FonteNutricional.Taco, "597", "Noz, crua", 620m, 14m, 18.4m, 59.4m, false, 40m, Alergenos.Amendoim,
            Observacao: "Amendoim marcado por precaução: beneficiamento comum."),
        new("linhaca", "Linhaça", CategoriaAlimento.OleaginosasESementes, "g", FonteNutricional.Taco, "594", "Linhaça, semente", 495m, 14.1m, 43.3m, 32.3m, false, 30m, Alergenos.Nenhum,
            Observacao: "Semente."),
        new("azeite-de-oliva-extra-virgem", "Azeite de oliva extra virgem", CategoriaAlimento.OleosEGorduras, "g", FonteNutricional.Taco, "260", "Azeite, de oliva, extra virgem", 884m, 0m, 0m, 100m, false, 20m, Alergenos.Nenhum,
            Observacao: "Proteína e carboidrato \"NA\" (não se aplica) na TACO, contados como 0."),
        new("oleo-de-soja", "Óleo de soja", CategoriaAlimento.OleosEGorduras, "g", FonteNutricional.Taco, "272", "Óleo, de soja", 884m, 0m, 0m, 100m, false, 15m, Alergenos.Nenhum,
            Observacao: "Proteína e carboidrato \"NA\" (não se aplica) na TACO, contados como 0."),
        new("manteiga", "Manteiga", CategoriaAlimento.OleosEGorduras, "g", FonteNutricional.Taco, "261", "Manteiga, com sal", 726m, 0.4m, 0.1m, 82.4m, false, 20m, Alergenos.Lactose,
            Observacao: "Com sal."),
        new("acucar-refinado", "Açúcar refinado", CategoriaAlimento.AcucaresEMel, "g", FonteNutricional.Taco, "494", "Açúcar, refinado", 387m, 0.3m, 99.5m, 0m, false, 20m, Alergenos.Nenhum),
        new("acucar-mascavo", "Açúcar mascavo", CategoriaAlimento.AcucaresEMel, "g", FonteNutricional.Taco, "493", "Açúcar, mascavo", 369m, 0.8m, 94.5m, 0.1m, false, 20m, Alergenos.Nenhum),
        new("mel", "Mel", CategoriaAlimento.AcucaresEMel, "g", FonteNutricional.Taco, "507", "Mel, de abelha", 309m, 0m, 84m, 0m, false, 30m, Alergenos.Nenhum),
        new("cafe-coado", "Café coado", CategoriaAlimento.Bebidas, "ml", FonteNutricional.Taco, "471", "Café, infusão 10%", 9m, 0.7m, 1.5m, 0.1m, false, 300m, Alergenos.Nenhum,
            Observacao: "Sem açúcar (infusão a 10%). Valor por 100 g, usado como 100 ml."),
        new("cha-mate", "Chá mate", CategoriaAlimento.Bebidas, "ml", FonteNutricional.Taco, "476", "Chá, mate, infusão 5%", 3m, 0m, 0.6m, 0.1m, false, 400m, Alergenos.Nenhum,
            Observacao: "Sem açúcar (infusão a 5%)."),
        new("cha-preto", "Chá preto", CategoriaAlimento.Bebidas, "ml", FonteNutricional.Taco, "477", "Chá, preto, infusão 5%", 2m, 0m, 0.6m, 0m, false, 400m, Alergenos.Nenhum,
            Observacao: "Sem açúcar (infusão a 5%)."),
        new("cha-de-erva-doce", "Chá de erva-doce", CategoriaAlimento.Bebidas, "ml", FonteNutricional.Taco, "475", "Chá, erva-doce, infusão 5%", 1m, 0m, 0.4m, 0m, false, 400m, Alergenos.Nenhum,
            Observacao: "Sem açúcar (infusão a 5%). Sem cafeína."),
        new("suco-de-laranja-natural", "Suco de laranja natural", CategoriaAlimento.Bebidas, "ml", FonteNutricional.Taco, "215", "Laranja, pêra, suco", 33m, 0.7m, 7.6m, 0.1m, false, 400m, Alergenos.Nenhum,
            Observacao: "Laranja pera, sem açúcar."),
        new("agua-de-coco", "Água de coco", CategoriaAlimento.Bebidas, "ml", FonteNutricional.Taco, "478", "Coco, água de", 22m, 0m, 5.3m, 0m, false, 500m, Alergenos.Nenhum),
        new("refrigerante-de-cola", "Refrigerante de cola", CategoriaAlimento.Bebidas, "ml", FonteNutricional.Taco, "480", "Refrigerante, tipo cola", 34m, 0m, 8.7m, 0m, false, 350m, Alergenos.Nenhum),
        new("refrigerante-de-guarana", "Refrigerante de guaraná", CategoriaAlimento.Bebidas, "ml", FonteNutricional.Taco, "481", "Refrigerante, tipo guaraná", 39m, 0m, 10m, 0m, false, 350m, Alergenos.Nenhum),
        new("cerveja-pilsen", "Cerveja pilsen", CategoriaAlimento.BebidasAlcoolicas, "ml", FonteNutricional.Taco, "474", "Cerveja, pilsen", 41m, 0.6m, 3.3m, 0m, false, 355m, Alergenos.Gluten,
            Observacao: "Tem 3,6 g de álcool em 100 g (TACO), que entram nas kcal e não em proteína, carboidrato ou gordura. Só aparece com a bebida entre as preferências (ValidadorBebidaAlcoolica)."),
    ];
}

public enum FonteNutricional
{
    Taco,
    Usda,
    Derivado
}

public enum CategoriaAlimento
{
    CereaisEMassas,
    Paes,
    Leguminosas,
    TuberculosERaizes,
    Hortalicas,
    Frutas,
    CarneBovina,
    Aves,
    CarneSuina,
    Peixes,
    Ovos,
    Laticinios,
    OleaginosasESementes,
    OleosEGorduras,
    AcucaresEMel,
    Bebidas,
    BebidasAlcoolicas
}

/// <summary>
/// Marcas de alérgenos e restrições de cada item. A marcação é CONSERVADORA:
/// na dúvida (versões comerciais que costumam conter, "pode conter",
/// contaminação cruzada), o item é marcado. Ela serve para tirar itens do
/// cardápio de quem informou a restrição, e NÃO substitui orientação de um
/// médico ou nutricionista: rótulos e receitas variam.
/// </summary>
[Flags]
public enum Alergenos
{
    Nenhum = 0,
    Lactose = 1,
    Gluten = 2,
    Amendoim = 4,
    Peixe = 8,
    Crustaceo = 16,
    Ovo = 32
}

/// <summary>Nome do item usado nas UFs listadas (ex.: "Aipim cozido" no RJ).</summary>
public record ApelidoRegional(string Nome, IReadOnlyList<string> Ufs);

/// <summary>Peso de uma unidade (ovo, pão, fruta inteira) e de onde ele veio.</summary>
public record PesoPorUnidade(decimal Gramas, string Origem);

/// <summary>Um alimento da base, com os valores por 100 g (ou 100 ml).</summary>
/// <param name="Id">Identificador estável (não muda se o nome mudar).</param>
/// <param name="Nome">Nome canônico, como vai para o cardápio.</param>
/// <param name="Medida">"g" ou "ml" (NormalizadorUnidades).</param>
/// <param name="CodigoFonte">Número na TACO, fdcId no USDA, ou o item da TACO de onde o DERIVADO vem.</param>
/// <param name="NomeNaFonte">Descrição do alimento na fonte.</param>
/// <param name="Ajustavel">Se o C# pode mudar a quantidade em gramas para fechar a meta do dia (fase 4). Contáveis mudam só de unidade em unidade.</param>
/// <param name="PorcaoMaxima">Maior quantidade plausível numa refeição, na Medida do item.</param>
public record AlimentoBase(
    string Id,
    string Nome,
    CategoriaAlimento Categoria,
    string Medida,
    FonteNutricional Fonte,
    string CodigoFonte,
    string NomeNaFonte,
    decimal Kcal,
    decimal ProteinaG,
    decimal CarboidratoG,
    decimal GorduraG,
    bool Ajustavel,
    decimal PorcaoMaxima,
    Alergenos Alergenos,
    PesoPorUnidade? PesoUnidade = null,
    IReadOnlyList<ApelidoRegional>? Apelidos = null,
    string? Observacao = null)
{
    public IReadOnlyList<ApelidoRegional> Apelidos { get; init; } = Apelidos ?? [];
}
