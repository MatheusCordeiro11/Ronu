using System.Text.Json;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Fase 3: kcal e macros pela base (quantidade ÷ 100 × valor por 100 g), gravados
/// sem arredondar, com o total do dia exatamente igual à soma das refeições.
/// </summary>
public class CalculoPelaBaseTests
{
    private static AlimentoDto Item(string id, decimal quantidade, string unidade = "g") =>
        new() { Nome = id, AlimentoId = id, Quantidade = quantidade, Unidade = unidade };

    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    private static List<DiaDietaDto> Dia(params List<AlimentoDto>[] refeicoes) =>
    [
        new()
        {
            DiaSemana = "Segunda-feira",
            Refeicoes = refeicoes.Select((a, i) => new RefeicaoDto { Nome = $"R{i}", Alimentos = a, Macros = Zero() }).ToList(),
            TotalDoDia = Zero(),
            MetaCalculada = Zero()
        }
    ];

    // Arroz branco cozido, TACO 3: 128 kcal, 2,5 g P, 28,1 g C, 0,2 g G por 100 g. 150 g = 192 kcal.
    [Fact]
    public void Item_Em_Gramas_E_Quantidade_Vezes_Valor_Por_100g()
    {
        var dias = Dia([Item("arroz-branco-cozido", 150)]);

        CalculoPelaBase.Calcular(dias);

        var m = dias[0].Refeicoes[0].Alimentos[0].Macros!;
        Assert.Equal((192m, 3.75m, 42.15m, 0.3m), (m.Calorias, m.ProteinasG, m.CarboidratosG, m.GordurasG));
    }

    // Leite desnatado (USDA): 34 kcal, 3,37 g P, 4,96 g C, 0,08 g G por 100; para bebidas, 100 g vale como 100 ml.
    [Fact]
    public void Bebida_Em_Ml_Usa_O_Valor_Por_100g_Como_100ml()
    {
        var dias = Dia([Item("leite-desnatado", 200, "ml")]);

        CalculoPelaBase.Calcular(dias);

        var m = dias[0].Refeicoes[0].Alimentos[0].Macros!;
        Assert.Equal((68m, 6.74m, 9.92m, 0.16m), (m.Calorias, m.ProteinasG, m.CarboidratosG, m.GordurasG));
    }

    // Sem arredondar: 33 g de arroz = 42,24 kcal, gravado exato (o dashboard arredonda na exibição).
    [Fact]
    public void Grava_Sem_Arredondar()
    {
        var dias = Dia([Item("arroz-branco-cozido", 33)]);

        CalculoPelaBase.Calcular(dias);

        Assert.Equal(42.24m, dias[0].Refeicoes[0].Alimentos[0].Macros!.Calorias);
        Assert.Equal(0.066m, dias[0].Refeicoes[0].Macros.GordurasG);
    }

    // Com quantidades quebradas em várias refeições, a refeição é exatamente a soma dos itens e o dia,
    // exatamente a soma das refeições (nada arredondado no caminho).
    [Fact]
    public void Total_Do_Dia_Bate_Com_A_Soma_Das_Refeicoes()
    {
        var dias = Dia(
            [Item("arroz-branco-cozido", 137), Item("feijao-cozido", 83), Item("azeite-de-oliva-extra-virgem", 7)],
            [Item("leite-desnatado", 215, "ml"), Item("aveia-em-flocos", 31), Item("banana-prata", 75)],
            [Item("peito-de-frango-sem-pele-grelhado", 143), Item("batata-doce-cozida", 211), Item("brocolis-cozido", 67)]);

        CalculoPelaBase.Calcular(dias);

        var dia = dias[0];
        foreach (var refeicao in dia.Refeicoes)
        {
            Assert.Equal(refeicao.Alimentos.Sum(a => a.Macros!.Calorias), refeicao.Macros.Calorias);
            Assert.Equal(refeicao.Alimentos.Sum(a => a.Macros!.GordurasG), refeicao.Macros.GordurasG);
        }
        Assert.Equal(dia.Refeicoes.Sum(r => r.Macros.Calorias), dia.TotalDoDia.Calorias);
        Assert.Equal(dia.Refeicoes.Sum(r => r.Macros.ProteinasG), dia.TotalDoDia.ProteinasG);
        Assert.Equal(dia.Refeicoes.Sum(r => r.Macros.CarboidratosG), dia.TotalDoDia.CarboidratosG);
        Assert.Equal(dia.Refeicoes.Sum(r => r.Macros.GordurasG), dia.TotalDoDia.GordurasG);
        // E não é um valor redondo: prova que nada foi arredondado para bater.
        Assert.NotEqual(Math.Round(dia.TotalDoDia.Calorias), dia.TotalDoDia.Calorias);
    }

    // Numa dieta nova, item sem AlimentoId (ou com id fora da base) torna a resposta inválida, nunca 0 kcal.
    [Theory]
    [InlineData(null)]
    [InlineData("whey-protein")]
    public void Item_Sem_Id_Na_Base_Torna_A_Resposta_Invalida(string? id)
    {
        var dias = Dia([Item("arroz-branco-cozido", 100), new AlimentoDto { Nome = "Whey protein", AlimentoId = id, Quantidade = 30, Unidade = "g" }]);

        var ex = Assert.Throws<RespostaIaInvalidaException>(() => CalculoPelaBase.Calcular(dias));

        Assert.Contains("Whey protein", ex.Message);
    }

    // Dietas antigas não passam pelo cálculo: ao abrir, ficam com os números que o Gemini declarou na época.
    [Fact]
    public void Dieta_Antiga_Abre_Com_O_Declarado_E_Sem_Macros_Por_Item()
    {
        const string antiga = """
            {"Dias":[{"DiaSemana":"Segunda-feira","Refeicoes":[{"Nome":"Almoço","Alimentos":[{"Nome":"Feijão","Quantidade":150,"Unidade":"g"}],
            "Macros":{"Calorias":500,"ProteinasG":30,"CarboidratosG":60,"GordurasG":15}}],
            "TotalDoDia":{"Calorias":500,"ProteinasG":30,"CarboidratosG":60,"GordurasG":15},
            "MetaCalculada":{"Calorias":2000,"ProteinasG":140,"CarboidratosG":250,"GordurasG":60}}]}
            """;

        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(antiga)!;

        Assert.Equal(500m, dieta.Dias[0].TotalDoDia.Calorias);
        Assert.Equal(500m, dieta.Dias[0].Refeicoes[0].Macros.Calorias);
        Assert.Null(dieta.Dias[0].Refeicoes[0].Alimentos[0].Macros);
    }
}
