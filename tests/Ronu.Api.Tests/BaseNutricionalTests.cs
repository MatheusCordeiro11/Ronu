using System.Globalization;
using System.Text;
using Ronu.Api.Data;

namespace Ronu.Api.Tests;

/// <summary>
/// Integridade da base nutricional própria (BaseNutricional): teto da lista,
/// nomes únicos, campos preenchidos, kcal coerentes com os macros (pega erro
/// de transcrição) e as regras das marcas de alérgenos, que são conservadoras.
/// </summary>
public class BaseNutricionalTests
{
    private static readonly IReadOnlyList<AlimentoBase> Itens = BaseNutricional.Itens;

    private static readonly HashSet<string> Ufs =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

    // ---------- Tamanho e nomes ----------

    [Fact]
    public void Nomes_E_Apelidos_Cabem_No_Teto_Do_Enum()
    {
        var nomes = Itens.Count + Itens.Sum(i => i.Apelidos.Count);

        Assert.InRange(nomes, 100, BaseNutricional.TetoDeNomes);
        Assert.Equal(150, BaseNutricional.TetoDeNomes);
    }

    [Fact]
    public void Ids_Sao_Unicos()
    {
        var repetidos = Itens.GroupBy(i => i.Id).Where(g => g.Count() > 1).Select(g => g.Key);

        Assert.Empty(repetidos);
    }

    // Nome e apelidos juntos, sem diferenciar maiúsculas nem acentos: o C#
    // vai casar o nome devolvido pelo Gemini com um único item.
    [Fact]
    public void Nomes_E_Apelidos_Sao_Unicos_Sem_Acento_E_Sem_Caixa()
    {
        var todos = Itens.SelectMany(i => i.Apelidos.Select(a => a.Nome).Prepend(i.Nome)).Select(Chave);
        var repetidos = todos.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key);

        Assert.Empty(repetidos);
    }

    [Fact]
    public void Apelidos_Usam_Ufs_Validas_Sem_Repetir_Uf_No_Mesmo_Item()
    {
        foreach (var item in Itens.Where(i => i.Apelidos.Count > 0))
        {
            var ufs = item.Apelidos.SelectMany(a => a.Ufs).ToList();

            Assert.All(ufs, uf => Assert.Contains(uf, Ufs));
            Assert.Equal(ufs.Count, ufs.Distinct().Count());
        }
    }

    // ---------- Campos ----------

    [Fact]
    public void Todo_Campo_Obrigatorio_Esta_Preenchido()
    {
        Assert.All(Itens, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Id));
            Assert.False(string.IsNullOrWhiteSpace(item.Nome));
            Assert.False(string.IsNullOrWhiteSpace(item.CodigoFonte));
            Assert.False(string.IsNullOrWhiteSpace(item.NomeNaFonte));
            Assert.Contains(item.Medida, new[] { "g", "ml" });
            Assert.True(item.Kcal > 0, item.Nome);
            Assert.True(item.ProteinaG >= 0 && item.CarboidratoG >= 0 && item.GorduraG >= 0, item.Nome);
            Assert.True(item.ProteinaG + item.CarboidratoG + item.GorduraG <= 100, item.Nome);
            Assert.True(item.PorcaoMaxima > 0, item.Nome);
            Assert.True(Enum.IsDefined(item.Categoria), item.Nome);
        });
    }

    [Fact]
    public void Codigo_Da_Fonte_E_Numerico_E_Taco_Fica_Entre_1_E_597()
    {
        Assert.All(Itens, item =>
        {
            Assert.True(int.TryParse(item.CodigoFonte, out var codigo), item.Nome);
            if (item.Fonte is FonteNutricional.Taco or FonteNutricional.Derivado)
            {
                Assert.InRange(codigo, 1, 597);
            }
        });
    }

    [Fact]
    public void Item_Derivado_Explica_De_Onde_Vem()
    {
        Assert.All(Itens.Where(i => i.Fonte == FonteNutricional.Derivado), item =>
            Assert.Contains("DERIVADO", item.Observacao ?? string.Empty));
    }

    [Fact]
    public void Peso_Por_Unidade_Tem_Origem_E_O_Item_So_Muda_De_Unidade_Em_Unidade()
    {
        Assert.All(Itens.Where(i => i.PesoUnidade is not null), item =>
        {
            Assert.True(item.PesoUnidade!.Gramas > 0, item.Nome);
            Assert.False(string.IsNullOrWhiteSpace(item.PesoUnidade.Origem), item.Nome);
            Assert.Equal("g", item.Medida);
            Assert.False(item.Ajustavel, $"{item.Nome}: contável não muda de peso sem mudar a contagem");
            Assert.True(item.PorcaoMaxima >= item.PesoUnidade.Gramas, item.Nome);
        });
    }

    [Fact]
    public void Bebidas_Em_Ml_E_So_Elas()
    {
        Assert.All(Itens, item =>
        {
            var bebida = item.Categoria is CategoriaAlimento.Bebidas or CategoriaAlimento.BebidasAlcoolicas
                || item.Nome.StartsWith("Leite ", StringComparison.Ordinal);
            Assert.Equal(bebida ? "ml" : "g", item.Medida);
        });
    }

    // ---------- Valores ----------

    // As kcal da tabela usam fatores próprios e o carboidrato inclui as fibras,
    // então 4P+4C+9G não bate exato (frutas e hortaliças ficam ~10–20% acima).
    // A tolerância pega erro de transcrição (vírgula no lugar errado, coluna
    // trocada) sem acusar essas diferenças. Bebida alcoólica fica de fora: as
    // kcal do álcool não estão em P, C nem G.
    [Fact]
    public void Kcal_Proximas_De_4P_Mais_4C_Mais_9G()
    {
        Assert.All(Itens.Where(i => i.Categoria != CategoriaAlimento.BebidasAlcoolicas), item =>
        {
            var calculado = 4 * item.ProteinaG + 4 * item.CarboidratoG + 9 * item.GorduraG;
            var diferenca = Math.Abs(calculado - item.Kcal);

            Assert.True(diferenca <= Math.Max(item.Kcal * 0.15m, 8m),
                $"{item.Nome}: {item.Kcal} kcal na tabela, {calculado} por 4P+4C+9G");
        });
    }

    [Fact]
    public void Bebida_Alcoolica_Tem_Kcal_Acima_Dos_Macros()
    {
        Assert.All(Itens.Where(i => i.Categoria == CategoriaAlimento.BebidasAlcoolicas), item =>
            Assert.True(item.Kcal > 4 * item.ProteinaG + 4 * item.CarboidratoG + 9 * item.GorduraG, item.Nome));
    }

    // ---------- Alérgenos (marcação conservadora) ----------

    [Fact]
    public void Laticinios_Tem_Lactose()
    {
        AssertMarcados(Itens.Where(i => i.Categoria == CategoriaAlimento.Laticinios), Alergenos.Lactose);
    }

    [Fact]
    public void Itens_Com_Leite_Queijo_Iogurte_Requeijao_Ou_Manteiga_No_Nome_Tem_Lactose()
    {
        AssertMarcados(ComNome("leite", "lactea", "lacteo", "queijo", "iogurte", "requeijao", "manteiga", "ricota", "achocolatado"), Alergenos.Lactose);
    }

    [Fact]
    public void Itens_Com_Trigo_Aveia_Cevada_Ou_Centeio_Tem_Gluten()
    {
        AssertMarcados(ComNome("pao", "macarrao", "aveia", "trigo", "cevada", "centeio", "cerveja", "cuscuz", "farinha lactea", "biscoito"), Alergenos.Gluten);
    }

    // Nenhuma bebida da lista leva leite; se uma entrar (café com leite,
    // vitamina, bebida láctea), precisa da marca de lactose.
    [Fact]
    public void Bebida_Com_Leite_No_Nome_Tem_Lactose()
    {
        var bebidas = Itens.Where(i => i.Categoria is CategoriaAlimento.Bebidas or CategoriaAlimento.BebidasAlcoolicas);

        AssertMarcados(bebidas.Where(b => Chave(b.Nome).Contains("leite") || Chave(b.Nome).Contains("lact")), Alergenos.Lactose);
    }

    [Fact]
    public void Peixes_Tem_A_Marca_De_Peixe()
    {
        AssertMarcados(Itens.Where(i => i.Categoria == CategoriaAlimento.Peixes), Alergenos.Peixe);
        AssertMarcados(ComNome("tilapia", "salmao", "sardinha", "atum", "merluza", "corvina", "pescada", "bacalhau"), Alergenos.Peixe);
    }

    [Fact]
    public void Crustaceos_Tem_A_Marca_De_Crustaceo()
    {
        AssertMarcados(ComNome("camarao", "caranguejo", "lagosta", "siri", "marisco"), Alergenos.Crustaceo);
    }

    [Fact]
    public void Ovos_E_Itens_Com_Ovo_No_Nome_Tem_A_Marca_De_Ovo()
    {
        AssertMarcados(Itens.Where(i => i.Categoria == CategoriaAlimento.Ovos), Alergenos.Ovo);
        AssertMarcados(ComNome("ovo", "clara", "gema"), Alergenos.Ovo);
    }

    // Castanhas e nozes passam pelo mesmo beneficiamento do amendoim
    // ("pode conter amendoim"): na dúvida, marca.
    [Fact]
    public void Amendoim_E_Oleaginosas_Tem_A_Marca_De_Amendoim()
    {
        AssertMarcados(ComNome("amendoim"), Alergenos.Amendoim);
        AssertMarcados(ComNome("castanha", "noz", "amendoa", "avela", "pistache"), Alergenos.Amendoim);
    }

    // Composição incerta (varia muito entre marcas e receitas) fica fora da
    // lista, e preparações de vários ingredientes viram itens separados.
    [Theory]
    [InlineData("granola")]
    [InlineData("bolo")]
    [InlineData("molho")]
    [InlineData("whey")]
    [InlineData("presunto")]
    [InlineData("peito de peru")]
    [InlineData("salada")]
    [InlineData("vitamina")]
    [InlineData("omelete")]
    public void Itens_De_Composicao_Incerta_Ou_Compostos_Ficam_Fora(string termo)
    {
        Assert.Empty(ComNome(termo));
    }

    // ---------- Apoio ----------

    private static void AssertMarcados(IEnumerable<AlimentoBase> itens, Alergenos marca)
    {
        var sem = itens.Where(i => !i.Alergenos.HasFlag(marca)).Select(i => i.Nome);
        Assert.Empty(sem);
    }

    // Itens cujo nome (ou apelido) contém algum dos termos, sem acento e sem caixa.
    private static IEnumerable<AlimentoBase> ComNome(params string[] termos) =>
        Itens.Where(i => i.Apelidos.Select(a => a.Nome).Prepend(i.Nome)
            .Any(n => termos.Any(t => Chave(n).Contains(Chave(t)))));

    private static string Chave(string texto)
    {
        var decomposto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var semAcento = decomposto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string(semAcento.ToArray()).Normalize(NormalizationForm.FormC);
    }
}
