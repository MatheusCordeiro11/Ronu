using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Meta adaptativa com ingestão por período: a meta de cada dia vem da dieta
/// ativa naquele dia (MetasDieta), e o erro da fórmula é o gasto real menos a
/// manutenção da fórmula nos mesmos dias. Os cenários reproduzem a simulação
/// da investigação de 2026-10-03 (perfil E: homem de 80 kg perdendo peso,
/// pesagem a cada 2 dias, 60 dias de histórico) — com o resultado do modelo
/// antigo nos comentários.
/// </summary>
public class AjusteAdaptativoPorPeriodoTests
{
    private static readonly DateTime Hoje = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private const int Atual = CalculadoraManutencao.VersaoFormula;

    // Perfil E com a fórmula nova: manutenção média 2940,8; meta = manutenção − 440.
    private const decimal F = 2940.8m;
    private const decimal M = F - 440m;

    // Fórmula antiga (versão 1) do mesmo perfil: meta média 2102,6 (= 2473,7 × 0,85).
    private const decimal MetaAntiga = 2102.6m;
    private const decimal ManutencaoAntiga = 2473.7m;

    private static CalculadoraAjusteAdaptativo NovaCalculadora() => new(new CalculadoraPesoTendencia());

    private sealed record Dieta(int Dia, decimal Meta, decimal Manutencao, int Versao);

    private static decimal[] Semana(decimal valor) => Enumerable.Repeat(valor, 7).ToArray();

    private static MetaDietaRegistroDto Registro(Dieta d) => new()
    {
        DataGeracao = Hoje.AddDays(d.Dia),
        VersaoFormula = d.Versao,
        MetasPorDia = Semana(d.Meta),
        // Como na carga inicial: dietas de outra versão não têm manutenção.
        ManutencoesPorDia = d.Versao == Atual ? Semana(d.Manutencao) : null
    };

    // Simula o peso dia a dia: a pessoa come a meta da dieta ativa (a mais
    // recente até aquele dia) e gasta gastoReal(dia); pesagem a cada "passo"
    // dias, antes de comer. Devolve o ajuste calculado hoje, com a última
    // dieta como a meta e a manutenção atuais.
    private static AjusteAdaptativoDto Simular(
        int diasHistorico, double pesoInicial, Dieta[] dietas, Func<int, double> gastoReal, int passo = 2)
    {
        var peso = pesoInicial;
        var historico = new List<RegistroPesoAderenciaDto>();
        for (var dia = -diasHistorico; dia <= 0; dia++)
        {
            if ((dia + diasHistorico) % passo == 0 || dia == 0)
            {
                historico.Add(new() { Data = Hoje.AddDays(dia), Peso = (decimal)Math.Round(peso, 4), Objetivo = "perder peso", Aderencia = "seguiu" });
            }

            var ativa = dietas.Where(d => d.Dia <= dia).MaxBy(d => d.Dia);
            var ingestao = ativa is null ? gastoReal(dia) : (double)ativa.Meta;
            peso += (ingestao - gastoReal(dia)) / 7700;
        }

        var atual = dietas[^1];
        return NovaCalculadora().Calcular(
            historico, dietas.Select(Registro).ToList(), "perder peso", atual.Meta, Semana(atual.Manutencao), Hoje);
    }

    private static void AssertAjuste(decimal esperado, AjusteAdaptativoDto r) =>
        Assert.True(Math.Abs(r.Percentual - esperado) <= 0.002m, $"ajuste {r.Percentual}, esperado {esperado} (fator bruto {r.FatorBruto})");

    // ---------- Cenários da simulação (regressão) ----------

    // 1a. Estável, fórmula certa. Antigo: 0%.
    [Fact]
    public void Cenario_1a_Estavel_Formula_Certa()
    {
        var r = Simular(60, 82, new[] { new Dieta(-60, M, F, Atual) }, _ => (double)F);

        AssertAjuste(0m, r);
        Assert.Equal(MotivoAjusteAdaptativo.DentroDoEsperado, r.Motivo);
    }

    // 1b. Gasto real 8% abaixo da fórmula. Antigo: bruto −9,4% -> −4,7% (igual).
    [Fact]
    public void Cenario_1b_Gasto_Real_8_Porcento_Abaixo()
    {
        var r = Simular(60, 82, new[] { new Dieta(-60, M, F, Atual) }, _ => 0.92 * (double)F);

        AssertAjuste(-0.047m, r);
        Assert.Equal(MotivoAjusteAdaptativo.PesoAcimaDoEsperado, r.Motivo);
    }

    // 2. Perdeu ~4 kg em 4 semanas (gasto real = fórmula + 660), com uma dieta
    // nova por semana acompanhando o peso. Antigo: +24,1% -> +5% (teto).
    [Fact]
    public void Cenario_2_Perdeu_4_Kg_Na_Janela()
    {
        static double Manutencao(double p) => (10 * p + 6.25 * 178 - 150 + 5) * 1.4 + 6.8 * p * 1.5 * 4 / 7;
        var dietas = new[] { -35, -28, -21, -14, -7, 0 }.Select((dia, k) =>
        {
            var p = 84.6 - k;
            var f = (decimal)Manutencao(p);
            return new Dieta(dia, f - 5.5m * (decimal)p, f, Atual);
        }).ToArray();

        var r = Simular(60, 85.5, dietas, dia => Manutencao(80 - dia / 7.0) + 660);

        AssertAjuste(0.05m, r);
        Assert.True(r.FatorBruto > 0.2m, $"fator bruto {r.FatorBruto}");
    }

    // 3. Começou musculação (3×1 h, MET 3,5) há 14 dias, fórmula certa: meta e
    // gasto sobem juntos. Antigo: 0% (certo por coincidência). Só com a
    // ingestão, sem a manutenção do período, daria −0,8%.
    [Fact]
    public void Cenario_3_Comecou_Musculacao_No_Meio_Da_Janela()
    {
        var musculacao = 2.5m * 80 * 1 * 3 / 7;
        var dietas = new[] { new Dieta(-60, M, F, Atual), new Dieta(-14, M + musculacao, F + musculacao, Atual) };

        var r = Simular(60, 82, dietas, dia => (double)(F + (dia >= -14 ? musculacao : 0)));

        AssertAjuste(0m, r);
    }

    // 4a. Revisão da fórmula há 14 dias, com dieta nova gerada no deploy; o gasto
    // real é o da fórmula nova. Antigo sem corte: +11,4% -> +5%; com o corte
    // (4b): +5,7% -> +2,9%. Sobra +1,7%: o atraso da tendência (EMA de 7 dias)
    // perto da mudança de regime, que some quando ela sai da janela.
    [Fact]
    public void Cenario_4a_Revisao_Com_Dieta_Nova_No_Deploy()
    {
        var dietas = new[] { new Dieta(-60, MetaAntiga, ManutencaoAntiga, 1), new Dieta(-14, M, F, Atual) };

        var r = Simular(60, 84, dietas, _ => (double)F);

        AssertAjuste(0.017m, r);
    }

    // 4c/4d. Continuou na dieta antiga até hoje (sem gerar outra depois do
    // deploy). Antigo, com ou sem corte: +15,9% -> +5% (errado). Agora a
    // ingestão é a meta antiga que ela de fato comeu.
    [Fact]
    public void Cenario_4c_Continuou_Na_Dieta_Antiga()
    {
        var dietas = new[] { new Dieta(-60, MetaAntiga, ManutencaoAntiga, 1), new Dieta(0, M, F, Atual) };

        var r = Simular(60, 84, dietas, _ => (double)F);

        AssertAjuste(0m, r);
        Assert.Equal(MotivoAjusteAdaptativo.DentroDoEsperado, r.Motivo);
        // Meta prescrita na janela (antiga) ≠ meta atual: ritmos nulos, texto genérico.
        Assert.Null(r.RitmoRealKgSemana);
        Assert.Null(r.RitmoEsperadoKgSemana);
    }

    // ---------- Sem desfazer o próprio ajuste ----------

    // Fórmula 10% acima do gasto real (−11,8% da meta base), uma dieta nova por
    // semana já com o ajuste anterior. O modelo antigo supunha a meta base e
    // caía para um equilíbrio em ⅓ do erro (−3,9%); agora fica no teto (−5%).
    [Fact]
    public void Geracoes_Semanais_Nao_Desfazem_O_Proprio_Ajuste()
    {
        var gastoReal = 0.90 * (double)F;
        var calc = NovaCalculadora();
        var historico = new List<RegistroPesoAderenciaDto>();
        var dietas = new List<MetaDietaRegistroDto>();
        var inicio = Hoje.AddDays(-200);
        var peso = 82.0;
        var percentual = 0m;
        var meta = M;
        var aplicados = new List<decimal>();

        for (var dia = 0; dia < 22 * 7; dia++)
        {
            if (dia % 7 == 0)
            {
                if (dia >= 28)
                {
                    percentual = calc.Calcular(historico, dietas, "perder peso", M, Semana(F), inicio.AddDays(dia)).Percentual;
                    aplicados.Add(percentual);
                }
                meta = M * (1 + percentual);
                dietas.Add(new() { DataGeracao = inicio.AddDays(dia), VersaoFormula = Atual, MetasPorDia = Semana(meta), ManutencoesPorDia = Semana(F) });
            }
            if (dia % 2 == 0)
            {
                historico.Add(new() { Data = inicio.AddDays(dia), Peso = (decimal)Math.Round(peso, 4), Objetivo = "perder peso", Aderencia = "seguiu" });
            }
            peso += ((double)meta - gastoReal) / 7700;
        }

        Assert.Equal(18, aplicados.Count);
        Assert.All(aplicados, p => Assert.Equal(-0.05m, p));
    }

    // ---------- Regras do cálculo ----------

    private static RegistroPesoAderenciaDto Pesagem(int dia, decimal peso = 80, string? aderencia = "seguiu", string objetivo = "manter peso") =>
        new() { Data = Hoje.AddDays(dia), Peso = peso, Objetivo = objetivo, Aderencia = aderencia };

    private static MetaDietaRegistroDto DietaEm(DateTime data, decimal meta, int versao = Atual, decimal[]? manutencoes = null) =>
        new() { DataGeracao = data, VersaoFormula = versao, MetasPorDia = Semana(meta), ManutencoesPorDia = manutencoes };

    // A dieta gerada num dia vale para aquele dia, mesmo às 23:59. Pesagens
    // diárias de −20 a 0: dias −20..−11 na dieta A (2000), −10..−1 na B (3000).
    [Fact]
    public void Dieta_Gerada_No_Dia_Vale_Para_O_Proprio_Dia()
    {
        var historico = Enumerable.Range(-20, 21).Select(d => Pesagem(d)).ToList();
        var dietas = new[]
        {
            DietaEm(Hoje.AddDays(-30), 2000m),
            DietaEm(Hoje.AddDays(-10).Date.AddHours(23).AddMinutes(59), 3000m)
        };

        var r = NovaCalculadora().Calcular(historico, dietas, "manter peso", 3000m, Semana(2500m), Hoje);

        Assert.Equal(20, r.DiasComDieta);
        Assert.Equal(2500m, r.MetaPrescritaMediaDiaria);
    }

    // A aderência vale para os dias que a resposta cobre (até a pesagem
    // anterior), não por registro: 14 dias "comeu um pouco mais" e 6 "seguiu".
    // Por registro (antigo), a média daria 1,03; por dia, 1,105.
    [Fact]
    public void Aderencia_Pesa_Pelos_Dias_Que_Cobre()
    {
        var historico = new List<RegistroPesoAderenciaDto>
        {
            Pesagem(-20), Pesagem(-6, aderencia: "comeu_mais"), Pesagem(-4), Pesagem(-2), Pesagem(0)
        };

        var r = NovaCalculadora().Calcular(historico, new[] { DietaEm(Hoje.AddDays(-30), 2000m) }, "manter peso", 2000m, Semana(2000m), Hoje);

        Assert.Equal((14 * 2300m + 6 * 2000m) / 20, r.IngestaoMediaDiaria);
    }

    // Manutenção do período: a da dieta, se for da versão atual e existir;
    // senão, a manutenção atual (o gasto real não mudou, mudou a conta).
    [Theory]
    [InlineData(Atual, true, 2800)]       // mesma versão, com manutenção: a da dieta
    [InlineData(Atual, false, 2940)]      // mesma versão, sem manutenção (carga inicial): a atual
    [InlineData(Atual - 1, true, 2940)]   // outra versão: a atual, mesmo com manutenção salva
    public void Manutencao_Do_Periodo(int versao, bool comManutencao, int esperada)
    {
        var historico = Enumerable.Range(0, 8).Select(i => Pesagem(-21 + 3 * i)).ToList();
        var dieta = DietaEm(Hoje.AddDays(-30), 2500m, versao, comManutencao ? Semana(2800m) : null);

        var r = NovaCalculadora().Calcular(historico, new[] { dieta }, "manter peso", 2500m, Semana(2940m), Hoje);

        Assert.Equal(esperada, r.ManutencaoMediaDiaria);
    }

    // Sem nenhuma dieta não se sabe o que a pessoa comeu: histórico insuficiente.
    [Fact]
    public void Sem_Dieta_Nao_Ajusta()
    {
        var historico = Enumerable.Range(0, 10).Select(i => Pesagem(-27 + 3 * i)).ToList();

        var r = NovaCalculadora().Calcular(historico, Array.Empty<MetaDietaRegistroDto>(), "manter peso", 2500m, Semana(2500m), Hoje);

        Assert.Equal(MotivoAjusteAdaptativo.HistoricoInsuficiente, r.Motivo);
        Assert.Equal(0, r.PontosUsados);
    }

    // Pesagens anteriores à primeira dieta não contam: com a dieta gerada há 10
    // dias, sobram as pesagens de −9 a 0 (4 em 9 dias) -> insuficiente.
    [Fact]
    public void Pesagens_Antes_Da_Primeira_Dieta_Nao_Contam()
    {
        var historico = Enumerable.Range(0, 10).Select(i => Pesagem(-27 + 3 * i)).ToList();

        var r = NovaCalculadora().Calcular(historico, new[] { DietaEm(Hoje.AddDays(-10), 2500m) }, "manter peso", 2500m, Semana(2500m), Hoje);

        Assert.Equal(MotivoAjusteAdaptativo.HistoricoInsuficiente, r.Motivo);
        Assert.Equal(4, r.PontosUsados);
    }

    // Ritmos no DTO só quando a meta prescrita na janela fica a até 1% da meta
    // atual; acima disso (ex.: a dieta anterior já tinha −5%), vão nulos.
    [Theory]
    [InlineData(2500 * 0.95, false)]
    [InlineData(2500 * 0.995, true)]
    [InlineData(2500, true)]
    public void Ritmos_Nulos_Quando_A_Meta_Prescrita_Difere_Mais_De_1_Porcento(double metaPrescrita, bool ritmosPresentes)
    {
        var historico = Enumerable.Range(0, 8).Select(i => Pesagem(-21 + 3 * i)).ToList();
        var dieta = DietaEm(Hoje.AddDays(-30), (decimal)metaPrescrita, manutencoes: Semana(2500m));

        var r = NovaCalculadora().Calcular(historico, new[] { dieta }, "manter peso", 2500m, Semana(2500m), Hoje);

        Assert.Equal(ritmosPresentes, r.RitmoRealKgSemana is not null);
        Assert.Equal(ritmosPresentes, r.RitmoEsperadoKgSemana is not null);
    }
}
