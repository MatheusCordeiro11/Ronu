using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// ValidadorBebidaAlcoolica (regra 8 do prompt): bebida alcoólica só passa se a
/// mesma bebida estiver entre os alimentos preferidos da pessoa.
/// </summary>
public class ValidadorBebidaAlcoolicaTests
{
    [Theory]
    [InlineData("Cereais cerveja lata (Cerveja pilsen)")]
    [InlineData("Vinho tinto seco")]
    [InlineData("Caipirinha de cachaça")]
    [InlineData("CHOPE")]
    [InlineData("Gin tônica")]
    [InlineData("Vodka")]
    [InlineData("Espumante brut")]
    public void Bebida_Sem_Preferencia_E_Encontrada(string nome)
    {
        var encontrada = ValidadorBebidaAlcoolica.EncontrarNaoPermitida(Alimentos("Arroz branco cozido", nome), Array.Empty<string>());

        Assert.Equal(nome, encontrada);
    }

    [Fact]
    public void Bebida_Preferida_E_Permitida()
    {
        var encontrada = ValidadorBebidaAlcoolica.EncontrarNaoPermitida(
            Alimentos("Cereais cerveja lata (Cerveja pilsen)"), new[] { "Banana", "cerveja" });

        Assert.Null(encontrada);
    }

    [Fact]
    public void Preferir_Uma_Bebida_Nao_Libera_Outra()
    {
        var encontrada = ValidadorBebidaAlcoolica.EncontrarNaoPermitida(Alimentos("Vinho tinto"), new[] { "Cerveja" });

        Assert.Equal("Vinho tinto", encontrada);
    }

    // Palavra inteira: "gin", "rum" e "vinho" não casam dentro de outras palavras.
    [Theory]
    [InlineData("Gengibre cru")]
    [InlineData("Vinagrete")]
    [InlineData("Ameixa")]
    [InlineData("Café coado")]
    public void Alimento_Comum_Nao_E_Bebida(string nome)
    {
        Assert.Null(ValidadorBebidaAlcoolica.EncontrarNaoPermitida(Alimentos(nome), Array.Empty<string>()));
    }

    private static IEnumerable<AlimentoDto> Alimentos(params string[] nomes) =>
        nomes.Select(n => new AlimentoDto { Nome = n, Quantidade = 100, Unidade = "g" });
}
