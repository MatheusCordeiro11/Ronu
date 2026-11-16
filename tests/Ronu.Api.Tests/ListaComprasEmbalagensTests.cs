using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.ListaCompras;

namespace Ronu.Api.Tests;

/// <summary>
/// Embalagens na lista de compras (2026-11-16): a escolha do tamanho (função pura, sobre a quantidade exata), o
/// limite de 4 embalagens, a peça do frango inteiro e a regressão do contrato (Quantidade, Unidade e Texto iguais
/// aos de antes da mudança, para os 116 itens).
/// </summary>
public class ListaComprasEmbalagensTests
{
    private static ItemCompra Compra(string chave) => ConversaoCompras.Itens.Single(i => i.Chave == chave);

    private static EmbalagemListaComprasDto? Embalagem(string chave, decimal exato) => MontadorListaCompras.Embalagem(exato, Compra(chave));

    // ---------- Escolha do tamanho ----------

    [Fact]
    public void Arroz_De_612_Gramas_Vira_1_Pacote_De_1_Kg_Com_A_Sobra()
    {
        var e = Embalagem("arroz-branco", 612.4m)!;

        Assert.Equal("embalagem", e.Tipo);
        Assert.Equal("1 pacote de 1 kg", e.Texto);
        Assert.Equal(1, e.Quantidade);
        Assert.Equal("pacote", e.Nome);
        Assert.Equal("1 kg", e.Rotulo);
        Assert.Equal(1000m, e.TamanhoTotal);
        Assert.Equal(387.6m, e.Sobra);
        Assert.Equal("390 g", e.SobraTexto);
        Assert.Contains("Open Food Facts", e.Fonte);
        Assert.Contains("26 de 74", e.Fonte);
        Assert.Contains("ODbL", e.Fonte);
    }

    [Fact]
    public void Abaixo_Do_Menor_Tamanho_E_1_Embalagem_Do_Menor()
    {
        Assert.Equal("1 pacote de 50 g", Embalagem("queijo-parmesao", 30m)!.Texto);
    }

    [Fact]
    public void Exatamente_O_Tamanho_E_1_Embalagem_Sem_Sobra()
    {
        var e = Embalagem("macarrao", 500m)!;

        Assert.Equal("1 pacote de 500 g", e.Texto);
        Assert.Equal(0m, e.Sobra);
        Assert.Equal("", e.SobraTexto);
    }

    // 165 g exatos viram 200 g na linha (passos de 50 acima de 100), mas cabem num pote de 170 g.
    [Fact]
    public void A_Escolha_Parte_Do_Exato_E_Nao_Do_Arredondado()
    {
        Assert.Equal(200m, MontadorListaCompras.Arredondar(165m, UnidadeCompra.Gramas));
        Assert.Equal("1 pote de 170 g", Embalagem("iogurte-natural-desnatado", 165m)!.Texto);
    }

    [Fact]
    public void Ovos_Em_Duzias_E_O_Resto_De_Maquina_Nao_Pede_Outra_Duzia()
    {
        Assert.Equal("3 dúzias", Embalagem("ovos", 31m)!.Texto);
        Assert.Equal("dúzias", Embalagem("ovos", 31m)!.Nome);
        Assert.Equal("", Embalagem("ovos", 31m)!.Rotulo);
        Assert.Equal("5 ovos", Embalagem("ovos", 31m)!.SobraTexto);
        Assert.Equal("1 dúzia", Embalagem("ovos", 12.0000000000000000000001m)!.Texto);
        Assert.Contains("convenção", Embalagem("ovos", 12m)!.Fonte);
    }

    // Empate no total (2 de 500 g ou 1 de 1 kg): fica o de menos embalagens.
    [Fact]
    public void Entre_Dois_Tamanhos_Com_O_Mesmo_Total_Fica_O_De_Menos_Embalagens()
    {
        Assert.Equal("1 pacote de 1 kg", Embalagem("arroz-integral", 700m)!.Texto);
        Assert.Equal("1 pacote de 500 g", Embalagem("arroz-integral", 450m)!.Texto);
    }

    [Fact]
    public void Acima_Do_Maior_Tamanho_Sao_Varias_Embalagens_Ate_4()
    {
        var e = Embalagem("macarrao", 1800m)!;

        Assert.Equal("4 pacotes de 500 g", e.Texto);
        Assert.Equal(4, e.Quantidade);
        Assert.Equal(2000m, e.TamanhoTotal);
    }

    [Fact]
    public void Acima_De_4_Embalagens_A_Linha_Volta_Aos_Gramas()
    {
        Assert.Null(Embalagem("macarrao", 2001m));
        Assert.Null(Embalagem("leite-integral", 6500m));
        Assert.Null(Embalagem("ovos", 49m));
    }

    // O tamanho que passaria de 4 embalagens sai antes da escolha: com 500 g e 1 kg, 2,4 kg dão 3 pacotes de 1 kg
    // (e não nulo, como daria escolher primeiro os 5 pacotes de 500 g e só depois olhar o limite).
    [Fact]
    public void Tamanho_Acima_Do_Limite_Sai_Antes_Da_Escolha()
    {
        Assert.Equal("3 pacotes de 1 kg", Embalagem("feijao-carioca", 2400m)!.Texto);
        Assert.Equal("1 pacote de 5 kg", Embalagem("arroz-branco", 4500m)!.Texto);
        Assert.Equal("1 garrafa de 2 L", Embalagem("refrigerante-de-guarana", 1500m)!.Texto);
        Assert.Equal("500 ml", Embalagem("refrigerante-de-guarana", 1500m)!.SobraTexto);
    }

    [Fact]
    public void Latas_Ganham_Da_Garrafa_Quando_Sobra_Menos()
    {
        Assert.Equal("2 latas de 350 ml", Embalagem("refrigerante-de-guarana", 600m)!.Texto);
        Assert.Equal("1 lata de 350 ml", Embalagem("cerveja-pilsen", 350m)!.Texto);
    }

    [Fact]
    public void Pao_De_Forma_Conta_Fatias_E_Mostra_O_Pacote_Em_Gramas()
    {
        var e = Embalagem("pao-de-forma", 12m)!;

        Assert.Equal("1 pacote de 400 g", e.Texto);
        Assert.Equal(16m, e.TamanhoTotal);
        Assert.Equal("4 fatias", e.SobraTexto);
        Assert.Contains("25 g por fatia", e.Fonte);
        Assert.Equal("1 pacote de 500 g", Embalagem("pao-de-forma", 17m)!.Texto);
    }

    [Fact]
    public void Leite_Em_Caixas_De_1_L_E_A_Sobra_Em_Litros()
    {
        var e = Embalagem("leite-integral", 2300m)!;

        Assert.Equal("3 caixas de 1 L", e.Texto);
        Assert.Equal("700 ml", e.SobraTexto);
        Assert.Equal("1,3 L", MontadorListaCompras.TextoSobra(1300m, Compra("leite-integral")));
    }

    [Theory]
    [InlineData("patinho", 600)]
    [InlineData("banana-prata", 7)]
    [InlineData("suco-de-laranja", 450)]
    [InlineData("atum", 120)]
    [InlineData("azeite", 250)]
    [InlineData("castanha-de-caju", 100)]
    public void Item_Sem_Tamanho_Nao_Tem_Embalagem(string chave, int exato)
    {
        Assert.Null(Embalagem(chave, exato));
    }

    // ---------- Frango inteiro ----------

    [Fact]
    public void Frango_Inteiro_Abaixo_Da_Peca_Minima_Mostra_A_Peca()
    {
        var e = Embalagem("frango-inteiro", 450m)!;

        Assert.Equal("peca", e.Tipo);
        Assert.Equal("1 peça de 1 kg ou mais", e.Texto);
        Assert.Equal("peça", e.Nome);
        Assert.Equal("1 kg ou mais", e.Rotulo);
        Assert.Equal(1000m, e.TamanhoTotal);
        Assert.Equal(550m, e.Sobra);
        Assert.Equal("550 g", e.SobraTexto);
        // "Sobram pelo menos": para baixo (447 g deixam pelo menos 550 g, e não "cerca de 550").
        Assert.Equal("550 g", Embalagem("frango-inteiro", 447m)!.SobraTexto);
        Assert.Equal("540 g", Embalagem("frango-inteiro", 451m)!.SobraTexto);
        Assert.Contains("1 kg ou mais", e.Fonte);
        Assert.Contains("calibração", e.Fonte);
    }

    [Fact]
    public void Frango_Inteiro_Acima_Da_Peca_Minima_Fica_Em_Gramas()
    {
        Assert.Null(Embalagem("frango-inteiro", 1000m));
        Assert.Null(Embalagem("frango-inteiro", 1850m));
    }

    // ---------- Na lista montada ----------

    private static AlimentoDto Alimento(string id, decimal quantidade)
    {
        var item = BaseNutricional.Itens.Single(i => i.Id == id);
        return new AlimentoDto { Nome = item.Nome, Quantidade = quantidade, Unidade = item.Medida, AlimentoId = id };
    }

    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    private static DietaSemanalDto Dieta(Func<int, List<AlimentoDto>> doDia) => new()
    {
        Dias = Enumerable.Range(0, 7).Select(d => new DiaDietaDto
        {
            DiaSemana = $"Dia {d + 1}",
            Refeicoes = [new RefeicaoDto { Nome = "Almoço", Alimentos = doDia(d), Macros = Zero() }],
            TotalDoDia = Zero(),
            MetaCalculada = Zero()
        }).ToList()
    };

    [Fact]
    public void Lista_Montada_Traz_A_Embalagem_Sem_Mudar_Quantidade_Unidade_E_Texto()
    {
        // 200 g de arroz cozido por dia: 1.400 g cozidos, 499,24 g crus pelo par da TACO, 500 g na linha.
        var lista = MontadorListaCompras.Montar(Dieta(_ => [Alimento("arroz-branco-cozido", 200)]), "SP");
        var arroz = lista.Secoes.SelectMany(s => s.Itens).Single(i => i.Chave == "arroz-branco");

        Assert.Equal(500m, arroz.Quantidade);
        Assert.Equal("g", arroz.Unidade);
        Assert.Equal("500 g", arroz.Texto);
        Assert.Equal("1 pacote de 1 kg", arroz.Embalagem!.Texto);
        Assert.Equal(Math.Round(1000m - arroz.Detalhe.QuantidadeExata, 2), arroz.Embalagem.Sobra, 1);
    }

    // ---------- Regressão do contrato ----------

    // Retrato tirado com o código anterior às embalagens (main 17cf2ef), com a mesma dieta de referência: todos os
    // itens da base, em quantidades variadas, em 2 de cada 3 dias. As embalagens não podem mudar nenhum destes valores.
    private static readonly Dictionary<string, (decimal Quantidade, string Unidade, string Texto)> Antes = new()
    {
        ["alface"] = (500m, "g", "500 g"),
        ["rucula"] = (500m, "g", "500 g"),
        ["agriao"] = (400m, "g", "400 g"),
        ["couve"] = (350m, "g", "350 g"),
        ["repolho"] = (400m, "g", "400 g"),
        ["repolho-roxo"] = (300m, "g", "300 g"),
        ["acelga"] = (400m, "g", "400 g"),
        ["tomate"] = (4m, "unidades", "4 tomates"),
        ["pepino"] = (6m, "unidades", "6 pepinos"),
        ["cebola"] = (7m, "unidades", "7 cebolas"),
        ["cenoura"] = (5m, "unidades", "5 cenouras"),
        ["beterraba"] = (850m, "g", "850 g"),
        ["brocolis"] = (150m, "g", "150 g"),
        ["couve-flor"] = (300m, "g", "300 g"),
        ["abobrinha"] = (450m, "g", "450 g"),
        ["chuchu"] = (400m, "g", "400 g"),
        ["abobora"] = (450m, "g", "450 g"),
        ["berinjela"] = (250m, "g", "250 g"),
        ["abobrinha-refogada"] = (350m, "g", "350 g"),
        ["couve-refogada"] = (450m, "g", "450 g"),
        ["espinafre-refogado"] = (500m, "g", "500 g"),
        ["batata"] = (200m, "g", "200 g"),
        ["batata-doce"] = (400m, "g", "400 g"),
        ["mandioca"] = (600m, "g", "600 g"),
        ["mandioquinha"] = (300m, "g", "300 g"),
        ["cara"] = (250m, "g", "250 g"),
        ["banana-prata"] = (7m, "unidades", "7 bananas"),
        ["banana-nanica"] = (650m, "g", "650 g"),
        ["maca"] = (2m, "unidades", "2 maçãs"),
        ["pera"] = (3m, "unidades", "3 peras"),
        ["laranja"] = (4m, "unidades", "4 laranjas"),
        ["tangerina"] = (3m, "unidades", "3 tangerinas"),
        ["kiwi"] = (4m, "unidades", "4 kiwis"),
        ["goiaba"] = (4m, "unidades", "4 goiabas"),
        ["mamao-papaia"] = (650m, "g", "650 g"),
        ["mamao-formosa"] = (750m, "g", "750 g"),
        ["abacaxi"] = (950m, "g", "950 g"),
        ["melancia"] = (500m, "g", "500 g"),
        ["melao"] = (750m, "g", "750 g"),
        ["morango"] = (500m, "g", "500 g"),
        ["uva"] = (400m, "g", "400 g"),
        ["manga"] = (350m, "g", "350 g"),
        ["abacate"] = (650m, "g", "650 g"),
        ["patinho"] = (550m, "g", "550 g"),
        ["patinho-moido"] = (900m, "g", "900 g"),
        ["acem-moido"] = (600m, "g", "600 g"),
        ["acem"] = (600m, "g", "600 g"),
        ["alcatra"] = (600m, "g", "600 g"),
        ["contrafile"] = (700m, "g", "700 g"),
        ["coxao-mole"] = (700m, "g", "700 g"),
        ["lagarto"] = (600m, "g", "600 g"),
        ["musculo"] = (500m, "g", "500 g"),
        ["maminha"] = (900m, "g", "900 g"),
        ["file-mignon"] = (600m, "g", "600 g"),
        ["peito-de-frango"] = (950m, "g", "950 g"),
        ["coxa-de-frango"] = (800m, "g", "800 g"),
        ["sobrecoxa-de-frango"] = (650m, "g", "650 g"),
        ["frango-inteiro"] = (850m, "g", "850 g"),
        ["lombo"] = (800m, "g", "800 g"),
        ["bisteca"] = (550m, "g", "550 g"),
        ["pernil"] = (700m, "g", "700 g"),
        ["tilapia"] = (500m, "g", "500 g"),
        ["salmao"] = (600m, "g", "600 g"),
        ["sardinha"] = (900m, "g", "900 g"),
        ["merluza"] = (500m, "g", "500 g"),
        ["corvina"] = (800m, "g", "800 g"),
        ["pao-frances"] = (7m, "unidades", "7 pães"),
        ["pao-de-forma"] = (17m, "fatias", "17 fatias"),
        ["ovos"] = (27m, "unidades", "27 ovos"),
        ["leite-integral"] = (650m, "ml", "650 ml"),
        ["leite-desnatado"] = (400m, "ml", "400 ml"),
        ["iogurte-natural"] = (300m, "g", "300 g"),
        ["iogurte-natural-desnatado"] = (500m, "g", "500 g"),
        ["queijo-minas-frescal"] = (550m, "g", "550 g"),
        ["queijo-mucarela"] = (400m, "g", "400 g"),
        ["queijo-prato"] = (350m, "g", "350 g"),
        ["queijo-parmesao"] = (400m, "g", "400 g"),
        ["ricota"] = (550m, "g", "550 g"),
        ["requeijao"] = (350m, "g", "350 g"),
        ["manteiga"] = (500m, "g", "500 g"),
        ["arroz-branco"] = (70m, "g", "70 g"),
        ["arroz-integral"] = (150m, "g", "150 g"),
        ["macarrao"] = (150m, "g", "150 g"),
        ["flocao"] = (90m, "g", "90 g"),
        ["fecula-de-mandioca"] = (500m, "g", "500 g"),
        ["farinha-de-mandioca"] = (450m, "g", "450 g"),
        ["aveia"] = (500m, "g", "500 g"),
        ["milho-em-conserva"] = (500m, "g", "500 g"),
        ["feijao-carioca"] = (80m, "g", "80 g"),
        ["feijao-preto"] = (70m, "g", "70 g"),
        ["feijao-fradinho"] = (150m, "g", "150 g"),
        ["lentilha"] = (150m, "g", "150 g"),
        ["atum"] = (350m, "g", "350 g"),
        ["pasta-de-amendoim"] = (300m, "g", "300 g"),
        ["amendoim"] = (550m, "g", "550 g"),
        ["castanha-de-caju"] = (500m, "g", "500 g"),
        ["castanha-do-para"] = (200m, "g", "200 g"),
        ["amendoa"] = (400m, "g", "400 g"),
        ["noz"] = (600m, "g", "600 g"),
        ["linhaca"] = (350m, "g", "350 g"),
        ["azeite"] = (250m, "g", "250 g"),
        ["oleo-de-soja"] = (450m, "g", "450 g"),
        ["acucar-refinado"] = (400m, "g", "400 g"),
        ["acucar-mascavo"] = (350m, "g", "350 g"),
        ["mel"] = (400m, "g", "400 g"),
        ["cafe-em-po"] = (60m, "g", "60 g"),
        ["cha-mate"] = (2m, "saches", "2 sachês"),
        ["cha-preto"] = (2m, "saches", "2 sachês"),
        ["cha-de-erva-doce"] = (3m, "saches", "3 sachês"),
        ["suco-de-laranja"] = (450m, "ml", "450 ml"),
        ["agua-de-coco"] = (150m, "ml", "150 ml"),
        ["refrigerante-de-cola"] = (400m, "ml", "400 ml"),
        ["refrigerante-de-guarana"] = (600m, "ml", "600 ml"),
        ["cerveja-pilsen"] = (350m, "ml", "350 ml"),
        ["acai"] = (300m, "g", "300 g"),
        ["pao-de-queijo"] = (27m, "unidades", "27 pães de queijo"),
    };

    [Fact]
    public void Quantidade_Unidade_E_Texto_Sao_Os_De_Antes_Das_Embalagens_Nos_116_Itens()
    {
        var dieta = Dieta(d => BaseNutricional.Itens
            .Select((item, i) => (item, i))
            .Where(x => (x.i + d) % 3 != 0)
            .Select(x => Alimento(x.item.Id, 15 + (x.i * 37 + d * 11) % 140))
            .ToList());

        var itens = MontadorListaCompras.Montar(dieta, "SP").Secoes.SelectMany(s => s.Itens).ToList();

        Assert.Equal(116, itens.Count);
        Assert.Equal(Antes.Keys.Order(), itens.Select(i => i.Chave).Order());
        Assert.All(itens, i => Assert.Equal(Antes[i.Chave], (i.Quantidade, i.Unidade, i.Texto)));
        // E a mesma dieta tem itens com embalagem e itens sem.
        Assert.Contains(itens, i => i.Embalagem is not null);
        Assert.Contains(itens, i => i.Embalagem is null);
    }
}
