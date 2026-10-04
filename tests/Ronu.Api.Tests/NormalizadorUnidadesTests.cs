using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// NormalizadorUnidades: sinônimos viram "g"/"ml", kg e litro são convertidos,
/// e unidades fora do permitido ficam como vieram (só contadas para o log).
/// </summary>
public class NormalizadorUnidadesTests
{
    [Theory]
    [InlineData("gramas", "g")]
    [InlineData("Gramas", "g")]
    [InlineData(" grama ", "g")]
    [InlineData("gr", "g")]
    [InlineData("G", "g")]
    [InlineData("mililitros", "ml")]
    [InlineData("mL", "ml")]
    [InlineData("ML", "ml")]
    public void Sinonimo_Vira_A_Forma_Curta_Sem_Mudar_A_Quantidade(string escrita, string esperada)
    {
        var alimento = Alimento(150, escrita);

        var resultado = NormalizadorUnidades.Normalizar(new[] { alimento });

        Assert.Equal(esperada, alimento.Unidade);
        Assert.Equal(150m, alimento.Quantidade);
        Assert.Equal(1, resultado.Corrigidos);
        Assert.Empty(resultado.ForaDoPadrao);
    }

    [Theory]
    [InlineData("kg", 0.2, "g", 200)]
    [InlineData("quilos", 1.5, "g", 1500)]
    [InlineData("litro", 0.25, "ml", 250)]
    [InlineData("L", 1, "ml", 1000)]
    public void Kg_E_Litro_Sao_Convertidos(string escrita, decimal quantidade, string esperada, decimal quantidadeEsperada)
    {
        var alimento = Alimento(quantidade, escrita);

        var resultado = NormalizadorUnidades.Normalizar(new[] { alimento });

        Assert.Equal(esperada, alimento.Unidade);
        Assert.Equal(quantidadeEsperada, alimento.Quantidade);
        Assert.Equal(1, resultado.Corrigidos);
    }

    [Theory]
    [InlineData("g")]
    [InlineData("ml")]
    public void Unidade_Ja_Padrao_Nao_Conta_Como_Correcao(string unidade)
    {
        var resultado = NormalizadorUnidades.Normalizar(new[] { Alimento(100, unidade) });

        Assert.Equal(0, resultado.Corrigidos);
        Assert.Empty(resultado.ForaDoPadrao);
    }

    [Fact]
    public void Unidade_Fora_Do_Permitido_Fica_Como_Veio_E_E_Contada()
    {
        var ovo = Alimento(2, "unidades");
        var pao = Alimento(1, "fatia");
        var banana = Alimento(1, "Unidades");

        var resultado = NormalizadorUnidades.Normalizar(new[] { ovo, pao, banana, Alimento(100, "gramas") });

        Assert.Equal("unidades", ovo.Unidade);
        Assert.Equal(2m, ovo.Quantidade);
        Assert.Equal(4, resultado.Total);
        Assert.Equal(1, resultado.Corrigidos);
        // Sem diferenciar maiúsculas: "unidades" e "Unidades" somam juntas.
        Assert.Equal(2, resultado.ForaDoPadrao["unidades"]);
        Assert.Equal(1, resultado.ForaDoPadrao["fatia"]);
    }

    private static AlimentoDto Alimento(decimal quantidade, string unidade) =>
        new() { Nome = "Alimento", Quantidade = quantidade, Unidade = unidade };
}
