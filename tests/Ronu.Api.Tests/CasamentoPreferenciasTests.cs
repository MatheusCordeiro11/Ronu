using Ronu.Api.Data;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Tabela de categorias do "evitar" (aprovada em 2026-10-25) e casamento por nome.
/// Cada linha da tabela tem um teste: todas as expressões dela (com acento,
/// maiúsculas e plural) tiram exatamente os itens esperados, calculados aqui de
/// forma independente da tabela. Contra a base inteira (com ovo frito e abobrinha
/// refogada, que só saem na lista da pessoa).
/// </summary>
public class CasamentoPreferenciasTests
{
    private static readonly IReadOnlyList<AlimentoBase> Base = BaseNutricional.Itens;

    private static IEnumerable<string> Onde(Func<AlimentoBase, bool> filtro) => Base.Where(filtro).Select(i => i.Id);
    private static IEnumerable<string> Categoria(params CategoriaAlimento[] categorias) => Onde(i => categorias.Contains(i.Categoria));
    private static IEnumerable<string> ComAlergeno(Alergenos a) => Onde(i => i.Alergenos.HasFlag(a));

    private static readonly CategoriaAlimento[] Carnes =
        [CategoriaAlimento.CarneBovina, CategoriaAlimento.CarneSuina, CategoriaAlimento.Aves, CategoriaAlimento.Peixes];

    private static void Tira(IEnumerable<string> esperado, params string[] textos)
    {
        var ids = esperado.ToHashSet();
        Assert.NotEmpty(ids);
        foreach (var texto in textos)
        {
            var resultado = CasamentoPreferencias.Casar(texto, Base);
            Assert.True(ids.SetEquals(resultado.ItensIds),
                $"'{texto}': esperado {string.Join(", ", ids.Order())}; veio {string.Join(", ", resultado.ItensIds.Order())}");
            Assert.Empty(resultado.TrechosSemCasamento);
        }
    }

    // Uma linha nova na tabela precisa de um teste novo aqui (e este número muda junto).
    [Fact]
    public void Tabela_Tem_27_Linhas_Todas_Testadas() => Assert.Equal(27, CasamentoPreferencias.Tabela.Count);

    [Fact]
    public void Carne_Sozinha_Tira_Todas_As_Carnes_E_Peixes()
    {
        Tira(Categoria(Carnes), "carne", "Carnes", "não como carne", "vegetariano", "Vegetariana");
        Assert.Equal(25, Categoria(Carnes).Count());
    }

    // A expressão mais longa vence: "carne vermelha" não aciona também "carne".
    [Fact]
    public void Carne_Vermelha_Tira_Bovinos_E_Suinos() =>
        Tira(Categoria(CategoriaAlimento.CarneBovina, CategoriaAlimento.CarneSuina), "carne vermelha", "Carnes vermelhas", "Não como carne vermelha");

    [Fact]
    public void Carne_Branca_Tira_Aves_Peixes_E_Suinos() =>
        Tira(Categoria(CategoriaAlimento.Aves, CategoriaAlimento.Peixes, CategoriaAlimento.CarneSuina), "carne branca", "Carnes brancas");

    [Fact]
    public void Carne_Bovina_Tira_Os_Bovinos() =>
        Tira(Categoria(CategoriaAlimento.CarneBovina), "carne bovina", "Boi", "bovinos", "carne de vaca", "Carne de boi", "Bovina");

    // Nome composto da base ("Carne moída de acém"): sem a linha própria, "carne" tiraria aves e peixes.
    [Fact]
    public void Carne_Moida_Tira_Os_Bovinos() =>
        Tira(Categoria(CategoriaAlimento.CarneBovina), "carne moída", "Carne moída de acém");

    [Fact]
    public void Porco_Tira_Os_Suinos() =>
        Tira(Categoria(CategoriaAlimento.CarneSuina), "porco", "Carne de porco", "suínos", "carne suína");

    [Fact]
    public void Frango_Tira_As_Aves() =>
        Tira(Categoria(CategoriaAlimento.Aves), "frango", "Aves", "carne de frango", "Frango grelhado");

    [Fact]
    public void Galinha_Tira_Aves_E_Ovos() =>
        Tira(Categoria(CategoriaAlimento.Aves, CategoriaAlimento.Ovos), "galinha", "Galinhas");

    [Fact]
    public void Peixe_Tira_Os_Peixes() =>
        Tira(Categoria(CategoriaAlimento.Peixes), "peixe", "Peixes", "pescado");

    [Fact]
    public void Frutos_Do_Mar_Tira_Os_Peixes() =>
        Tira(Categoria(CategoriaAlimento.Peixes), "frutos do mar", "Camarão", "mariscos", "crustáceos");

    // Nos alérgenos, um nome composto ("leite integral") continua tirando a família inteira.
    [Fact]
    public void Leite_Tira_Tudo_Que_Tem_Lactose()
    {
        Tira(ComAlergeno(Alergenos.Lactose), "leite", "Lactose", "intolerância à lactose", "laticínios", "derivados do leite", "lácteos", "Leite integral");
        Assert.Equal(13, ComAlergeno(Alergenos.Lactose).Count());
    }

    [Fact]
    public void Queijo_Tira_Queijos_Ricota_Requeijao_E_Pao_De_Queijo() =>
        Tira(["queijo-minas-frescal", "queijo-mucarela", "queijo-prato", "queijo-parmesao", "ricota", "requeijao-cremoso", "pao-de-queijo-assado"],
            "queijo", "Queijos", "queijo muçarela", "pão de queijo");

    [Fact]
    public void Iogurte_Tira_Os_Iogurtes() =>
        Tira(["iogurte-natural", "iogurte-natural-desnatado"], "iogurte", "Iogurtes", "iogurte natural");

    [Fact]
    public void Gluten_Tira_Tudo_Que_Tem_Gluten() =>
        Tira(ComAlergeno(Alergenos.Gluten), "glúten", "celíaco", "Celíaca", "trigo", "cevada", "centeio");

    [Fact]
    public void Ovo_Tira_Tudo_Que_Tem_Ovo() =>
        Tira(ComAlergeno(Alergenos.Ovo), "ovo", "Ovos", "ovo de galinha", "clara de ovo");

    [Fact]
    public void Amendoim_E_Castanhas_Tiram_A_Mesma_Familia() =>
        Tira(ComAlergeno(Alergenos.Amendoim), "amendoim", "castanhas", "nozes", "noz", "oleaginosas", "amêndoa", "Castanha-de-caju",
            "castanha-do-pará", "pasta de amendoim", "caju");

    [Fact]
    public void Semente_Tira_A_Linhaca() => Tira(["linhaca"], "semente", "Sementes");

    [Fact]
    public void Soja_Tira_O_Oleo_De_Soja() => Tira(["oleo-de-soja"], "soja", "óleo de soja");

    [Fact]
    public void Milho_Tira_Cuscuz_E_Milho_Verde() => Tira(["cuscuz-de-milho-cozido", "milho-verde-em-conserva"], "milho");

    [Fact]
    public void Doce_Tira_Os_Acucares_E_Nao_A_Batata_Doce()
    {
        string[] acucares = ["acucar-refinado", "acucar-mascavo", "mel", "refrigerante-de-cola", "refrigerante-de-guarana"];
        Tira(acucares, "açúcar", "doce", "Doces", "açúcares");
        Assert.DoesNotContain("batata-doce-cozida", CasamentoPreferencias.Casar("doce", Base).ItensIds);
    }

    [Fact]
    public void Batata_Doce_Tira_So_A_Batata_Doce() =>
        Tira(["batata-doce-cozida"], "batata doce", "Batata-doce", "batatas-doces", "batata doce cozida");

    [Fact]
    public void Acai_Sem_Acucar_Tira_So_O_Acai() => Tira(["acai-sem-acucar"], "açaí sem açúcar");

    [Fact]
    public void Cafeina_Tira_Cafe_Chas_Com_Cafeina_E_Refrigerantes() =>
        Tira(["cafe-coado", "cha-mate", "cha-preto", "refrigerante-de-cola", "refrigerante-de-guarana"], "cafeína");

    [Fact]
    public void Cafe_Tira_So_O_Cafe() => Tira(["cafe-coado"], "café", "Cafés", "café coado");

    [Fact]
    public void Refrigerante_Tira_Os_Dois_Refrigerantes() =>
        Tira(["refrigerante-de-cola", "refrigerante-de-guarana"], "refrigerante", "Refrigerantes");

    [Fact]
    public void Alcool_Tira_A_Cerveja() => Tira(Categoria(CategoriaAlimento.BebidasAlcoolicas), "álcool", "bebida alcoólica", "Bebidas");

    [Fact]
    public void Vegano_Tira_Carnes_Ovo_Lactose_E_Mel() =>
        Tira(Onde(i => Carnes.Contains(i.Categoria) || i.Alergenos.HasFlag(Alergenos.Ovo) || i.Alergenos.HasFlag(Alergenos.Lactose) || i.Id == "mel"),
            "vegano", "Vegana");

    // ---------- Fora da tabela ----------

    // A sobra sem item volta como palavra, para o aviso da tela; a carne sai do mesmo jeito.
    [Theory]
    [InlineData("carne de cordeiro", "cordeiro")]
    [InlineData("carne sem gordura", "gordura")]
    public void Carne_De_Cordeiro_Tira_As_Carnes_E_Devolve_A_Sobra(string texto, string sobra)
    {
        var resultado = CasamentoPreferencias.Casar(texto, Base);

        Assert.True(Categoria(Carnes).ToHashSet().SetEquals(resultado.ItensIds));
        Assert.Equal([sobra], resultado.TrechosSemCasamento);
    }

    // ---------- Sobra da tabela casada pelo nome ----------

    [Fact]
    public void Frango_Com_Batata_Tira_Aves_E_Batatas() =>
        Tira(Onde(i => i.Categoria == CategoriaAlimento.Aves || i.Id is "batata-inglesa-cozida" or "batata-doce-cozida"), "frango com batata");

    [Fact]
    public void Banana_Maca_Sem_Virgula_Tira_Cada_Uma() =>
        Tira(["banana-prata", "banana-nanica", "maca-fuji"], "banana maçã");

    [Fact]
    public void Porco_Feijao_Sem_Virgula_Tira_Suinos_E_Feijoes() =>
        Tira(Onde(i => i.Categoria == CategoriaAlimento.CarneSuina || i.Id is "feijao-cozido" or "feijao-preto-cozido" or "feijao-fradinho-cozido"),
            "porco feijão");

    [Fact]
    public void Carne_Vermelha_Nem_Peixe_Tira_Bovinos_Suinos_E_Peixes() =>
        Tira(Categoria(CategoriaAlimento.CarneBovina, CategoriaAlimento.CarneSuina, CategoriaAlimento.Peixes), "não como carne vermelha nem peixe");

    // O trecho inteiro como nome de um item vem antes da sobra: "pão de queijo" não tira os outros pães, e
    // "leite integral" não tira o arroz integral.
    [Fact]
    public void Nome_Composto_Inteiro_Nao_Espalha_A_Sobra()
    {
        var paoDeQueijo = CasamentoPreferencias.Casar("pão de queijo", Base).ItensIds;
        Assert.DoesNotContain("pao-frances", paoDeQueijo);
        Assert.DoesNotContain("pao-de-forma-integral", paoDeQueijo);

        var leiteIntegral = CasamentoPreferencias.Casar("leite integral", Base).ItensIds;
        Assert.DoesNotContain("arroz-integral-cozido", leiteIntegral);
        Assert.DoesNotContain("pasta-de-amendoim-integral", leiteIntegral);
    }

    [Fact]
    public void Palavra_Com_Erro_No_Meio_Volta_Sozinha()
    {
        var resultado = CasamentoPreferencias.Casar("banana cordeiro", Base);

        Assert.True(new[] { "banana-prata", "banana-nanica" }.ToHashSet().SetEquals(resultado.ItensIds));
        Assert.Equal(["cordeiro"], resultado.TrechosSemCasamento);
    }

    [Theory]
    [InlineData("banana", "banana-prata", "banana-nanica")]
    [InlineData("Bananas", "banana-prata", "banana-nanica")]
    [InlineData("arroz integral", "arroz-integral-cozido")]
    [InlineData("batata", "batata-inglesa-cozida", "batata-doce-cozida")]
    [InlineData("não como feijão", "feijao-cozido", "feijao-preto-cozido", "feijao-fradinho-cozido")]
    [InlineData("aipim", "mandioca-cozida")]
    [InlineData("Macaxeira", "mandioca-cozida")]
    [InlineData("suco de laranja", "suco-de-laranja-natural")]
    // Falsos positivos aceitos (tira mais): a laranja pera e a couve-flor.
    [InlineData("pera", "pera-williams", "laranja-pera")]
    [InlineData("laranja", "laranja-pera", "suco-de-laranja-natural")]
    [InlineData("couve", "couve-crua", "couve-refogada", "couve-flor-cozida")]
    public void Palavra_Fora_Da_Tabela_Casa_Pelo_Nome(string texto, params string[] esperado) => Tira(esperado, texto);

    [Fact]
    public void Trechos_Separados_Casam_Cada_Um() =>
        Tira(Onde(i => i.Id is "banana-prata" or "banana-nanica" or "maca-fuji" || i.Categoria == CategoriaAlimento.CarneSuina),
            "banana, maçã e porco", "Banana; maçã / porco");

    // "porko" agora casa com "porco" (tolerância a 1 letra); "bananna" não, porque "banana" não é da tabela.
    [Fact]
    public void Porko_Casa_Com_Porco() => Tira(Categoria(CategoriaAlimento.CarneSuina), "porko");

    [Theory]
    [InlineData("bananna")]
    [InlineData("cordeiro")]
    [InlineData("whey protein")]
    public void Sem_Casamento_Devolve_O_Trecho(string texto)
    {
        var resultado = CasamentoPreferencias.Casar(texto, Base);

        Assert.False(resultado.Casou);
        Assert.Equal([texto], resultado.TrechosSemCasamento);
    }

    // ---------- Tolerância a 1 letra (palavras da tabela com 5 letras ou mais) ----------

    [Fact]
    public void Lactoze_Casa_Com_Lactose() => Tira(ComAlergeno(Alergenos.Lactose), "lactoze");

    [Fact]
    public void Glutem_Casa_Com_Gluten() => Tira(ComAlergeno(Alergenos.Gluten), "glutem");

    // Duas letras vizinhas invertidas contam como 1.
    [Fact]
    public void Gluetn_Casa_Com_Gluten() => Tira(ComAlergeno(Alergenos.Gluten), "gluetn");

    [Fact]
    public void Amendoin_Casa_Com_Amendoim() => Tira(ComAlergeno(Alergenos.Amendoim), "amendoin");

    [Fact]
    public void Castanah_Casa_Com_Castanha() => Tira(ComAlergeno(Alergenos.Amendoim), "castanah");

    [Theory]
    [InlineData("lactosse", "lactose")]  // uma letra a mais
    [InlineData("frago", "frango")]      // uma a menos
    [InlineData("pexie", "peixe")]       // vizinhas invertidas
    public void Uma_Letra_De_Distancia_Casa(string doTexto, string daTabela) =>
        Assert.True(CasamentoPreferencias.PalavraCasa(doTexto, daTabela));

    [Theory]
    [InlineData("ovu", "ovo")]           // menos de 5 letras: só igual
    [InlineData("doci", "doce")]
    [InlineData("pouco", "porco")]       // palavra vazia nunca casa por aproximação
    [InlineData("lactozze", "lactose")]  // 2 letras de distância
    [InlineData("lgutne", "gluten")]     // vizinhas invertidas duas vezes
    public void Fora_Da_Tolerancia_Nao_Casa(string doTexto, string daTabela) =>
        Assert.False(CasamentoPreferencias.PalavraCasa(doTexto, daTabela));

    [Fact]
    public void Como_Pouco_Porco_Nao_Tira_Os_Suinos_Pelo_Pouco()
    {
        Assert.Empty(CasamentoPreferencias.Casar("como pouco", Base).ItensIds);
    }

    [Theory]
    [InlineData("feijões", "feijao")]
    [InlineData("pães", "pao")]
    [InlineData("açúcares", "acucar")]
    [InlineData("nozes", "noz")]
    [InlineData("ovos", "ovo")]
    [InlineData("doces", "doce")]
    public void Plural_Vira_Singular(string palavra, string singular) =>
        Assert.Equal([singular], CasamentoPreferencias.Palavras(palavra));
}
