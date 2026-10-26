using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Lista fechada de alimentos por pessoa (fase 2b-1): nome regional da UF,
/// "evitar", alérgenos, cerveja só para quem pediu, itens que saem da lista e
/// mínimos por papel no prato.
/// </summary>
public class ListaAlimentosPessoaTests
{
    private static PreferenciaContextoDto Gosto(string alimento) => new() { Alimento = alimento, Tipo = "preferido" };
    private static PreferenciaContextoDto Evitar(string alimento) => new() { Alimento = alimento, Tipo = "evitar" };

    private static ListaAlimentos Montar(string? uf = null, Alergenos alergias = Alergenos.Nenhum, params PreferenciaContextoDto[] prefs) =>
        ListaAlimentosPessoa.Montar(uf, prefs, alergias);

    private static HashSet<string> Ids(ListaAlimentos lista) => lista.Itens.Select(i => i.Item.Id).ToHashSet();

    [Theory]
    [InlineData("RJ", "Aipim cozido")]
    [InlineData("pe", "Macaxeira cozida")]
    [InlineData("BA", "Macaxeira cozida")]
    [InlineData("SP", "Mandioca cozida")]
    [InlineData("RS", "Mandioca cozida")]
    [InlineData(null, "Mandioca cozida")]
    public void Mandioca_Usa_O_Nome_Da_Uf_E_So_Ele(string? uf, string nome)
    {
        var nomes = Montar(uf).Itens.Where(i => i.Item.Id == "mandioca-cozida").Select(i => i.Nome);

        Assert.Equal([nome], nomes);
    }

    // Os dois feijões em todas as UFs: valores diferentes, não são apelidos um do outro.
    [Theory]
    [InlineData("RS")]
    [InlineData("SP")]
    public void Os_Dois_Feijoes_Entram_Em_Toda_Uf(string uf)
    {
        var ids = Ids(Montar(uf));

        Assert.Contains("feijao-cozido", ids);
        Assert.Contains("feijao-preto-cozido", ids);
    }

    [Fact]
    public void Abobrinha_Refogada_E_Ovo_Frito_Ficam_Fora_E_Patinho_Moido_Perde_O_Refogado()
    {
        var lista = Montar();

        Assert.DoesNotContain("abobrinha-refogada", Ids(lista));
        Assert.DoesNotContain("ovo-de-galinha-frito", Ids(lista));
        Assert.Contains("couve-refogada", Ids(lista));
        Assert.Contains("espinafre-refogado", Ids(lista));
        Assert.Contains(lista.Itens, i => i.Nome == "Patinho bovino moído");
        Assert.DoesNotContain(lista.Itens, i => i.Nome.Contains("moído refogado"));
    }

    [Fact]
    public void Sem_Preferencias_Cabe_No_Teto_E_Sem_Cerveja()
    {
        var lista = Montar("SP");

        Assert.Equal(BaseNutricional.Itens.Count - 3, lista.Itens.Count); // abobrinha refogada, ovo frito e cerveja
        Assert.True(lista.Itens.Count <= BaseNutricional.TetoDeNomes);
        Assert.Equal(lista.Itens.Count, lista.Itens.Select(i => i.Nome).Distinct().Count());
        Assert.True(lista.Minimos.Atende);
    }

    [Fact]
    public void Cerveja_So_Entra_Para_Quem_Marcou_Como_Gosto()
    {
        Assert.DoesNotContain("cerveja-pilsen", Ids(Montar()));
        Assert.DoesNotContain("cerveja-pilsen", Ids(Montar(prefs: Gosto("Vinho"))));
        Assert.Contains("cerveja-pilsen", Ids(Montar(prefs: Gosto("Cerveja"))));
        // O "evitar" vence o "gosto".
        Assert.DoesNotContain("cerveja-pilsen", Ids(Montar(prefs: [Gosto("Cerveja"), Evitar("álcool")])));
    }

    // O perfil B do g8c recebeu patinho bovino e lombo suíno com "Carne vermelha" no "evitar".
    [Fact]
    public void Evitar_Carne_Vermelha_Tira_Bovinos_E_Suinos_Da_Lista()
    {
        var lista = Montar(prefs: Evitar("Carne vermelha"));

        Assert.DoesNotContain(lista.Itens, i => i.Item.Categoria is CategoriaAlimento.CarneBovina or CategoriaAlimento.CarneSuina);
        Assert.Contains(lista.Itens, i => i.Item.Categoria == CategoriaAlimento.Aves);
    }

    [Theory]
    [InlineData(Alergenos.Lactose)]
    [InlineData(Alergenos.Gluten)]
    [InlineData(Alergenos.Amendoim)]
    [InlineData(Alergenos.Peixe)]
    [InlineData(Alergenos.Ovo)]
    [InlineData(Alergenos.Lactose | Alergenos.Gluten)]
    public void Alergenos_Saem_Da_Lista(Alergenos alergias)
    {
        var lista = Montar(alergias: alergias);

        Assert.DoesNotContain(lista.Itens, i => (i.Item.Alergenos & alergias) != Alergenos.Nenhum);
        Assert.NotEmpty(lista.Itens);
    }

    [Fact]
    public void Preferencias_Dizem_Se_Casaram()
    {
        var lista = Montar(prefs: [Gosto("Whey protein"), Gosto("Banana"), Evitar("cordeiro"), Evitar("Peixe")]);
        var porNome = lista.Preferencias.ToDictionary(p => p.Alimento);

        Assert.False(porNome["Whey protein"].Casou);
        Assert.Equal(["whey protein"], porNome["Whey protein"].TrechosSemCasamento);
        Assert.Equal(2, porNome["Banana"].ItensCasados);
        Assert.False(porNome["cordeiro"].Casou);
        Assert.Equal(6, porNome["Peixe"].ItensCasados);
    }

    // ---------- Mínimos por papel ----------

    // Vegano: sobram só as 4 leguminosas como proteína, de um grupo só.
    [Fact]
    public void Vegano_Fica_Abaixo_Do_Minimo_De_Proteina_Sem_Ficar_Vazio()
    {
        var minimos = Montar(prefs: Evitar("vegano")).Minimos;

        Assert.Equal(4, minimos.ItensPorPapel[PapelNoPrato.Proteina]);
        Assert.Equal(1, minimos.GruposDeProteina);
        Assert.Equal([PapelNoPrato.Proteina], minimos.AbaixoDoMinimo);
        Assert.Empty(minimos.Vazios);
        Assert.False(minimos.Atende);
    }

    [Fact]
    public void Varios_Evitar_Deixam_A_Proteina_Vazia()
    {
        var minimos = Montar(prefs: [Evitar("carne"), Evitar("ovo"), Evitar("leite"), Evitar("feijão"), Evitar("lentilha")]).Minimos;

        Assert.Equal([PapelNoPrato.Proteina], minimos.Vazios);
        Assert.False(minimos.Atende);
    }

    // Mensagem do 400 do DietasController (papel vazio): diz o que faltou e onde corrigir.
    [Fact]
    public void Mensagem_Do_400_Diz_O_Que_Faltou()
    {
        Assert.Equal(
            "Com o que você marcou para evitar, não sobrou nenhuma fonte de proteína entre os alimentos do Ronu. Revise a lista de Evitar em Alimentação.",
            MinimosPorPapel.MensagemPapeisVazios([PapelNoPrato.Proteina]));
        Assert.Contains("nenhuma fonte de proteína e nenhuma fruta",
            MinimosPorPapel.MensagemPapeisVazios([PapelNoPrato.Proteina, PapelNoPrato.Fruta]));
    }

    [Fact]
    public void Frutas_Abaixo_Do_Minimo()
    {
        var evitar = BaseNutricional.Itens.Where(i => i.Grupo == GrupoDeTroca.Frutas).Skip(2).Select(i => Evitar(i.Nome)).ToArray();

        var minimos = Montar(prefs: evitar).Minimos;

        Assert.Equal(2, minimos.ItensPorPapel[PapelNoPrato.Fruta]);
        Assert.Contains(PapelNoPrato.Fruta, minimos.AbaixoDoMinimo);
    }
}
