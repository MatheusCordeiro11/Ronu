using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Piso de carboidrato, peso de referência (IMC 30) e gordura em 25% das
/// calorias da CalculadoraMacros. Antes, proteína (1,8 g/kg) e gordura
/// (1,0 g/kg) usavam o peso total e o carboidrato era só a sobra — negativo em
/// dias de descanso com déficit para IMC alto. Nos casos desse bug, as metas
/// de entrada são as da fórmula daquela época (Mifflin-St Jeor, dia de
/// descanso, -15% de "perder peso"), para provar que eles continuam cobertos.
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

    // Homem 110 kg, 175 cm, 35 anos (-15 g sem ajuste, -37 g com -5%): 25% das
    // calorias (~48 g) ficaria abaixo do mínimo de 0,6 g/kg (55,1 g), então a
    // gordura vai para o mínimo, o carboidrato fica acima do piso e a meta NÃO sobe.
    [Theory]
    [InlineData(0)]
    [InlineData(-0.05)]
    public void Homem_110kg_Antes_Negativo_Agora_Gordura_No_Minimo_Sem_Subir_A_Meta(double ajuste)
    {
        var meta = MetaDescansoPerderPeso("M", 110, 175, 35, (decimal)ajuste);
        Assert.True(DivisaoAntiga(meta, 110).C < 0);

        var r = CalculadoraMacros.Calcular(meta, 110, 175);
        var pesoRef = 30m * 1.75m * 1.75m; // 91,875

        Assert.True(r.Macros.CarboidratosG > 100m);
        Assert.Equal(1.8m * pesoRef, r.Macros.ProteinasG);
        Assert.Equal(0.6m * pesoRef, r.Macros.GordurasG);
        Assert.Equal(meta, r.Macros.Calorias);
        Assert.False(r.MetaElevadaPeloPiso);
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

        // Pessoa de 40 kg (único caso em que 25% das calorias passa do mínimo de 0,6 g/kg
        // e mesmo assim não cabe): 910 kcal pedem 25,3 g de gordura, mas 72 g P + 100 g C
        // deixam só 222 kcal -> a gordura cede para 24,7 g (acima do mínimo de 24 g).
        var r = CalculadoraMacros.Calcular(910m, 40, 160);
        Assert.False(r.MetaElevadaPeloPiso);
        Assert.Equal(910m, r.Macros.Calorias);
        Assert.Equal(100m, r.Macros.CarboidratosG);
        Assert.Equal(72m, r.Macros.ProteinasG);
        Assert.Equal(222m / 9m, r.Macros.GordurasG);
    }

    // Com calorias folgadas: gordura = 25% das calorias, carboidrato = o resto.
    // Metas da fórmula nova nos perfis do g3.
    [Theory]
    [InlineData(3290.5, 80, 178)]     // A, treino de jiu-jitsu (MET 7,8), manter peso
    [InlineData(2474.5, 80, 178)]     // A, descanso
    [InlineData(2034.5, 80, 178)]     // E, descanso, perder peso
    [InlineData(4163.25, 95, 180)]    // C, jiu-jitsu + musculação, ganhar peso
    [InlineData(1488.6, 58, 162)]     // B, descanso, perder peso
    public void Com_Folga_Gordura_E_25_Porcento_Das_Calorias(double metaKcal, double peso, double altura)
    {
        var meta = (decimal)metaKcal;

        var r = CalculadoraMacros.Calcular(meta, (decimal)peso, (decimal)altura);

        Assert.Equal(meta, r.Macros.Calorias);
        Assert.Equal(1.8m * (decimal)peso, r.Macros.ProteinasG);
        Assert.Equal(meta * 0.25m, r.Macros.GordurasG * 9, 6);
        Assert.Equal((meta * 0.75m - r.Macros.ProteinasG * 4) / 4, r.Macros.CarboidratosG, 6);
        Assert.False(r.MetaElevadaPeloPiso);
    }

    // Varredura: nenhum carboidrato abaixo de 100 g, proteína sempre 1,8 g/kg do peso de
    // referência, gordura nunca abaixo de 0,6 g/kg nem — com carboidrato acima do piso —
    // abaixo de 25% das calorias, calorias = soma dos macros, e a meta nunca desce — só
    // sobe, e só quando marcada.
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
            if (m.CarboidratosG > 100m)
            {
                Assert.True(m.GordurasG * 9 >= m.Calorias * 0.25m - 0.0001m, $"gordura {m.GordurasG} (peso {peso}, altura {altura}, meta {meta})");
            }
            Assert.True(Math.Abs(m.Calorias - (m.ProteinasG * 4 + m.CarboidratosG * 4 + m.GordurasG * 9)) < 0.0001m);
            Assert.True(m.Calorias >= meta);
            Assert.Equal(m.Calorias > meta, r.MetaElevadaPeloPiso);
            casos++;
        }
        Assert.True(casos > 5000);
    }
}
