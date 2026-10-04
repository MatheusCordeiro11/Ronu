using System.Text.Json;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// O que vai para a tabela MetasDieta a cada dieta gerada
/// (RepositorioDietaIA.MontarMetaDieta) e a manutenção por dia que o gerador
/// passa adiante (CalculadoraManutencao.ManutencaoPorDia).
/// </summary>
public class MetaDietaTests
{
    private static readonly DateTime Data = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

    private static MacrosDto Macros(decimal calorias) => new()
    {
        Calorias = calorias, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0
    };

    private static DiaDietaDto Dia(string nome, decimal meta) => new()
    {
        DiaSemana = nome,
        Refeicoes = new(),
        TotalDoDia = Macros(0),
        MetaCalculada = Macros(meta)
    };

    // A IA pode devolver os dias em outra ordem e com outra grafia: as metas
    // vão pela ordem segunda ... domingo, casadas pelo nome.
    [Fact]
    public void Metas_Pela_Ordem_Da_Semana_Casadas_Pelo_Nome()
    {
        var dieta = new DietaSemanalDto
        {
            Dias = new()
            {
                Dia("Domingo", 7), Dia(" segunda-feira ", 1), Dia("SÁBADO", 6), Dia("Terça-feira", 2),
                Dia("Quarta-feira", 3), Dia("Quinta-feira", 4), Dia("Sexta-feira", 5)
            },
            AjusteAdaptativo = new AjusteAdaptativoDto { Percentual = -0.031m, Motivo = MotivoAjusteAdaptativo.PesoAcimaDoEsperado },
            ManutencaoPorDia = new[] { 10m, 20m, 30m, 40m, 50m, 60m, 70m }
        };

        var meta = RepositorioDietaIA.MontarMetaDieta(42, Data, dieta);

        Assert.Equal(new[] { 1m, 2m, 3m, 4m, 5m, 6m, 7m }, meta.MetasPorDia);
        Assert.Equal(new[] { 10m, 20m, 30m, 40m, 50m, 60m, 70m }, meta.ManutencoesPorDia);
        Assert.Equal(-0.031m, meta.PercentualAjusteAdaptativo);
        Assert.Equal(CalculadoraManutencao.VersaoFormula, meta.VersaoFormula);
        Assert.Equal(42, meta.UsuarioId);
        Assert.Equal(Data, meta.DataGeracao);
        Assert.Null(meta.DietaIAId); // só depois de salvar a DietaIA
    }

    [Fact]
    public void Sem_Ajuste_Nem_Manutencao_Ficam_Nulos()
    {
        var dieta = new DietaSemanalDto { Dias = GeradorDietaGemini.NomesDias.Select(n => Dia(n, 2000)).ToList() };

        var meta = RepositorioDietaIA.MontarMetaDieta(1, Data, dieta);

        Assert.Null(meta.PercentualAjusteAdaptativo);
        Assert.Null(meta.ManutencoesPorDia);
        Assert.All(meta.MetasPorDia, m => Assert.Equal(2000m, m));
    }

    [Fact]
    public void Dia_Desconhecido_E_Erro()
    {
        var dieta = new DietaSemanalDto { Dias = new() { Dia("Feriado", 2000) } };

        Assert.Throws<InvalidOperationException>(() => RepositorioDietaIA.MontarMetaDieta(1, Data, dieta));
    }

    // A manutenção vai para a MetasDieta, não para o JSON salvo nem para a API.
    [Fact]
    public void Manutencao_Nao_Entra_No_Json()
    {
        var dieta = new DietaSemanalDto { Dias = new(), ManutencaoPorDia = new[] { 1m, 2m, 3m, 4m, 5m, 6m, 7m } };

        Assert.DoesNotContain("ManutencaoPorDia", JsonSerializer.Serialize(dieta));
    }

    // Perfil A do g3 (jiu-jitsu 7,8 seg/ter/qui/sex): manutenção por dia e a meta
    // da fórmula derivada dela, com o objetivo.
    [Fact]
    public void Manutencao_Por_Dia_E_A_Base_Da_Meta_Da_Formula()
    {
        var contexto = new ContextoDietaDto
        {
            Sexo = "Masculino", Peso = 80, Altura = 178, Idade = 30, Objetivo = "perder peso",
            Modalidades = new() { new() { Nome = "Jiu-jitsu", MetReferencia = 7.8m, DuracaoHoras = 1.5m, DiasSemana = new[] { 1, 2, 4, 5 } } },
            Preferencias = new()
        };
        var calc = new CalculadoraGastoCalorico();

        var manutencao = CalculadoraManutencao.ManutencaoPorDia(contexto, calc);
        var metas = CalculadoraManutencao.CaloriasBasePorDia(contexto, calc);

        Assert.Equal(new[] { 3290.5m, 3290.5m, 2474.5m, 3290.5m, 3290.5m, 2474.5m, 2474.5m }, manutencao);
        for (var dia = 1; dia <= 7; dia++)
        {
            Assert.Equal(manutencao[dia - 1] - 440m, metas[dia]);
        }
    }
}
