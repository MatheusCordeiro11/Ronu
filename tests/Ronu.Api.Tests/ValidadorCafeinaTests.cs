using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Regra 7: bebida com cafeína só de manhã e no início da tarde. À noite = jantar, ceia ou a partir das 18:00.
/// </summary>
public class ValidadorCafeinaTests
{
    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    private static RefeicaoDto Refeicao(string nome, string? horario, params (string Id, string Nome)[] itens) => new()
    {
        Nome = nome,
        Horario = horario,
        Macros = Zero(),
        Alimentos = itens.Select(i => new AlimentoDto { Nome = i.Nome, Quantidade = 200, Unidade = "ml", AlimentoId = i.Id }).ToList()
    };

    private static List<DiaDietaDto> Dias(params RefeicaoDto[] refeicoes) => new()
    {
        new() { DiaSemana = "Sexta-feira", Refeicoes = refeicoes.ToList(), TotalDoDia = Zero(), MetaCalculada = Zero() }
    };

    [Theory]
    [InlineData("Jantar", "19:00", true)]
    [InlineData("Ceia", "21:30", true)]
    [InlineData("Lanche da tarde", "18:00", true)]
    [InlineData("Lanche pós-treino", "21:45", true)]
    [InlineData("Lanche da tarde", "17:59", false)]
    [InlineData("Café da manhã", "07:00", false)]
    [InlineData("Lanche", null, false)]
    public void Noite_E_Jantar_Ceia_Ou_A_Partir_Das_18(string nome, string? horario, bool noite) =>
        Assert.Equal(noite, ValidadorCafeina.ENoite(Refeicao(nome, horario)));

    [Fact]
    public void Encontra_So_Cafeina_A_Noite()
    {
        var dias = Dias(
            Refeicao("Café da manhã", "07:00", ("cafe-coado", "Café coado")),
            Refeicao("Ceia", "21:30", ("cha-mate", "Chá mate"), ("cha-de-erva-doce", "Chá de erva-doce")),
            Refeicao("Jantar", "20:00", ("refrigerante-de-cola", "Refrigerante de cola")));

        Assert.Equal(
            new[] { "Sexta-feira, Ceia 21:30: Chá mate", "Sexta-feira, Jantar 20:00: Refrigerante de cola" },
            ValidadorCafeina.Encontrar(dias));
    }

    // A lista final (BraCaffT e USDA): o mate fica pelo chimarrão e pelo tereré; o guaraná, com 1 mg/100 ml, sai.
    [Fact]
    public void Lista_Tem_Cafe_Chas_E_Cola_Mas_Nao_O_Guarana()
    {
        Assert.Equal(new[] { "cafe-coado", "cha-mate", "cha-preto", "refrigerante-de-cola" }, ValidadorCafeina.ComCafeina.Order());
        Assert.Empty(ValidadorCafeina.Encontrar(Dias(Refeicao("Jantar", "20:00", ("refrigerante-de-guarana", "Refrigerante de guaraná")))));
    }

    [Fact]
    public void Corrigir_Troca_Cha_Pelo_Sem_Cafeina_Tira_Refrigerante_E_A_Refeicao_Vazia()
    {
        var lista = ListaAlimentosPessoa.Montar("SP", new List<PreferenciaContextoDto>(), Alergenos.Nenhum);
        var dias = Dias(
            Refeicao("Café da manhã", "07:00", ("cafe-coado", "Café coado")),
            Refeicao("Ceia", "21:30", ("cha-mate", "Chá mate")),
            Refeicao("Jantar", "20:00", ("refrigerante-de-cola", "Refrigerante de cola")));

        var mudancas = ValidadorCafeina.Corrigir(dias, lista);

        Assert.Equal(2, mudancas.Count);
        var refeicoes = dias[0].Refeicoes;
        Assert.Equal(new[] { "Café da manhã", "Ceia" }, refeicoes.Select(r => r.Nome));
        Assert.Equal("cafe-coado", refeicoes[0].Alimentos.Single().AlimentoId);
        var cha = refeicoes[1].Alimentos.Single();
        Assert.Equal((ValidadorCafeina.SemCafeina, 200m), (cha.AlimentoId, cha.Quantidade));
        Assert.Empty(ValidadorCafeina.Encontrar(dias));
    }

    [Fact]
    public void Sem_O_Cha_Sem_Cafeina_Na_Lista_O_Item_Sai()
    {
        var lista = ListaAlimentosPessoa.Montar("SP", new List<PreferenciaContextoDto>(), Alergenos.Nenhum);
        var semErvaDoce = lista with { Itens = lista.Itens.Where(i => i.Item.Id != ValidadorCafeina.SemCafeina).ToList() };
        var dias = Dias(Refeicao("Jantar", "20:00", ("cafe-coado", "Café coado"), ("arroz-branco-cozido", "Arroz branco cozido")));

        ValidadorCafeina.Corrigir(dias, semErvaDoce);

        Assert.Equal("arroz-branco-cozido", dias[0].Refeicoes.Single().Alimentos.Single().AlimentoId);
    }
}
