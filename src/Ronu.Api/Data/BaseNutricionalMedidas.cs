// Medidas caseiras da fase 2a, geradas em 2026-10-20 a partir de docs/pof/ (tabela de revisão aprovada).
// Para mudar uma medida: confira a linha na POF, edite aqui e em docs/pof/. O teste BaseNutricionalTests
// confere cada medida contra docs/pof/medidas-pof-itens-mapeados.csv.

namespace Ronu.Api.Data;

public static partial class BaseNutricional
{
    /// <summary>
    /// Medidas caseiras por item (ver MedidaCaseira). Itens que não estão aqui só têm gramas.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, MedidaCaseira[]> MedidasPorItem =
        new Dictionary<string, MedidaCaseira[]>
        {
            ["arroz-branco-cozido"] =
            [
                new("colher de sopa", "colheres de sopa", 25m, OrigemMedida.Direta, "6300101", 35, "Arroz cozido - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 45m, OrigemMedida.Direta, "6300101", 35, "Arroz cozido - colher de arroz cheia"),
                new("concha", "conchas", 100m, OrigemMedida.Direta, "6300101", 35, "Arroz cozido - concha média cheia"),
                new("escumadeira", "escumadeiras", 85m, OrigemMedida.Direta, "6300101", 35, "Arroz cozido - escumadeira média cheia"),
                new("prato fundo", "pratos fundos", 200m, OrigemMedida.Direta, "6300101", 35, "Arroz cozido - prato fundo cheio"),
            ],
            ["arroz-integral-cozido"] =
            [
                new("colher de sopa", "colheres de sopa", 20m, OrigemMedida.Direta, "6300201", 36, "Arroz integral cozido - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 63m, OrigemMedida.Direta, "6300201", 36, "Arroz integral cozido - colher de arroz cheia"),
                new("concha", "conchas", 117m, OrigemMedida.Direta, "6300201", 36, "Arroz integral cozido - concha média cheia"),
                new("escumadeira", "escumadeiras", 59m, OrigemMedida.Direta, "6300201", 36, "Arroz integral cozido - escumadeira média cheia"),
                new("prato fundo", "pratos fundos", 200m, OrigemMedida.Direta, "6300201", 36, "Arroz cozido - prato fundo cheio"),
            ],
            ["aveia-em-flocos"] =
            [
                new("colher de sopa", "colheres de sopa", 15m, OrigemMedida.Direta, "6500401", 74, "Aveia flocos - colher de sopa cheia"),
                new("colher de sobremesa", "colheres de sobremesa", 7m, OrigemMedida.Direta, "6500401", 74, "Aveia flocos - colher de sobremesa cheia"),
                new("colher de chá", "colheres de chá", 2m, OrigemMedida.Direta, "6500401", 74, "Aveia flocos - colher de chá cheia"),
            ],
            ["macarrao-cozido"] =
            [
                new("colher de sopa", "colheres de sopa", 25m, OrigemMedida.Direta, "6503401", 87, "Macarrão cozido - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 50m, OrigemMedida.Direta, "6503401", 87, "Macarrão cozido - colher de arroz cheia"),
                new("escumadeira", "escumadeiras", 110m, OrigemMedida.Direta, "6503401", 87, "Macarrão cozido - escumadeira média cheia"),
                new("pegador", "pegadores", 110m, OrigemMedida.Direta, "6503401", 87, "Macarrão cozido - pegador"),
                new("garfada", "garfadas", 30m, OrigemMedida.Direta, "6503401", 87, "Macarrão cozido - garfada"),
                new("prato raso", "pratos rasos", 320m, OrigemMedida.Direta, "6503401", 88, "Macarrão cozido - prato raso cheio"),
                new("prato fundo", "pratos fundos", 400m, OrigemMedida.Direta, "6503401", 88, "Macarrão cozido - prato fundo cheio"),
            ],
            ["cuscuz-de-milho-cozido"] =
            [
                new("pedaço", "pedaços", 135m, OrigemMedida.Direta, "6902901", 193, "Cuscuz de milho - pedaço médio"),
            ],
            ["milho-verde-em-conserva"] =
            [
                new("colher de sopa", "colheres de sopa", 24m, OrigemMedida.Direta, "7700401", 312, "Milho verde enlatado - colher de sopa"),
                new("colher de sobremesa", "colheres de sobremesa", 17m, OrigemMedida.Direta, "7700401", 312, "Milho verde enlatado - colher de sobremesa cheia"),
            ],
            ["farinha-de-mandioca-torrada"] =
            [
                new("colher de sopa", "colheres de sopa", 16m, OrigemMedida.Direta, "6501401", 80, "Farinha de mandioca - colher de sopa cheia"),
                new("colher de sobremesa", "colheres de sobremesa", 10m, OrigemMedida.Direta, "6501401", 80, "Farinha de mandioca - colher de sobremesa cheia"),
                new("colher de chá", "colheres de chá", 5m, OrigemMedida.Direta, "6501401", 80, "Farinha de mandioca - colher de chá cheia"),
                new("colher de servir", "colheres de servir", 33m, OrigemMedida.Direta, "6501401", 80, "Farinha de mandioca - colher de arroz"),
                new("concha", "conchas", 110m, OrigemMedida.Direta, "6501401", 80, "Farinha de mandioca - concha"),
                new("punhado", "punhados", 23m, OrigemMedida.Direta, "6501401", 81, "Farinha de mandioca - punhado"),
                new("prato fundo", "pratos fundos", 271m, OrigemMedida.Direta, "6501401", 81, "Farinha de mandioca - prato fundo"),
                new("tigela", "tigelas", 148m, OrigemMedida.Direta, "6501401", 81, "Farinha de mandioca - tigela"),
                new("copo médio", "copos médios", 172m, OrigemMedida.Direta, "6501401", 80, "Farinha de mandioca - copo médio"),
                new("xícara de chá", "xícaras de chá", 148m, OrigemMedida.Direta, "6501401", 81, "Farinha de mandioca - xícara de chá"),
                new("caneca", "canecas", 171m, OrigemMedida.Direta, "6501401", 80, "Farinha de mandioca - caneca"),
            ],
            ["pao-frances"] =
            [
                new("unidade", "unidades", 50m, OrigemMedida.Direta, "8000105", 393, "Pão francês - unidade", EhUnidade: true),
            ],
            ["pao-de-forma-integral"] =
            [
                new("fatia", "fatias", 25m, OrigemMedida.Categoria, "8001401", 395, "Pão de forma de aveia - unidade", EhUnidade: true),
            ],
            ["pao-de-queijo-assado"] =
            [
                new("unidade", "unidades", 20m, OrigemMedida.Direta, "8000801", 394, "Pão de queijo - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 10m, OrigemMedida.Direta, "8000801", 394, "Pão de queijo - unidade pequena"),
            ],
            ["feijao-cozido"] =
            [
                new("colher de sopa", "colheres de sopa", 17m, OrigemMedida.Categoria, "6303102", 43, "Feijão branco cozido - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 35m, OrigemMedida.Categoria, "6303102", 43, "Feijão branco cozido - colher de arroz cheia"),
                new("concha", "conchas", 140m, OrigemMedida.Categoria, "6303102", 43, "Feijão branco cozido - concha média cheia"),
            ],
            ["feijao-preto-cozido"] =
            [
                new("colher de sopa", "colheres de sopa", 17m, OrigemMedida.Categoria, "6303102", 43, "Feijão branco cozido - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 35m, OrigemMedida.Categoria, "6303102", 43, "Feijão branco cozido - colher de arroz cheia"),
                new("concha", "conchas", 140m, OrigemMedida.Categoria, "6303102", 43, "Feijão branco cozido - concha média cheia"),
            ],
            ["feijao-fradinho-cozido"] =
            [
                new("colher de sopa", "colheres de sopa", 17m, OrigemMedida.Categoria, "6301603", 40, "Feijão branco cozido - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 35m, OrigemMedida.Categoria, "6301603", 40, "Feijão branco cozido - colher de arroz cheia"),
                new("concha", "conchas", 140m, OrigemMedida.Categoria, "6301603", 40, "Feijão branco cozido - concha média cheia"),
            ],
            ["lentilha-cozida"] =
            [
                new("colher de sopa", "colheres de sopa", 18m, OrigemMedida.Direta, "6302901", 42, "Lentilha cozida - colher de sopa"),
                new("colher de servir", "colheres de servir", 32m, OrigemMedida.Direta, "6302901", 42, "Lentilha cozida - colher de arroz"),
                new("concha", "conchas", 160m, OrigemMedida.Direta, "6302901", 42, "Lentilha cozida - concha média cheia"),
            ],
            ["batata-inglesa-cozida"] =
            [
                new("unidade", "unidades", 140m, OrigemMedida.Direta, "6400101", 46, "Batata-inglesa cozida - unidade média"),
                new("unidade pequena", "unidades pequenas", 70m, OrigemMedida.Direta, "6400101", 46, "Batata-inglesa cozida - unidade pequena"),
                new("colher de sopa", "colheres de sopa", 30m, OrigemMedida.Direta, "6400101", 46, "Batata-inglesa cozida - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 60m, OrigemMedida.Direta, "6400101", 45, "Batata-inglesa cozida - colher de arroz cheia"),
                new("escumadeira", "escumadeiras", 80m, OrigemMedida.Direta, "6400101", 46, "Batata-inglesa cozida - escumadeira média cheia"),
            ],
            ["batata-doce-cozida"] =
            [
                // Saiu da espera da 2a em 2026-10-26, conferida no PDF. Sem marca de unidade: 355 g é grande
                // demais para uma refeição, então o cardápio fica em gramas.
                new("unidade", "unidades", 355m, OrigemMedida.Direta, "6400401", 51, "Batata-doce cozida - unidade média"),
                new("fatia", "fatias", 70m, OrigemMedida.Direta, "6400401", 51, "Batata-doce cozida - fatia média"),
                new("colher de sopa", "colheres de sopa", 42m, OrigemMedida.Direta, "6400401", 51, "Batata-doce cozida - colher de sopa cheia"),
            ],
            ["mandioca-cozida"] =
            [
                new("pedaço", "pedaços", 100m, OrigemMedida.Categoria, "6400601", 55, "Aipim cozido - pedaço médio"),
                new("colher de sopa", "colheres de sopa", 30m, OrigemMedida.Categoria, "6400601", 54, "Aipim cozido - colher de sopa cheia picada"),
                new("colher de servir", "colheres de servir", 60m, OrigemMedida.Categoria, "6400601", 54, "Aipim cozido - colher de arroz cheia picada"),
            ],
            ["mandioquinha-cozida"] =
            [
                new("unidade", "unidades", 80m, OrigemMedida.Direta, "6400303", 50, "Batata-baroa cozida - unidade média"),
                new("colher de sopa", "colheres de sopa", 35m, OrigemMedida.Direta, "6400303", 50, "Batata-baroa cozida - colher de sopa cheia"),
            ],
            ["alface-crua"] =
            [
                new("unidade", "unidades", 200m, OrigemMedida.Direta, "6700101", 105, "Alface - unidade média"),
                new("unidade pequena", "unidades pequenas", 130m, OrigemMedida.Direta, "6700101", 105, "Alface - unidade pequena"),
                new("colher de sopa", "colheres de sopa", 8m, OrigemMedida.Direta, "6700101", 104, "Alface - colher de sopa cheia picada"),
                new("folha", "folhas", 10m, OrigemMedida.Direta, "6700101", 104, "Alface - folha média"),
                new("pires", "pires", 20m, OrigemMedida.Direta, "6700101", 104, "Alface - pires de folha"),
                new("prato de sobremesa", "pratos de sobremesa", 30m, OrigemMedida.Direta, "6700101", 104, "Alface - prato de sobremesa cheio picada"),
                new("prato raso", "pratos rasos", 50m, OrigemMedida.Direta, "6700101", 104, "Alface - prato raso"),
                new("prato fundo", "pratos fundos", 60m, OrigemMedida.Direta, "6700101", 104, "Alface - prato fundo"),
            ],
            ["rucula-crua"] =
            [
                new("ramo", "ramos", 6m, OrigemMedida.Direta, "6702001", 124, "Rúcula - ramo"),
            ],
            ["agriao-cru"] =
            [
                new("colher de sopa", "colheres de sopa", 7m, OrigemMedida.Direta, "6701301", 116, "Agrião - colher de sopa cheia picada"),
                new("garfada", "garfadas", 30m, OrigemMedida.Direta, "6701301", 116, "Agrião - garfada"),
                new("ramo", "ramos", 5m, OrigemMedida.Direta, "6701301", 116, "Agrião - ramo médio"),
                new("pires", "pires", 10m, OrigemMedida.Direta, "6701301", 116, "Agrião - pires cheio picado"),
                new("prato raso", "pratos rasos", 80m, OrigemMedida.Direta, "6701301", 116, "Agrião - prato raso cheio picado"),
            ],
            ["couve-crua"] =
            [
                new("folha", "folhas", 20m, OrigemMedida.Direta, "6700501", 106, "Couve crua - folha média"),
            ],
            ["tomate-cru"] =
            [
                new("unidade", "unidades", 100m, OrigemMedida.Direta, "6705101", 143, "Tomate - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 50m, OrigemMedida.Direta, "6705101", 143, "Tomate - unidade pequena"),
                new("fatia", "fatias", 15m, OrigemMedida.Direta, "6705101", 143, "Tomate - fatia média"),
                new("colher de sopa", "colheres de sopa", 30m, OrigemMedida.Direta, "6705101", 143, "Tomate - colher de sopa cheia em tiras"),
            ],
            ["pepino-cru"] =
            [
                new("unidade", "unidades", 100m, OrigemMedida.Direta, "6704001", 133, "Pepino - unidade média", EhUnidade: true),
                new("colher de sopa", "colheres de sopa", 18m, OrigemMedida.Direta, "6704001", 132, "Pepino - colher de sopa cheia picada"),
            ],
            ["cenoura-crua"] =
            [
                new("unidade", "unidades", 120m, OrigemMedida.Direta, "6401201", 69, "Cenoura crua - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 55m, OrigemMedida.Direta, "6401201", 69, "Cenoura crua - unidade pequena"),
                new("colher de sopa", "colheres de sopa", 12m, OrigemMedida.Direta, "6401201", 68, "Cenoura crua - colher de sopa cheia ralada"),
            ],
            ["cenoura-cozida"] =
            [
                new("unidade", "unidades", 100m, OrigemMedida.Direta, "6401201", 69, "Cenoura cozida - unidade média"),
                new("unidade pequena", "unidades pequenas", 46m, OrigemMedida.Direta, "6401201", 69, "Cenoura cozida - unidade pequena"),
                new("fatia", "fatias", 5m, OrigemMedida.Direta, "6401201", 69, "Cenoura cozida - fatia"),
                new("escumadeira", "escumadeiras", 48m, OrigemMedida.Direta, "6401201", 69, "Cenoura cozida - escumadeira média rasa picada"),
            ],
            ["beterraba-crua"] =
            [
                new("unidade", "unidades", 140m, OrigemMedida.Direta, "6401101", 66, "Beterraba crua - unidade média"),
                new("colher de sopa", "colheres de sopa", 16m, OrigemMedida.Direta, "6401101", 66, "Beterraba crua - colher de sopa cheia ralada"),
                new("prato de sobremesa", "pratos de sobremesa", 168m, OrigemMedida.Direta, "6401101", 66, "Beterraba - prato de sobremesa"),
            ],
            ["beterraba-cozida"] =
            [
                new("unidade", "unidades", 125m, OrigemMedida.Direta, "6401101", 67, "Beterraba cozida - unidade média"),
                new("unidade pequena", "unidades pequenas", 75m, OrigemMedida.Direta, "6401101", 67, "Beterraba cozida - unidade pequena"),
                new("fatia", "fatias", 12m, OrigemMedida.Direta, "6401101", 67, "Beterraba cozida - fatia média"),
                new("colher de sopa", "colheres de sopa", 20m, OrigemMedida.Direta, "6401101", 67, "Beterraba cozida - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 38m, OrigemMedida.Direta, "6401101", 66, "Beterraba cozida - colher de arroz cheia"),
                new("escumadeira", "escumadeiras", 45m, OrigemMedida.Direta, "6401101", 67, "Beterraba cozida - escumadeira média rasa picada"),
                new("prato de sobremesa", "pratos de sobremesa", 168m, OrigemMedida.Direta, "6401101", 67, "Beterraba - prato de sobremesa"),
            ],
            ["brocolis-cozido"] =
            [
                new("colher de sopa", "colheres de sopa", 10m, OrigemMedida.Direta, "6701704", 121, "Brócolis cozido - colher de sopa cheia picada"),
                new("pires", "pires", 75m, OrigemMedida.Direta, "6701704", 121, "Brócolis cozido - pires"),
            ],
            ["couve-flor-cozida"] =
            [
                new("unidade pequena", "unidades pequenas", 30m, OrigemMedida.Direta, "6700601", 110, "Couve-flor cozida - ramo pequeno"),
                new("colher de sopa", "colheres de sopa", 25m, OrigemMedida.Direta, "6700601", 110, "Couve-flor cozida - colher de sopa cheia"),
                new("ramo", "ramos", 60m, OrigemMedida.Direta, "6700601", 110, "Couve-flor cozida - ramo médio"),
                new("prato de sobremesa", "pratos de sobremesa", 100m, OrigemMedida.Direta, "6700601", 110, "Couve-flor cozida - prato de sobremesa"),
            ],
            ["abobrinha-cozida"] =
            [
                new("unidade", "unidades", 335m, OrigemMedida.Direta, "6703701", 127, "Abobrinha - unidade"),
                new("fatia", "fatias", 61m, OrigemMedida.Direta, "6703701", 127, "Abobrinha - fatia"),
                new("pedaço", "pedaços", 17m, OrigemMedida.Direta, "6703701", 127, "Abobrinha - pedaço"),
                new("colher de sopa", "colheres de sopa", 30m, OrigemMedida.Direta, "6703701", 127, "Abobrinha cozida - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 70m, OrigemMedida.Direta, "6703701", 127, "Abobrinha cozida - colher de arroz cheia"),
                new("escumadeira", "escumadeiras", 90m, OrigemMedida.Direta, "6703701", 127, "Abobrinha cozida - escumadeira média cheia"),
                new("rodela", "rodelas", 18m, OrigemMedida.Direta, "6703701", 127, "Abobrinha - rodela"),
            ],
            ["abobrinha-refogada"] =
            [
                new("unidade", "unidades", 335m, OrigemMedida.Direta, "6703701", 129, "Abobrinha - unidade"),
                new("pedaço", "pedaços", 17m, OrigemMedida.Direta, "6703701", 129, "Abobrinha - pedaço"),
                new("rodela", "rodelas", 18m, OrigemMedida.Direta, "6703701", 129, "Abobrinha - rodela"),
            ],
            ["chuchu-cozido"] =
            [
                new("unidade", "unidades", 230m, OrigemMedida.Direta, "6704101", 134, "Chuchu cozido - unidade média"),
                new("unidade pequena", "unidades pequenas", 145m, OrigemMedida.Direta, "6704101", 134, "Chuchu cozido - unidade pequena"),
                new("pedaço", "pedaços", 30m, OrigemMedida.Direta, "6704101", 133, "Chuchu cozido - pedaço"),
                new("colher de sopa", "colheres de sopa", 20m, OrigemMedida.Direta, "6704101", 133, "Chuchu cozido - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 45m, OrigemMedida.Direta, "6704101", 133, "Chuchu cozido - colher de arroz cheia"),
                new("escumadeira", "escumadeiras", 110m, OrigemMedida.Direta, "6704101", 133, "Chuchu cozido - escumadeira média cheia"),
            ],
            ["couve-refogada"] =
            [
                new("colher de sopa", "colheres de sopa", 20m, OrigemMedida.Direta, "6700501", 107, "Couve refogada - colher de sopa cheia picada"),
                new("folha", "folhas", 25m, OrigemMedida.Direta, "6700501", 108, "Couve refogada - folha média"),
            ],
            ["repolho-cru"] =
            [
                new("colher de sopa", "colheres de sopa", 10m, OrigemMedida.Direta, "6700901", 113, "Repolho cru - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 25m, OrigemMedida.Direta, "6700901", 113, "Repolho cru - colher de arroz cheia"),
                new("escumadeira", "escumadeiras", 25m, OrigemMedida.Direta, "6700901", 113, "Repolho cru - escumadeira média cheia"),
                new("folha", "folhas", 26m, OrigemMedida.Direta, "6700901", 113, "Repolho cru - folha média"),
                new("pires", "pires", 100m, OrigemMedida.Direta, "6700901", 113, "Repolho - pires cheio"),
            ],
            ["repolho-roxo-cru"] =
            [
                new("colher de sopa", "colheres de sopa", 10m, OrigemMedida.Categoria, "6700901", 113, "Repolho cru - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 25m, OrigemMedida.Categoria, "6700901", 113, "Repolho cru - colher de arroz cheia"),
                new("escumadeira", "escumadeiras", 25m, OrigemMedida.Categoria, "6700901", 113, "Repolho cru - escumadeira média cheia"),
                new("folha", "folhas", 26m, OrigemMedida.Categoria, "6700901", 113, "Repolho cru - folha média"),
                new("pires", "pires", 100m, OrigemMedida.Categoria, "6700901", 113, "Repolho - pires cheio"),
            ],
            ["acelga-crua"] =
            [
                new("colher de sopa", "colheres de sopa", 6m, OrigemMedida.Direta, "6701501", 118, "Acelga - colher de sopa cheia picada"),
                new("colher de servir", "colheres de servir", 10m, OrigemMedida.Direta, "6701501", 118, "Acelga - colher de arroz cheia"),
                new("folha", "folhas", 10m, OrigemMedida.Direta, "6701501", 118, "Acelga - folha média"),
            ],
            ["cebola-crua"] =
            [
                new("unidade", "unidades", 70m, OrigemMedida.Direta, "6705701", 148, "Cebola - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 30m, OrigemMedida.Direta, "6705701", 148, "Cebola - unidade pequena"),
                new("fatia", "fatias", 6m, OrigemMedida.Direta, "6705701", 148, "Cebola - fatia média"),
                new("colher de sopa", "colheres de sopa", 10m, OrigemMedida.Direta, "6705701", 148, "Cebola - colher de sopa cheia"),
            ],
            ["abobora-cozida"] =
            [
                new("pedaço", "pedaços", 50m, OrigemMedida.Direta, "6703901", 130, "Abóbora cozida - pedaço médio"),
                new("colher de sopa", "colheres de sopa", 36m, OrigemMedida.Direta, "6703901", 130, "Abóbora cozida - colher de sopa cheia"),
                new("escumadeira", "escumadeiras", 100m, OrigemMedida.Direta, "6703901", 130, "Abóbora cozida - escumadeira média cheia"),
            ],
            ["banana-prata"] =
            [
                new("unidade", "unidades", 75m, OrigemMedida.Direta, "6801101", 155, "Banana-prata - unidade", EhUnidade: true),
                new("pedaço", "pedaços", 16m, OrigemMedida.Direta, "6801101", 155, "Banana - pedaço"),
                new("prato raso", "pratos rasos", 235m, OrigemMedida.Direta, "6801101", 155, "Banana - prato raso"),
            ],
            ["banana-nanica"] =
            [
                new("pedaço", "pedaços", 16m, OrigemMedida.Categoria, "6801101", 155, "Banana - pedaço"),
                new("prato raso", "pratos rasos", 235m, OrigemMedida.Categoria, "6801101", 155, "Banana - prato raso"),
            ],
            ["maca-fuji"] =
            [
                new("unidade", "unidades", 150m, OrigemMedida.Categoria, "6803001", 160, "Maçã - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 90m, OrigemMedida.Categoria, "6803001", 160, "Maçã - unidade pequena"),
            ],
            ["pera-williams"] =
            [
                new("unidade", "unidades", 130m, OrigemMedida.Categoria, "6803601", 163, "pera - unidade média", EhUnidade: true),
            ],
            ["laranja-pera"] =
            [
                new("unidade", "unidades", 180m, OrigemMedida.Direta, "6801801", 156, "Laranja - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 90m, OrigemMedida.Direta, "6801801", 156, "Laranja - unidade pequena"),
            ],
            ["tangerina"] =
            [
                new("unidade", "unidades", 135m, OrigemMedida.Direta, "6802201", 157, "Tangerina - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 100m, OrigemMedida.Direta, "6802201", 157, "Tangerina - unidade pequena"),
            ],
            ["kiwi"] =
            [
                new("unidade", "unidades", 94m, OrigemMedida.Direta, "6807801", 174, "Kiwi - unidade média", EhUnidade: true),
                new("rodela", "rodelas", 11m, OrigemMedida.Direta, "6807801", 174, "Kiwi - rodela"),
            ],
            ["mamao-papaia"] =
            [
                new("unidade", "unidades", 310m, OrigemMedida.Categoria, "6803101", 161, "Mamão papaya - unidade média"),
                new("unidade pequena", "unidades pequenas", 270m, OrigemMedida.Categoria, "6803101", 161, "Mamão papaya - unidade pequena"),
                new("fatia", "fatias", 170m, OrigemMedida.Categoria, "6803101", 160, "Mamão - fatia média"),
                new("colher de sopa", "colheres de sopa", 40m, OrigemMedida.Categoria, "6803101", 160, "Mamão papaya - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 70m, OrigemMedida.Categoria, "6803101", 160, "Mamão papaya - colher de arroz cheia"),
            ],
            ["mamao-formosa"] =
            [
                new("fatia", "fatias", 170m, OrigemMedida.Categoria, "6803101", 160, "Mamão - fatia média", EhUnidade: true),
            ],
            ["abacaxi"] =
            [
                new("unidade", "unidades", 750m, OrigemMedida.Direta, "6802601", 159, "Abacaxi - unidade média"),
                new("unidade pequena", "unidades pequenas", 480m, OrigemMedida.Direta, "6802601", 159, "Abacaxi - unidade pequena"),
                new("fatia", "fatias", 75m, OrigemMedida.Direta, "6802601", 158, "Abacaxi - fatia média", EhUnidade: true),
            ],
            ["melancia"] =
            [
                new("fatia", "fatias", 200m, OrigemMedida.Direta, "6803401", 162, "Melancia - fatia média", EhUnidade: true),
                new("pedaço", "pedaços", 150m, OrigemMedida.Direta, "6803401", 162, "Melancia - pedaço médio"),
            ],
            ["melao"] =
            [
                new("unidade", "unidades", 660m, OrigemMedida.Direta, "6803501", 163, "Melão - unidade média"),
                new("fatia", "fatias", 90m, OrigemMedida.Direta, "6803501", 162, "Melão - fatia média", EhUnidade: true),
            ],
            ["morango"] =
            [
                new("unidade", "unidades", 12m, OrigemMedida.Direta, "6805201", 169, "Morango - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 7m, OrigemMedida.Direta, "6805201", 169, "Morango - unidade pequena"),
            ],
            ["uva-italia"] =
            [
                new("unidade", "unidades", 8m, OrigemMedida.Categoria, "6803901", 164, "Uva - unidade média"),
                new("unidade pequena", "unidades pequenas", 4m, OrigemMedida.Categoria, "6803901", 164, "Uva - unidade pequena"),
            ],
            ["abacate"] =
            [
                new("unidade", "unidades", 430m, OrigemMedida.Direta, "6802701", 159, "Abacate - unidade média"),
                new("unidade pequena", "unidades pequenas", 370m, OrigemMedida.Direta, "6802701", 159, "Abacate - unidade pequena"),
                new("colher de sopa", "colheres de sopa", 45m, OrigemMedida.Direta, "6802701", 159, "Abacate - colher de sopa cheia picado"),
            ],
            ["goiaba-vermelha"] =
            [
                new("unidade", "unidades", 170m, OrigemMedida.Categoria, "6804201", 165, "Goiaba - unidade média", EhUnidade: true),
                new("unidade pequena", "unidades pequenas", 50m, OrigemMedida.Categoria, "6804201", 166, "Goiaba - unidade pequena"),
            ],
            ["acai-sem-acucar"] =
            [
                new("concha", "conchas", 101m, OrigemMedida.Direta, "6601706", 101, "Açaí - concha"),
                new("tigela", "tigelas", 202m, OrigemMedida.Direta, "6601706", 102, "Açaí - tigela"),
                new("copo médio", "copos médios", 273m, OrigemMedida.Direta, "6601706", 101, "Açaí - copo médio"),
                new("copo grande", "copos grandes", 326m, OrigemMedida.Direta, "6601706", 101, "Açaí - copo grande"),
            ],
            ["patinho-bovino-grelhado"] =
            [
                new("bife", "bifes", 100m, OrigemMedida.Categoria, "7100501", 229, "Bife bovino - unidade média"),
            ],
            ["patinho-bovino-moido-refogado"] =
            [
                new("colher de sopa", "colheres de sopa", 25m, OrigemMedida.Categoria, "7104301", 261, "Carne bovina moída - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 60m, OrigemMedida.Categoria, "7104301", 260, "Carne bovina moída - colher de arroz cheia"),
            ],
            ["carne-moida-de-acem-cozida"] =
            [
                new("colher de sopa", "colheres de sopa", 25m, OrigemMedida.Categoria, "7104301", 259, "Carne bovina moída - colher de sopa cheia"),
                new("colher de servir", "colheres de servir", 60m, OrigemMedida.Categoria, "7104301", 259, "Carne bovina moída - colher de arroz cheia"),
            ],
            ["alcatra-bovina-grelhada"] =
            [
                new("bife", "bifes", 100m, OrigemMedida.Categoria, "7100301", 225, "Bife bovino - unidade média"),
            ],
            ["contrafile-bovino-grelhado"] =
            [
                new("bife", "bifes", 100m, OrigemMedida.Categoria, "7100201", 222, "Bife bovino - unidade média"),
            ],
            ["maminha-bovina-grelhada"] =
            [
                new("bife", "bifes", 100m, OrigemMedida.Categoria, "7100303", 226, "Bife bovino - unidade média"),
            ],
            ["file-mignon-grelhado"] =
            [
                new("bife", "bifes", 100m, OrigemMedida.Categoria, "7100101", 220, "Bife bovino - unidade média"),
            ],
            ["sobrecoxa-de-frango-sem-pele-assada"] =
            [
                new("sobrecoxa", "sobrecoxas", 65m, OrigemMedida.Categoria, "7800302", 329, "Frango assado - sobrecoxa média"),
            ],
            ["frango-sem-pele-assado"] =
            [
                new("sobrecoxa", "sobrecoxas", 65m, OrigemMedida.Direta, "7800103", 323, "Frango assado - sobrecoxa média"),
                new("peito", "peitos", 180m, OrigemMedida.Direta, "7800103", 323, "Frango assado - peito médio"),
                new("asa", "asas", 40m, OrigemMedida.Direta, "7800103", 323, "Frango assado - asa média"),
            ],
            ["file-de-tilapia-grelhado"] =
            [
                new("filé", "filés", 120m, OrigemMedida.Semelhante, "7400101", 303, "Peixe cozido - filé médio"),
            ],
            ["atum-em-conserva-em-agua"] =
            [
                new("colher de sopa", "colheres de sopa", 16m, OrigemMedida.Direta, "7703402", 320, "Atum sólido em água e sal Coqueiro - colher de sopa cheia"),
                new("colher de sobremesa", "colheres de sobremesa", 10m, OrigemMedida.Direta, "7703402", 320, "Atum sólido em água e sal Coqueiro - colher de sobremesa"),
            ],
            ["merluza-assada"] =
            [
                new("filé", "filés", 120m, OrigemMedida.Semelhante, "7200101", 292, "Peixe cozido - filé médio"),
            ],
            ["corvina-assada"] =
            [
                new("filé", "filés", 120m, OrigemMedida.Semelhante, "7200101", 292, "Peixe cozido - filé médio"),
            ],
            ["ovo-de-galinha-cozido"] =
            [
                new("unidade", "unidades", 45m, OrigemMedida.Direta, "7803301", 362, "Ovo de galinha cozido - unidade média", EhUnidade: true),
            ],
            ["ovo-de-galinha-frito"] =
            [
                new("unidade", "unidades", 50m, OrigemMedida.Direta, "7803301", 362, "Ovo de galinha frito - unidade média", EhUnidade: true),
            ],
            ["leite-integral"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "7900101", 367, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "7900101", 367, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "7900101", 367, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "7900101", 367, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Direta, "7900101", 367, "Copo tulipa"),
                new("copo de cafezinho", "copos de cafezinho", 50m, OrigemMedida.Direta, "7900101", 367, "Copo de cafezinho"),
                new("xícara de chá", "xícaras de chá", 200m, OrigemMedida.Direta, "7900101", 368, "Xícara de chá"),
                new("xícara de café", "xícaras de café", 50m, OrigemMedida.Direta, "7900101", 367, "Xícara de café"),
                new("caneca", "canecas", 300m, OrigemMedida.Direta, "7900101", 367, "Caneca de 300ml"),
            ],
            ["leite-desnatado"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "7903601", 385, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "7903601", 385, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "7903601", 385, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "7903601", 385, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Direta, "7903601", 385, "Copo tulipa"),
                new("xícara de chá", "xícaras de chá", 200m, OrigemMedida.Direta, "7903601", 385, "Xícara de chá"),
                new("xícara de café", "xícaras de café", 50m, OrigemMedida.Direta, "7903601", 385, "Xícara de café"),
                new("caneca", "canecas", 300m, OrigemMedida.Direta, "7903601", 384, "Caneca de 300ml"),
            ],
            ["iogurte-natural"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "7901204", 373, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "7901204", 373, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "7901204", 373, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "7901204", 373, "Copo de requeijão"),
            ],
            ["iogurte-natural-desnatado"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "7901203", 373, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "7901203", 373, "Copo médio"),
            ],
            ["queijo-mucarela"] =
            [
                new("fatia", "fatias", 20m, OrigemMedida.Direta, "7901801", 377, "Queijo muçarela - fatia média", EhUnidade: true),
            ],
            ["queijo-prato"] =
            [
                new("fatia", "fatias", 15m, OrigemMedida.Direta, "7901701", 377, "Queijo prato - fatia média", EhUnidade: true),
            ],
            ["ricota"] =
            [
                new("fatia", "fatias", 35m, OrigemMedida.Direta, "7902201", 379, "Ricota Marília - fatia média"),
            ],
            ["requeijao-cremoso"] =
            [
                new("colher de sopa", "colheres de sopa", 30m, OrigemMedida.Direta, "7902901", 381, "Requeijão - colher de sopa cheia"),
            ],
            ["castanha-de-caju"] =
            [
                new("unidade", "unidades", 2.5m, OrigemMedida.Direta, "6600801", 100, "Castanha de caju torrada - unidade média"),
                new("punhado", "punhados", 40m, OrigemMedida.Direta, "6600801", 100, "Castanha de caju torrada - punhado"),
            ],
            ["castanha-do-para"] =
            [
                new("unidade", "unidades", 4m, OrigemMedida.Direta, "6600701", 100, "Castanha-do-pará - unidade"),
            ],
            ["amendoa-torrada"] =
            [
                new("unidade", "unidades", 1m, OrigemMedida.Direta, "6600501", 100, "Amêndoa - unidade"),
            ],
            ["noz"] =
            [
                new("unidade", "unidades", 5m, OrigemMedida.Direta, "6601501", 101, "Noz - unidade"),
            ],
            ["linhaca"] =
            [
                new("colher de sopa", "colheres de sopa", 13m, OrigemMedida.Direta, "6302001", 41, "Semente de linhaça - colher de sopa"),
                new("colher de sobremesa", "colheres de sobremesa", 3m, OrigemMedida.Direta, "6302001", 41, "Semente de linhaça - colher de sobremesa"),
            ],
            ["oleo-de-soja"] =
            [
                new("colher de sopa", "colheres de sopa", 8m, OrigemMedida.Direta, "8400301", 474, "Óleo vegetal - colher de sopa"),
                new("colher de chá", "colheres de chá", 2m, OrigemMedida.Direta, "8400301", 474, "Óleo vegetal - colher de chá"),
            ],
            ["manteiga"] =
            [
                new("colher de sopa", "colheres de sopa", 32m, OrigemMedida.Direta, "7901501", 375, "Manteiga - colher de sopa cheia"),
                new("colher de sobremesa", "colheres de sobremesa", 23m, OrigemMedida.Direta, "7901501", 375, "Manteiga - colher de sobremesa cheia"),
                new("colher de chá", "colheres de chá", 8m, OrigemMedida.Direta, "7901501", 375, "Manteiga - colher de chá cheia"),
            ],
            ["acucar-refinado"] =
            [
                new("colher de sopa", "colheres de sopa", 24m, OrigemMedida.Direta, "6906602", 205, "Açúcar refinado - colher de sopa cheia"),
                new("colher de sobremesa", "colheres de sobremesa", 16m, OrigemMedida.Direta, "6906602", 205, "Açúcar refinado - colher de sobremesa cheia"),
                new("colher de chá", "colheres de chá", 5m, OrigemMedida.Direta, "6906602", 205, "Açúcar refinado - colher de chá cheia"),
            ],
            ["acucar-mascavo"] =
            [
                new("colher de sopa", "colheres de sopa", 19m, OrigemMedida.Direta, "6900304", 176, "Açúcar mascavo - colher de sopa cheia"),
            ],
            ["mel"] =
            [
                new("colher de sopa", "colheres de sopa", 15m, OrigemMedida.Direta, "6901602", 189, "Mel de abelha - colher de sopa"),
                new("colher de sobremesa", "colheres de sobremesa", 9m, OrigemMedida.Direta, "6901602", 189, "Mel de abelha - colher de sobremesa"),
                new("colher de chá", "colheres de chá", 3m, OrigemMedida.Direta, "6901602", 188, "Mel de abelha - colher de chá"),
            ],
            ["cafe-coado"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "8501302", 505, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "8501302", 505, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "8501302", 505, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "8501302", 505, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Direta, "8501302", 505, "Copo tulipa"),
                new("copo de cafezinho", "copos de cafezinho", 50m, OrigemMedida.Direta, "8501302", 505, "Copo de cafezinho"),
                new("xícara de chá", "xícaras de chá", 200m, OrigemMedida.Direta, "8501302", 505, "Xícara de chá"),
                new("xícara de café", "xícaras de café", 50m, OrigemMedida.Direta, "8501302", 505, "Xícara de café"),
                new("caneca", "canecas", 300m, OrigemMedida.Direta, "8501302", 505, "Caneca de 300ml"),
            ],
            ["cha-preto"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Categoria, "8206301", 458, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Categoria, "8206301", 458, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Categoria, "8206301", 458, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Categoria, "8206301", 458, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Categoria, "8206301", 458, "Copo tulipa"),
                new("copo de cafezinho", "copos de cafezinho", 50m, OrigemMedida.Categoria, "8206301", 458, "Copo de cafezinho"),
                new("xícara de chá", "xícaras de chá", 200m, OrigemMedida.Categoria, "8206301", 458, "Xícara de chá"),
                new("xícara de café", "xícaras de café", 50m, OrigemMedida.Categoria, "8206301", 458, "Xícara de café"),
                new("caneca", "canecas", 300m, OrigemMedida.Categoria, "8206301", 458, "Caneca de 300ml"),
            ],
            ["cha-de-erva-doce"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Categoria, "8206301", 458, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Categoria, "8206301", 458, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Categoria, "8206301", 458, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Categoria, "8206301", 458, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Categoria, "8206301", 458, "Copo tulipa"),
                new("copo de cafezinho", "copos de cafezinho", 50m, OrigemMedida.Categoria, "8206301", 458, "Copo de cafezinho"),
                new("xícara de chá", "xícaras de chá", 200m, OrigemMedida.Categoria, "8206301", 458, "Xícara de chá"),
                new("xícara de café", "xícaras de café", 50m, OrigemMedida.Categoria, "8206301", 458, "Xícara de café"),
                new("caneca", "canecas", 300m, OrigemMedida.Categoria, "8206301", 458, "Caneca de 300ml"),
            ],
            ["suco-de-laranja-natural"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "8500407", 488, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "8500407", 488, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "8500407", 488, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "8500407", 488, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Direta, "8500407", 488, "Copo tulipa"),
                new("xícara de chá", "xícaras de chá", 200m, OrigemMedida.Direta, "8500407", 489, "Xícara de chá"),
                new("xícara de café", "xícaras de café", 50m, OrigemMedida.Direta, "8500407", 489, "Xícara de café"),
                new("caneca", "canecas", 300m, OrigemMedida.Direta, "8500407", 488, "Caneca de 300ml"),
            ],
            ["agua-de-coco"] =
            [
                new("unidade", "unidades", 340m, OrigemMedida.Direta, "8202101", 449, "Água de coco - unidade"),
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "8202101", 448, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "8202101", 448, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "8202101", 448, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "8202101", 448, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Direta, "8202101", 448, "Copo tulipa"),
                new("xícara de chá", "xícaras de chá", 200m, OrigemMedida.Direta, "8202101", 449, "Xícara de chá"),
            ],
            ["refrigerante-de-cola"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "8200101", 434, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "8200101", 434, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "8200101", 434, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "8200101", 434, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Direta, "8200101", 434, "Copo tulipa"),
                new("copo de cafezinho", "copos de cafezinho", 50m, OrigemMedida.Direta, "8200101", 434, "Copo de cafezinho"),
                new("xícara de chá", "xícaras de chá", 200m, OrigemMedida.Direta, "8200101", 436, "Xícara de chá"),
                new("caneca", "canecas", 300m, OrigemMedida.Direta, "8200101", 434, "Caneca de 300ml"),
            ],
            ["refrigerante-de-guarana"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "8200301", 439, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "8200301", 439, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "8200301", 439, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "8200301", 439, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Direta, "8200301", 439, "Copo tulipa"),
                new("xícara de café", "xícaras de café", 50m, OrigemMedida.Direta, "8200301", 440, "Xícara de café"),
                new("caneca", "canecas", 300m, OrigemMedida.Direta, "8200301", 439, "Caneca de 300ml"),
            ],
            ["cerveja-pilsen"] =
            [
                new("copo americano", "copos americanos", 150m, OrigemMedida.Direta, "8300101", 464, "Copo americano"),
                new("copo médio", "copos médios", 240m, OrigemMedida.Direta, "8300101", 464, "Copo médio"),
                new("copo grande", "copos grandes", 300m, OrigemMedida.Direta, "8300101", 464, "Copo grande"),
                new("copo de requeijão", "copos de requeijão", 240m, OrigemMedida.Direta, "8300101", 464, "Copo de requeijão"),
                new("copo tulipa", "copos tulipa", 290m, OrigemMedida.Direta, "8300101", 464, "Copo tulipa"),
                new("caneca", "canecas", 300m, OrigemMedida.Direta, "8300101", 464, "Caneca de 300ml"),
            ],
        };
}
