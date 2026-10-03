using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Meta calórica da fórmula por dia (CalculadoraManutencao), antes da meta
/// adaptativa e dos macros. Os perfis são os da medição g3 (2026-10-03),
/// com os valores recalculados à mão nos comentários.
/// </summary>
public class CalculadoraManutencaoTests
{
    private const int Segunda = 1, Terca = 2, Quarta = 3;

    private static ModalidadeContextoDto Modalidade(decimal met, decimal horas, params int[] dias) => new()
    {
        Nome = "Teste", MetReferencia = met, DuracaoHoras = horas, DiasSemana = dias
    };

    private static ContextoDietaDto Perfil(
        string sexo, decimal peso, decimal altura, int idade, string objetivo, params ModalidadeContextoDto[] modalidades) => new()
    {
        Sexo = sexo, Peso = peso, Altura = altura, Idade = idade, Objetivo = objetivo,
        Modalidades = modalidades.ToList(), Preferencias = new()
    };

    private static IReadOnlyDictionary<int, decimal> Calcular(ContextoDietaDto perfil) =>
        CalculadoraManutencao.CaloriasBasePorDia(perfil, new CalculadoraGastoCalorico());

    [Fact]
    public void Tmb_Mifflin_St_Jeor()
    {
        // 10·80 + 6,25·178 − 5·30 + 5 = 1767,5
        Assert.Equal(1767.5m, CalculadoraManutencao.Tmb("Masculino", 80, 178, 30));
        // 10·58 + 6,25·162 − 5·28 − 161 = 1291,5
        Assert.Equal(1291.5m, CalculadoraManutencao.Tmb("Feminino", 58, 162, 28));
    }

    // METs do Compêndio 2024: aula de luta = 50% técnica + 50% rola/sparring.
    [Fact]
    public void Mets_Do_Compendio_Com_Aula_Em_Blocos()
    {
        var mets = Ronu.Api.Data.MetsCompendio2024.PorModalidade;

        Assert.Equal(0.5m, Ronu.Api.Data.MetsCompendio2024.FracaoTempoBlocoIntenso);
        Assert.Equal(7.8m, mets["Jiu-jitsu"]);  // 0,5·5,3 (15425) + 0,5·10,3 (15430)
        Assert.Equal(7.8m, mets["Muay Thai"]);
        Assert.Equal(6.8m, mets["Boxe"]);       // 0,5·5,8 (15110) + 0,5·7,8 (15120)
        Assert.Equal(3.5m, mets["Musculação"]);
        Assert.Equal(7.0m, mets["Futebol"]);
        Assert.Equal(7.5m, mets["Basquete"]);
        Assert.Equal(5.8m, mets["Natação"]);
        Assert.Equal(7, mets.Count);
    }

    // MET líquido: o repouso (1 MET) já está na TMB e não entra de novo.
    [Fact]
    public void Gasto_Do_Treino_Desconta_O_Repouso()
    {
        var calc = new CalculadoraGastoCalorico();

        Assert.Equal(9.3m * 80 * 1.5m, calc.CalcularGastoSessao(10.3m, 80, 1.5m)); // 1116, não 1236
        Assert.Equal(0m, calc.CalcularGastoSessao(1.0m, 80, 1m));
        Assert.Equal(0m, calc.CalcularGastoSessao(0.5m, 80, 1m)); // nunca negativo
    }

    // Dia de descanso: TMB × 1,4 (atividade fora do treino), não a TMB pura.
    [Fact]
    public void Descanso_E_Tmb_Vezes_Fator_De_Atividade()
    {
        Assert.Equal(1.4m, CalculadoraManutencao.FatorAtividadeForaDoTreino);
        Assert.Equal(1767.5m * 1.4m, CalculadoraManutencao.Manutencao(1767.5m, 0m)); // 2474,5
    }

    // Perfil A do g3: homem 80 kg, 178 cm, 30 anos, manter peso, jiu-jitsu 1,5 h seg/ter/qui/sex.
    [Fact]
    public void Perfil_A_Treino_E_Descanso()
    {
        var dias = Calcular(Perfil("Masculino", 80, 178, 30, "manter peso", Modalidade(10.3m, 1.5m, 1, 2, 4, 5)));

        Assert.Equal(2474.5m + 9.3m * 80 * 1.5m, dias[Segunda]); // 3590,5
        Assert.Equal(2474.5m, dias[Quarta]);
    }

    // Perfil E do g3: o mesmo homem, perdendo peso. Déficit fixo de
    // 0,5%·80 kg/semana = 0,4 kg · 7700 / 7 = 440 kcal em todos os dias.
    [Fact]
    public void Perfil_E_Perder_Peso_Deficit_Fixo_Por_Dia()
    {
        var dias = Calcular(Perfil("Masculino", 80, 178, 30, "perder peso", Modalidade(10.3m, 1.5m, 1, 2, 4, 5)));

        Assert.Equal(3590.5m - 440m, dias[Segunda]);
        Assert.Equal(2474.5m - 440m, dias[Quarta]);
    }

    [Theory]
    [InlineData("perder peso", -5.5)]
    [InlineData("ganhar peso", 2.75)]
    [InlineData("manter peso", 0)]
    public void Ajuste_Do_Objetivo_Em_Kcal_Por_Kg(string objetivo, double kcalPorKg)
    {
        Assert.Equal(3000m + (decimal)kcalPorKg * 80, CalculadoraManutencao.AplicarObjetivo(3000m, objetivo, 80));
    }

    // Teto de 25%: mulher de 130 kg, 165 cm, 40 anos, sem treino. Manutenção
    // 1970,25 × 1,4 = 2758,35; o ritmo pediria 715 kcal (25,9%) -> limitado a 25%.
    // Nos perfis do g3 ele não atua (o maior é D, 22,7%).
    [Fact]
    public void Deficit_Limitado_A_25_Porcento_Da_Manutencao()
    {
        var dias = Calcular(Perfil("Feminino", 130, 165, 40, "perder peso"));
        Assert.Equal(2758.35m * 0.75m, dias[Segunda]);

        // Perfil D do g3 (mulher 90 kg, 160 cm, 36 anos, sem treino): 495 kcal de 2182,6 cabem no teto.
        var d = Calcular(Perfil("Feminino", 90, 160, 36, "perder peso"));
        Assert.Equal(1559m * 1.4m - 495m, d[Segunda]);
    }

    // A meta da fórmula e a meta adaptativa usam o MESMO ritmo: com a fórmula
    // certa, a adaptativa não tem nada a corrigir.
    [Theory]
    [InlineData("perder peso")]
    [InlineData("ganhar peso")]
    public void Ajuste_Do_Objetivo_Bate_Com_O_Ritmo_Esperado_Da_Meta_Adaptativa(string objetivo)
    {
        var historico = Enumerable.Range(0, 10).Select(i => new RegistroPesoAderenciaDto
        {
            Data = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc).AddDays(-3 * i), Peso = 80, Objetivo = objetivo, Aderencia = "seguiu"
        }).ToList();
        var adaptativa = new CalculadoraAjusteAdaptativo(new Ronu.Api.Services.CalculadoraPesoTendencia(), DateTime.MinValue)
            .Calcular(historico, objetivo, 2500m, new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));

        var kcalPorDia = CalculadoraManutencao.AplicarObjetivo(3000m, objetivo, 80) - 3000m;

        Assert.Equal(adaptativa.RitmoEsperadoKgSemana, kcalPorDia * 7m / 7700m);
    }

    // Perfil C do g3: homem 95 kg, 180 cm, 31 anos, ganhar peso, jiu-jitsu 1,5 h
    // seg a sex + musculação 1 h seg/qua/sex — duas modalidades no mesmo dia somam.
    [Fact]
    public void Perfil_C_Duas_Modalidades_No_Mesmo_Dia_Somam()
    {
        var dias = Calcular(Perfil("Masculino", 95, 180, 31, "ganhar peso",
            Modalidade(10.3m, 1.5m, 1, 2, 3, 4, 5), Modalidade(5.0m, 1.0m, 1, 3, 5)));

        // TMB 1925 × 1,4 = 2695; jiu-jitsu 9,3·95·1,5 = 1325,25; musculação 4·95·1 = 380;
        // superávit 0,25%·95 kg/semana = 0,2375 kg · 7700 / 7 = 261,25 kcal/dia.
        Assert.Equal(2695m + 1325.25m + 380m + 261.25m, dias[Segunda]);
        Assert.Equal(2695m + 1325.25m + 261.25m, dias[Terca]);
        Assert.Equal(2695m + 261.25m, dias[6]);
    }
}
