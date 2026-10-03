using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Piso de carboidrato e peso de referência (IMC 30) da CalculadoraMacros.
/// Antes, proteína (1,8 g/kg) e gordura (1,0 g/kg) usavam o peso total e o
/// carboidrato era só a sobra — negativo em dias de descanso com déficit para
/// IMC alto. As metas de entrada são as da fórmula (Mifflin-St Jeor, dia de
/// descanso, -15% de "perder peso"), recalculadas aqui de forma independente.
/// </summary>
public class PisoCarboidratoTests
{
    private static decimal MetaDescansoPerderPeso(string sexo, decimal peso, decimal altura, int idade, decimal ajuste = 0m)
    {
        var tmb = 10 * peso + 6.25m * altura - 5 * idade + (sexo == "M" ? 5 : -161);
        return tmb * 0.85m * (1 + ajuste);
    }

    // A divisão de antes (sem piso, peso total), para comparar onde nada deve mudar.
    private static (decimal P, decimal G, decimal C) DivisaoAntiga(decimal meta, decimal peso) =>
        (1.8m * peso, 1.0m * peso, (meta - 1.8m * peso * 4 - 1.0m * peso * 9) / 4);

    // Casos da tabela que davam carboidrato negativo: mulher 90 kg, 160 cm, 35 anos
    // (-32 g sem ajuste, -49 g com -5%). IMC 35,2 -> peso de referência 30·1,6² = 76,8 kg.
    [Theory]
    [InlineData(0)]
    [InlineData(-0.05)]
    public void Mulher_90kg_Antes_Negativa_Agora_Fica_No_Piso_E_A_Meta_Sobe(double ajuste)
    {
        var meta = MetaDescansoPerderPeso("F", 90, 160, 35, (decimal)ajuste);
        Assert.True(DivisaoAntiga(meta, 90).C < 0); // confirma o bug de antes (-32 / -49 g)

        var r = CalculadoraMacros.Calcular(meta, 90, 160);

        Assert.Equal(100m, r.Macros.CarboidratosG);
        Assert.Equal(138.24m, r.Macros.ProteinasG);   // 1,8 · 76,8
        Assert.Equal(46.08m, r.Macros.GordurasG);     // 0,6 · 76,8 (mínimo)
        Assert.Equal(1367.68m, r.Macros.Calorias);    // 552,96 + 400 + 414,72
        Assert.True(r.MetaElevadaPeloPiso);
    }

    // Homem 110 kg, 175 cm, 35 anos (-15 g sem ajuste, -37 g com -5%): a gordura
    // cedendo até o mínimo já basta — a meta NÃO sobe.
    [Theory]
    [InlineData(0)]
    [InlineData(-0.05)]
    public void Homem_110kg_Antes_Negativo_Agora_Gordura_Cede_Sem_Subir_A_Meta(double ajuste)
    {
        var meta = MetaDescansoPerderPeso("M", 110, 175, 35, (decimal)ajuste);
        Assert.True(DivisaoAntiga(meta, 110).C < 0);

        var r = CalculadoraMacros.Calcular(meta, 110, 175);
        var pesoRef = 30m * 1.75m * 1.75m; // 91,875

        Assert.Equal(100m, r.Macros.CarboidratosG);
        Assert.Equal(1.8m * pesoRef, r.Macros.ProteinasG);
        Assert.Equal(meta, r.Macros.Calorias);
        Assert.False(r.MetaElevadaPeloPiso);
        Assert.InRange(r.Macros.GordurasG, 0.6m * pesoRef, 1.0m * pesoRef);
        Assert.Equal(meta, r.Macros.ProteinasG * 4 + r.Macros.CarboidratosG * 4 + r.Macros.GordurasG * 9);
    }

    // A meta só sobe quando nem com a gordura no mínimo o piso cabe.
    [Fact]
    public void Meta_So_Sobe_Quando_Necessario()
    {
        // Mulher 55 kg, 160 cm, 35 anos: 1031,9 kcal não comportam 99 g P + 100 g C + 33 g G (1093 kcal).
        var pequena = CalculadoraMacros.Calcular(MetaDescansoPerderPeso("F", 55, 160, 35), 55, 160);
        Assert.True(pequena.MetaElevadaPeloPiso);
        Assert.Equal(1093m, pequena.Macros.Calorias);
        Assert.Equal(33m, pequena.Macros.GordurasG);

        // Homem 80 kg, 175 cm, 30 anos, descanso com -5% (1412 kcal): só a gordura cede (80 -> 48,5 g).
        var meta = MetaDescansoPerderPeso("M", 80, 175, 30, -0.05m);
        var r = CalculadoraMacros.Calcular(meta, 80, 175);
        Assert.False(r.MetaElevadaPeloPiso);
        Assert.Equal(meta, r.Macros.Calorias);
        Assert.Equal(100m, r.Macros.CarboidratosG);
        Assert.Equal(144m, r.Macros.ProteinasG);
        Assert.True(r.Macros.GordurasG < 80m && r.Macros.GordurasG >= 48m);
    }

    // Dias de treino e IMC normal com calorias folgadas: divisão idêntica à de antes.
    [Theory]
    [InlineData(2537.0375, 80, 175)]  // homem 80 kg, treino de jiu-jitsu, perder peso
    [InlineData(1748.75, 80, 175)]    // homem 80 kg, descanso, manter peso
    [InlineData(2300, 60, 165)]       // mulher 60 kg, IMC 22, dia de treino
    [InlineData(3500, 95, 185)]       // homem 95 kg, IMC 27,8 (musculoso), ganhar peso
    public void Treino_E_Imc_Normal_Nao_Mudam(double metaKcal, double peso, double altura)
    {
        var meta = (decimal)metaKcal;
        var antes = DivisaoAntiga(meta, (decimal)peso);

        var r = CalculadoraMacros.Calcular(meta, (decimal)peso, (decimal)altura);

        Assert.Equal(meta, r.Macros.Calorias);
        Assert.Equal(antes.P, r.Macros.ProteinasG);
        Assert.Equal(antes.G, r.Macros.GordurasG);
        Assert.Equal(antes.C, r.Macros.CarboidratosG);
        Assert.False(r.MetaElevadaPeloPiso);
    }

    // Varredura: nenhum carboidrato abaixo de 100 g, proteína sempre 1,8 g/kg do peso de
    // referência, gordura nunca abaixo de 0,6 g/kg, calorias = soma dos macros, e a meta
    // nunca desce — só sobe, e só quando marcada.
    [Fact]
    public void Nenhum_Carboidrato_Abaixo_De_100g()
    {
        var casos = 0;
        for (var peso = 40m; peso <= 160m; peso += 5m)
        for (var altura = 145m; altura <= 205m; altura += 10m)
        for (var meta = 700m; meta <= 4500m; meta += 100m)
        {
            var r = CalculadoraMacros.Calcular(meta, peso, altura);
            var pesoRef = Math.Min(peso, 30m * (altura / 100m) * (altura / 100m));
            var m = r.Macros;

            Assert.True(m.CarboidratosG >= 100m, $"carbo {m.CarboidratosG} (peso {peso}, altura {altura}, meta {meta})");
            Assert.Equal(1.8m * pesoRef, m.ProteinasG);
            Assert.True(m.GordurasG >= 0.6m * pesoRef - 0.0001m);
            Assert.True(Math.Abs(m.Calorias - (m.ProteinasG * 4 + m.CarboidratosG * 4 + m.GordurasG * 9)) < 0.0001m);
            Assert.True(m.Calorias >= meta);
            Assert.Equal(m.Calorias > meta, r.MetaElevadaPeloPiso);
            casos++;
        }
        Assert.True(casos > 5000);
    }
}
