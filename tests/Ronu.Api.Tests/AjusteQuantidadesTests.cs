using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Fase 4: ajuste de quantidades por dia, com um fator por grupo de nutriente
/// principal, dentro dos limites (50% a 150%, porção máxima, contáveis em
/// unidade inteira, nada zerado) e com gramas legíveis no fim.
/// </summary>
public class AjusteQuantidadesTests
{
    private static MacrosDto Macros(decimal kcal, decimal p, decimal c, decimal g) =>
        new() { Calorias = kcal, ProteinasG = p, CarboidratosG = c, GordurasG = g };

    private static List<DiaDietaDto> Dia(MacrosDto meta, params (string Id, decimal Quantidade)[] itens) =>
    [
        new()
        {
            DiaSemana = "Segunda-feira",
            Refeicoes =
            [
                new RefeicaoDto
                {
                    Nome = "Almoço",
                    Alimentos = itens.Select(i => new AlimentoDto { Nome = i.Id, AlimentoId = i.Id, Quantidade = i.Quantidade, Unidade = "g" }).ToList(),
                    Macros = Macros(0, 0, 0, 0)
                }
            ],
            TotalDoDia = Macros(0, 0, 0, 0),
            MetaCalculada = meta
        }
    ];

    private static decimal Q(List<DiaDietaDto> dias, string id) => dias[0].Refeicoes[0].Alimentos.Single(a => a.AlimentoId == id).Quantidade;

    // A meta é exatamente o cardápio com menos frango (×0,7) e mais arroz (×1,3): o ajuste tem que chegar
    // perto dela tirando proteína e pondo carboidrato, e não mexendo em tudo junto.
    [Fact]
    public void Proteina_Acima_E_Carboidrato_Abaixo_Mexem_Em_Grupos_Diferentes()
    {
        (string, decimal)[] original = [("peito-de-frango-sem-pele-grelhado", 200), ("arroz-branco-cozido", 200), ("feijao-cozido", 100), ("azeite-de-oliva-extra-virgem", 10), ("alface-crua", 50)];
        var alvo = Dia(Macros(0, 0, 0, 0), ("peito-de-frango-sem-pele-grelhado", 140), ("arroz-branco-cozido", 260), ("feijao-cozido", 70), ("azeite-de-oliva-extra-virgem", 10), ("alface-crua", 50));
        CalculoPelaBase.Calcular(alvo);
        var dias = Dia(alvo[0].TotalDoDia, original);

        var ajuste = Assert.Single(AjusteQuantidades.Ajustar(dias));
        CalculoPelaBase.Calcular(dias);

        Assert.True(Q(dias, "peito-de-frango-sem-pele-grelhado") < 200);
        Assert.True(Q(dias, "arroz-branco-cozido") > 200);
        Assert.Equal(50m, Q(dias, "alface-crua"));   // hortaliça (sem nutriente principal) não muda
        Assert.True(ajuste.DentroDaMargem);
        Assert.InRange(dias[0].TotalDoDia.ProteinasG / alvo[0].TotalDoDia.ProteinasG, 0.9m, 1.1m);
        Assert.InRange(dias[0].TotalDoDia.CarboidratosG / alvo[0].TotalDoDia.CarboidratosG, 0.9m, 1.1m);
    }

    // Meta impossível (muito abaixo): fica a melhor solução dentro dos limites, sem erro e sem zerar nada.
    [Fact]
    public void Meta_Impossivel_Fica_No_Piso_Sem_Zerar_E_Sem_Erro()
    {
        var dias = Dia(Macros(300, 20, 30, 10), ("peito-de-frango-sem-pele-grelhado", 200), ("arroz-branco-cozido", 300), ("azeite-de-oliva-extra-virgem", 10));

        var ajuste = Assert.Single(AjusteQuantidades.Ajustar(dias));

        Assert.False(ajuste.DentroDaMargem);
        Assert.Equal(100m, Q(dias, "peito-de-frango-sem-pele-grelhado"));
        Assert.Equal(150m, Q(dias, "arroz-branco-cozido"));
        Assert.Equal(5m, Q(dias, "azeite-de-oliva-extra-virgem"));
    }

    // Meta muito acima: nada passa de 150% nem da porção máxima (frango: 300 g; arroz: 400 g).
    [Fact]
    public void Nada_Passa_De_150_Por_Cento_Nem_Da_Porcao_Maxima()
    {
        var dias = Dia(Macros(6000, 300, 700, 150), ("peito-de-frango-sem-pele-grelhado", 250), ("arroz-branco-cozido", 200), ("azeite-de-oliva-extra-virgem", 10));

        AjusteQuantidades.Ajustar(dias);

        Assert.Equal(300m, Q(dias, "peito-de-frango-sem-pele-grelhado"));   // 150% seria 375 g; a porção máxima é 300 g
        Assert.Equal(300m, Q(dias, "arroz-branco-cozido"));                 // 150% de 200 g
        Assert.Equal(15m, Q(dias, "azeite-de-oliva-extra-virgem"));         // 150% de 10 g
    }

    // Contáveis em unidade inteira (ovo: 45 g; tomate: 100 g), mesmo o tomate, que não tem nutriente principal.
    [Fact]
    public void Contaveis_Ficam_Em_Unidade_Inteira()
    {
        var dias = Dia(Macros(900, 50, 100, 30), ("ovo-de-galinha-cozido", 100), ("arroz-branco-cozido", 250), ("tomate-cru", 80));

        AjusteQuantidades.Ajustar(dias);

        Assert.Equal(0m, Q(dias, "ovo-de-galinha-cozido") % 45m);
        Assert.True(Q(dias, "ovo-de-galinha-cozido") >= 45m);
        Assert.Equal(100m, Q(dias, "tomate-cru"));
    }

    // No fim, gramas legíveis: múltiplos de 5 g a partir de 50 g e de 1 g abaixo disso.
    [Fact]
    public void Gramas_Ficam_Legiveis()
    {
        var dias = Dia(Macros(1777, 113, 201, 53), ("peito-de-frango-sem-pele-grelhado", 173), ("arroz-branco-cozido", 233), ("feijao-cozido", 117), ("azeite-de-oliva-extra-virgem", 13));

        AjusteQuantidades.Ajustar(dias);

        foreach (var a in dias[0].Refeicoes[0].Alimentos)
        {
            Assert.Equal(0m, a.Quantidade % (a.Quantidade >= 50 ? 5m : 1m));
        }
    }

    [Theory]
    [InlineData(73.4, 75)]
    [InlineData(72.4, 70)]
    [InlineData(47.6, 48)]
    [InlineData(3.2, 3)]
    public void Legivel_Arredonda_Para_5g_Ou_1g(decimal quantidade, decimal esperado) =>
        Assert.Equal(esperado, AjusteQuantidades.Legivel(quantidade, 0, 1000));

    // O múltiplo legível nunca sai dos limites; se nenhum cabe, fica a quantidade exata.
    [Fact]
    public void Legivel_Respeita_Os_Limites() => Assert.Equal(52.4m, AjusteQuantidades.Legivel(52.4m, 52m, 53m));

    // Azeite, óleo de soja e manteiga passaram a ajustáveis na fase 4 (fecham a gordura do dia).
    [Theory]
    [InlineData("azeite-de-oliva-extra-virgem")]
    [InlineData("oleo-de-soja")]
    [InlineData("manteiga")]
    public void Oleos_Sao_Ajustaveis(string id) => Assert.True(BaseNutricional.Itens.Single(i => i.Id == id).Ajustavel);
}
