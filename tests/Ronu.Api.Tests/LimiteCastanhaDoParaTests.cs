using System.Text.Json.Nodes;
using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Selênio: a castanha-do-pará fica em até 8 g por dia e em até 2 dias da semana no cardápio gerado
/// (LimiteCastanhaDoPara, com as fontes), e a porção máxima da troca é a mesma.
/// </summary>
public class LimiteCastanhaDoParaTests
{
    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    private static AlimentoBase Base(string id) => BaseNutricional.Itens.Single(i => i.Id == id);

    private static AlimentoDto A(string id, decimal quantidade) =>
        new() { Nome = Base(id).Nome, AlimentoId = id, Quantidade = quantidade, Unidade = Base(id).Medida };

    private static RefeicaoDto R(string nome, params AlimentoDto[] alimentos) => new() { Nome = nome, Alimentos = alimentos.ToList(), Macros = Zero() };

    private static DiaDietaDto Dia(string nome, params RefeicaoDto[] refeicoes) =>
        new() { DiaSemana = nome, Refeicoes = refeicoes.ToList(), TotalDoDia = Zero(), MetaCalculada = Zero() };

    private static decimal Castanha(DiaDietaDto dia) =>
        dia.Refeicoes.SelectMany(r => r.Alimentos).Where(a => a.AlimentoId == LimiteCastanhaDoPara.Id).Sum(a => a.Quantidade);

    [Fact]
    public void Corta_30g_Para_8g()
    {
        var dias = new List<DiaDietaDto> { Dia("Terça-feira", R("Lanche da manhã", A("castanha-do-para", 30), A("banana-prata", 75))) };

        var mudancas = LimiteCastanhaDoPara.Aplicar(dias);

        Assert.Equal(8m, Castanha(dias[0]));
        Assert.Equal(2, dias[0].Refeicoes[0].Alimentos.Count);
        Assert.Equal(new[] { "Terça-feira: 30 g -> 8 g (teto do dia)" }, mudancas);
    }

    [Fact]
    public void Ate_8g_Fica_Como_Esta_E_Nao_Vai_Para_O_Log()
    {
        var dias = new List<DiaDietaDto> { Dia("Terça-feira", R("Ceia", A("castanha-do-para", 8), A("banana-prata", 75))) };

        Assert.Empty(LimiteCastanhaDoPara.Aplicar(dias));
        Assert.Equal(8m, Castanha(dias[0]));
    }

    // O teto é do dia, não da refeição: duas refeições com 5 g ficam com 5 g e 3 g.
    [Fact]
    public void Teto_Vale_Para_A_Soma_Do_Dia()
    {
        var dias = new List<DiaDietaDto>
        {
            Dia("Quarta-feira",
                R("Lanche da manhã", A("castanha-do-para", 5), A("banana-prata", 75)),
                R("Ceia", A("castanha-do-para", 5), A("maca-fuji", 130)))
        };

        LimiteCastanhaDoPara.Aplicar(dias);

        Assert.Equal(5m, dias[0].Refeicoes[0].Alimentos[0].Quantidade);
        Assert.Equal(3m, dias[0].Refeicoes[1].Alimentos[0].Quantidade);
    }

    // Terceiro dia com castanha na semana: ela sai. A refeição com outros itens fica; a que só tinha a castanha sai
    // do dia (como na correção da cafeína). Dia sem castanha não conta.
    [Fact]
    public void A_Partir_Do_Terceiro_Dia_A_Castanha_Sai()
    {
        var dias = new List<DiaDietaDto>
        {
            Dia("Segunda-feira", R("Lanche da tarde", A("castanha-do-para", 15), A("banana-prata", 75))),
            Dia("Terça-feira", R("Almoço", A("arroz-branco-cozido", 150))),
            Dia("Quarta-feira", R("Ceia", A("castanha-do-para", 10))),
            Dia("Quinta-feira", R("Almoço", A("arroz-branco-cozido", 150)), R("Lanche da manhã", A("castanha-do-para", 20), A("banana-prata", 75))),
            Dia("Sexta-feira", R("Almoço", A("arroz-branco-cozido", 150)), R("Ceia", A("castanha-do-para", 15))),
        };

        var mudancas = LimiteCastanhaDoPara.Aplicar(dias);

        Assert.Equal(new[] { 8m, 0m, 8m, 0m, 0m }, dias.Select(Castanha));
        Assert.Equal(new[] { "Almoço", "Lanche da manhã" }, dias[3].Refeicoes.Select(r => r.Nome));
        Assert.Equal(new[] { "banana-prata" }, dias[3].Refeicoes[1].Alimentos.Select(a => a.AlimentoId));
        Assert.Equal(new[] { "Almoço" }, dias[4].Refeicoes.Select(r => r.Nome));
        Assert.Equal(new[]
        {
            "Segunda-feira: 15 g -> 8 g (teto do dia)",
            "Quarta-feira: 10 g -> 8 g (teto do dia)",
            "Quinta-feira: 20 g -> 0 g (passou de 2 dias na semana)",
            "Sexta-feira: 15 g -> 0 g (passou de 2 dias na semana; refeição sem outro item saiu: Ceia)",
        }, mudancas);
    }

    // A base: porção máxima (a da troca também) igual ao teto do dia, não ajustável e não contável, então o ajuste
    // de quantidades nunca a aumenta.
    [Fact]
    public void Base_Tem_Porcao_Maxima_De_8g_E_Nao_E_Ajustavel()
    {
        var castanha = Base(LimiteCastanhaDoPara.Id);

        Assert.Equal(LimiteCastanhaDoPara.GramasPorDia, castanha.PorcaoMaxima);
        Assert.Equal(LimiteCastanhaDoPara.GramasPorDia, castanha.PorcaoMaximaTroca);
        Assert.False(castanha.Ajustavel);
        Assert.Null(castanha.MedidaUnidade);
    }

    // Um dia com a gordura bem abaixo da meta: o ajuste sobe o azeite, mas a castanha fica nos 8 g.
    [Fact]
    public void Ajuste_De_Quantidades_Nao_Mexe_Na_Castanha()
    {
        var dia = Dia("Sábado",
            R("Almoço", A("arroz-branco-cozido", 200), A("peito-de-frango-sem-pele-grelhado", 150), A("azeite-de-oliva-extra-virgem", 5)),
            R("Lanche da tarde", A("castanha-do-para", 8), A("banana-prata", 75)));
        dia.MetaCalculada = new MacrosDto { Calorias = 1200, ProteinasG = 60, CarboidratosG = 120, GordurasG = 50 };

        AjusteQuantidades.Ajustar([dia]);

        Assert.Equal(8m, Castanha(dia));
    }

    // As respostas reais guardadas em docs/medicoes (g8b a g10d, sem alergia): depois do limite, nenhum dia passa de
    // 8 g e nenhuma semana tem castanha em mais de 2 dias. Na medição de 2026-11-13, ela aparecia em 50 de 104
    // dietas, com até 30 g por dia e em até 3 dias da semana.
    [Fact]
    public void Respostas_Reais_Das_Medicoes_Ficam_Dentro_Do_Limite()
    {
        var arquivos = Directory.GetFiles(Path.Combine(RaizDoRepositorio(), "docs", "medicoes"), "*.json", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("rodada") && Path.GetFileName(f) != "metricas.json")
            .ToList();
        int dietas = 0, comCastanha = 0, cortadas = 0;
        decimal maiorAntes = 0;
        var maisDiasAntes = 0;

        foreach (var arquivo in arquivos)
        {
            var diasJson = JsonNode.Parse(File.ReadAllText(arquivo))?["resposta"]?["dieta"]?["dias"]?.AsArray();
            if (diasJson is null)
            {
                continue;
            }

            var dias = diasJson.Select(d => Dia(d!["diaSemana"]!.GetValue<string>(),
                d["refeicoes"]!.AsArray().Select(r => R(r!["nome"]!.GetValue<string>(),
                    r["alimentos"]!.AsArray().Select(a => new AlimentoDto
                    {
                        Nome = a!["nome"]!.GetValue<string>(), AlimentoId = a["alimentoId"]?.GetValue<string>(),
                        Quantidade = a["quantidade"]!.GetValue<decimal>(), Unidade = a["unidade"]!.GetValue<string>()
                    }).ToArray())).ToArray())).ToList();
            dietas++;
            var antes = dias.Select(Castanha).ToList();
            if (antes.Any(g => g > 0)) comCastanha++;
            maiorAntes = Math.Max(maiorAntes, antes.Max());
            maisDiasAntes = Math.Max(maisDiasAntes, antes.Count(g => g > 0));

            if (LimiteCastanhaDoPara.Aplicar(dias).Count > 0) cortadas++;

            Assert.All(dias, d => Assert.True(Castanha(d) <= LimiteCastanhaDoPara.GramasPorDia));
            Assert.True(dias.Count(d => Castanha(d) > 0) <= LimiteCastanhaDoPara.DiasPorSemana);
            Assert.All(dias, d => Assert.DoesNotContain(d.Refeicoes, r => r.Alimentos.Count == 0));
        }

        // Confere que o teste leu as medições de verdade (e que elas tinham o caso a corrigir).
        Assert.True(dietas >= 100, $"só {dietas} dietas lidas");
        Assert.True(comCastanha >= 40);
        Assert.Equal(30m, maiorAntes);
        Assert.Equal(3, maisDiasAntes);
        Assert.True(cortadas >= 40);
    }

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "medicoes")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("docs/medicoes não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
