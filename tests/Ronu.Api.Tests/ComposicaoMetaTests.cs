using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// O arredondamento que faz as partes mostradas somarem o total
/// (ArredondamentoPartes) e a composição da meta salva em cada dia da dieta
/// (CalculadoraManutencao.ComposicaoPorDia e ComposicaoMeta).
/// </summary>
public class ComposicaoMetaTests
{
    private static readonly CalculadoraGastoCalorico Calc = new();

    // ---------- ArredondamentoPartes ----------

    [Fact]
    public void Maior_Resto_Soma_O_Total_E_Nao_Mexe_Nas_Partes_Inteiras()
    {
        // TMB do perfil A: 800 + 1112,5 − 150 + 5 = 1767,5 → 1768.
        var partes = ArredondamentoPartes.MaiorResto(new[] { 800m, 1112.5m, -150m, 5m });

        Assert.Equal(new[] { 800, 1113, -150, 5 }, partes);
        Assert.Equal(1768, partes.Sum());
    }

    [Fact]
    public void Maior_Resto_Com_Varias_Fracoes_E_Negativos()
    {
        // Soma exata 0,9 → 1. Pisos: 0, 0, −1 (soma −1); faltam 2, que vão para
        // as maiores frações (0,7 do −0,3 e 0,4).
        var partes = ArredondamentoPartes.MaiorResto(new[] { 0.4m, 0.8m, -0.3m });

        Assert.Equal(1, partes.Sum());
        Assert.Equal(new[] { 0, 1, 0 }, partes);
    }

    [Fact]
    public void Arredondar_Cada_Parte_Sozinha_Nao_Bastaria()
    {
        // 0,5 + 0,5 = 1, mas 1 + 1 = 2: a última parte absorve a diferença.
        var partes = ArredondamentoPartes.AjustandoUltima(new[] { 0.5m, 0.5m }, 1);

        Assert.Equal(new[] { 1, 0 }, partes);
    }

    [Fact]
    public void Ajustando_Ultima_Pula_As_Partes_Zeradas_No_Fim()
    {
        // Repouso 1767,5 + dia a dia 707 = 2474,5 → 2475 (meta), ajuste e piso 0.
        // Arredondadas: 1768 + 707 = 2475, sem diferença; e com meta 2474 a
        // diferença cai no dia a dia, a última parte diferente de zero.
        var partes = ArredondamentoPartes.AjustandoUltima(new[] { 1767.5m, 707m, 0m, 0m }, 2474);

        Assert.Equal(new[] { 1768, 706, 0, 0 }, partes);
        Assert.Equal(2474, partes.Sum());
    }

    // ---------- Composição da meta (peça B) ----------

    // Perfil A: homem 80 kg, 178 cm, 30 anos, jiu-jitsu 7,8 por 1,5 h seg/ter/qui/sex.
    private static ContextoDietaDto PerfilA(string objetivo = "perder peso") => new()
    {
        Sexo = "Masculino", Peso = 80, Altura = 178, Idade = 30, Objetivo = objetivo,
        Modalidades = new() { new() { Nome = "Jiu-jitsu", MetReferencia = 7.8m, DuracaoHoras = 1.5m, DiasSemana = new[] { 1, 2, 4, 5 } } },
        Preferencias = new()
    };

    [Fact]
    public void Composicao_Da_Formula_Bate_Com_A_Meta_E_A_Manutencao_De_Sempre()
    {
        var composicao = CalculadoraManutencao.ComposicaoPorDia(PerfilA(), Calc);
        var metas = CalculadoraManutencao.CaloriasBasePorDia(PerfilA(), Calc);
        var manutencao = CalculadoraManutencao.ManutencaoPorDia(PerfilA(), Calc);

        Assert.All(composicao, c =>
        {
            Assert.Equal(c.GastoRepouso + c.AtividadeForaDoTreino + c.Treinos.Sum(t => t.Kcal), c.Manutencao);
            Assert.Equal(metas[c.DiaSemana], c.MetaFormula);
            Assert.Equal(manutencao[c.DiaSemana - 1], c.Manutencao);
        });

        var segunda = composicao[0];
        Assert.Equal(1767.5m, segunda.GastoRepouso);
        Assert.Equal(707m, segunda.AtividadeForaDoTreino);
        Assert.Equal(816m, Assert.Single(segunda.Treinos).Kcal);
        Assert.Equal(-440m, segunda.Objetivo);
        Assert.Empty(composicao[2].Treinos); // quarta, descanso
    }

    [Fact]
    public void Composicao_Salva_Soma_Exatamente_A_Meta_Com_Ajuste()
    {
        var segunda = CalculadoraManutencao.ComposicaoPorDia(PerfilA(), Calc)[0];
        var metaFinal = segunda.MetaFormula * (1 - 0.03m); // 2850,5 × 0,97 = 2764,985

        var c = ComposicaoMeta.Montar(segunda, -0.03m, metaFinal);

        Assert.Equal(2765, c.Meta);
        Assert.Equal(c.Meta, c.GastoRepouso + c.AtividadeForaDoTreino + c.Treino.Sum(t => t.Kcal) + c.Objetivo + c.AjusteProgresso + c.PisoCarboidrato);
        Assert.Equal(1768, c.GastoRepouso);
        Assert.Equal(707, c.AtividadeForaDoTreino);
        Assert.Equal(816, Assert.Single(c.Treino).Kcal);
        Assert.Equal(-440, c.Objetivo);
        Assert.Equal(-86, c.AjusteProgresso); // −85,515 arredondado
        Assert.Equal(0, c.PisoCarboidrato);
    }

    [Fact]
    public void Composicao_Salva_Mostra_O_Piso_Quando_A_Meta_Subiu()
    {
        // Dia sem treino que precisou subir 120,3 kcal para caber o mínimo.
        var dia = new ComposicaoFormulaDia(3, 1300.25m, 520.1m, Array.Empty<TreinoDoDia>(), 1820.35m, -455.09m, true, null, 1.4m);
        var metaFinal = dia.MetaFormula + 120.3m;

        var c = ComposicaoMeta.Montar(dia, 0m, metaFinal);

        Assert.Equal(ArredondamentoPartes.Arredondar(metaFinal), c.Meta);
        Assert.Equal(c.Meta, c.GastoRepouso + c.AtividadeForaDoTreino + c.Objetivo + c.AjusteProgresso + c.PisoCarboidrato);
        Assert.Equal(0, c.AjusteProgresso);
        Assert.InRange(c.PisoCarboidrato, 119, 121);
    }

    [Fact]
    public void Dieta_Antiga_Sem_Composicao_Vem_Nula()
    {
        // JSON salvo antes do campo existir (formato do RepositorioDietaIA).
        const string json = """
            {"Dias":[{"DiaSemana":"Segunda-feira","Refeicoes":[],"TotalDoDia":{"Calorias":0,"ProteinasG":0,"CarboidratosG":0,"GordurasG":0},"MetaCalculada":{"Calorias":2000,"ProteinasG":0,"CarboidratosG":0,"GordurasG":0},"MetaElevadaPeloPiso":false}]}
            """;

        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(json)!;

        Assert.Null(dieta.Dias[0].ComposicaoMeta);
    }
}
