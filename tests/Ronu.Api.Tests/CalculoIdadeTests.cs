namespace Ronu.Api.Tests;

/// <summary>
/// Testa isoladamente a fórmula de cálculo de idade usada em
/// ContextoDietaBuilder.ConstruirAsync. A fórmula é replicada aqui com "hoje"
/// parametrizado (na implementação real é DateTime.UtcNow) porque o método
/// original não recebe a data atual como parâmetro, o que tornaria o teste
/// não determinístico se dependesse do relógio do sistema.
/// </summary>
public class CalculoIdadeTests
{
    private static int CalcularIdade(DateOnly dataNascimento, DateOnly hoje)
    {
        var idade = hoje.Year - dataNascimento.Year;
        if (hoje < dataNascimento.AddYears(idade))
        {
            idade--;
        }
        return idade;
    }

    [Fact]
    public void Aniversario_Ja_Passou_Este_Ano()
    {
        var dataNascimento = new DateOnly(2000, 3, 10);
        var hoje = new DateOnly(2026, 9, 17);

        var idade = CalcularIdade(dataNascimento, hoje);

        Assert.Equal(26, idade);
    }

    [Fact]
    public void Aniversario_E_Hoje()
    {
        var dataNascimento = new DateOnly(2000, 9, 17);
        var hoje = new DateOnly(2026, 9, 17);

        var idade = CalcularIdade(dataNascimento, hoje);

        Assert.Equal(26, idade);
    }

    [Fact]
    public void Aniversario_Ainda_Nao_Chegou_Este_Ano()
    {
        var dataNascimento = new DateOnly(2000, 12, 25);
        var hoje = new DateOnly(2026, 9, 17);

        var idade = CalcularIdade(dataNascimento, hoje);

        Assert.Equal(25, idade);
    }
}
