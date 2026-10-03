using System.Net;
using System.Text;
using System.Text.Json;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Meta calórica adaptativa. A calculadora é pura: recebe o histórico, o
/// objetivo, a meta base e o "agora" por parâmetro e usa a CalculadoraPesoTendencia
/// real (também pura) — sem banco, sem relógio do sistema, sem Gemini.
/// </summary>
public class AjusteAdaptativoTests
{
    private static readonly DateTime Agora = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
    private const decimal MetaBase = 2500m;

    private static CalculadoraAjusteAdaptativo NovaCalculadora() => new(new CalculadoraPesoTendencia());

    private static RegistroPesoAderenciaDto Registro(int diasAtras, decimal peso, string objetivo, string? aderencia = "seguiu") =>
        new() { Data = Agora.AddDays(-diasAtras), Peso = peso, Objetivo = objetivo, Aderencia = aderencia };

    // Uma pesagem a cada 3 dias, de "diasAtras" até hoje, com o peso variando
    // linearmente a "kgPorSemana" a partir de "pesoInicial".
    private static List<RegistroPesoAderenciaDto> Rampa(
        int diasAtras, decimal pesoInicial, decimal kgPorSemana, string objetivo, string? aderencia = "seguiu")
    {
        var registros = new List<RegistroPesoAderenciaDto>();
        for (var d = diasAtras; d >= 0; d -= 3)
        {
            var diasDesdeInicio = diasAtras - d;
            registros.Add(Registro(d, pesoInicial + kgPorSemana * diasDesdeInicio / 7m, objetivo, aderencia));
        }
        return registros;
    }

    // 1) Histórico insuficiente -> 0%
    [Fact]
    public void Historico_Insuficiente_Nao_Ajusta()
    {
        var calc = NovaCalculadora();

        // Vazio
        var vazio = calc.Calcular(new List<RegistroPesoAderenciaDto>(), "perder peso", MetaBase, Agora);
        // Só 4 pontos, mesmo cobrindo 18 dias
        var poucosPontos = calc.Calcular(new List<RegistroPesoAderenciaDto>
        {
            Registro(18, 80, "perder peso"), Registro(12, 80, "perder peso"),
            Registro(6, 80, "perder peso"), Registro(0, 80, "perder peso")
        }, "perder peso", MetaBase, Agora);
        // 6 pontos, mas só 10 dias de intervalo
        var intervaloCurto = calc.Calcular(
            Enumerable.Range(0, 6).Select(i => Registro(i * 2, 80, "perder peso")).ToList(), "perder peso", MetaBase, Agora);
        // 10 pontos, todos fora da janela de 28 dias
        var foraDaJanela = calc.Calcular(Rampa(90, 80, 0, "perder peso").Where(r => r.Data < Agora.AddDays(-28)).ToList(),
            "perder peso", MetaBase, Agora);

        foreach (var r in new[] { vazio, poucosPontos, intervaloCurto, foraDaJanela })
        {
            Assert.Equal(0m, r.Percentual);
            Assert.False(r.Aplicado);
            Assert.Equal(MotivoAjusteAdaptativo.HistoricoInsuficiente, r.Motivo);
        }
        Assert.Equal(4, poucosPontos.PontosUsados);
        Assert.Equal(0, foraDaJanela.PontosUsados);
    }

    // 2) Dias "nao_seguiu" ficam fora da regressão e da estimativa de ingestão
    [Fact]
    public void Dias_Nao_Seguiu_Sao_Ignorados()
    {
        var calc = NovaCalculadora();
        // 7 pesagens em 21 dias, peso parado, objetivo manter: suficiente e dentro do esperado
        var historico = Enumerable.Range(0, 7).Select(i => Registro(i * 3, 80, "manter peso")).ToList();
        var completo = calc.Calcular(historico, "manter peso", MetaBase, Agora);

        // Os mesmos dias, mas 3 deles "nao_seguiu" e com "comeu_mais" absurdo se contassem
        var comDescartes = historico.Select((r, i) => i is 1 or 3 or 5
            ? Registro((int)(Agora - r.Data).TotalDays, 80, "manter peso", "nao_seguiu")
            : r).ToList();
        var resultado = calc.Calcular(comDescartes, "manter peso", MetaBase, Agora);

        Assert.Equal(MotivoAjusteAdaptativo.DentroDoEsperado, completo.Motivo);
        Assert.Equal(7, completo.PontosUsados);
        // Sobram 4 pontos válidos (< 5): os descartados não contam como ponto de dado
        Assert.Equal(4, resultado.PontosUsados);
        Assert.Equal(MotivoAjusteAdaptativo.HistoricoInsuficiente, resultado.Motivo);
        Assert.Equal(0m, resultado.Percentual);
    }

    // 3) Peso parado com objetivo de perder peso -> reduz a meta (limitado a -5%)
    [Fact]
    public void Peso_Parado_Perdendo_Peso_Reduz_A_Meta()
    {
        var resultado = NovaCalculadora().Calcular(Rampa(27, 80, 0, "perder peso"), "perder peso", MetaBase, Agora);

        // Esperado -0,4 kg/sem (0,5% de 80 kg), real 0: fator bruto = -0,4·1100/2500 = -17,6%,
        // amortecido para -8,8% e limitado pelo teto em -5%.
        Assert.Equal(-0.05m, resultado.Percentual);
        Assert.True(resultado.Aplicado);
        Assert.Equal(MotivoAjusteAdaptativo.PesoAcimaDoEsperado, resultado.Motivo);
        Assert.Equal(0m, resultado.RitmoRealKgSemana);
        Assert.Equal(-0.4m, resultado.RitmoEsperadoKgSemana);
    }

    // 4) "comeu_mais" explica parte do ganho de peso -> a redução é menor
    [Fact]
    public void Comeu_Mais_Reduz_O_Ajuste()
    {
        var calc = NovaCalculadora();
        // Ganhando 0,5 kg/semana com objetivo manter; 60 dias de histórico para a
        // tendência assentar antes da janela de 28 dias.
        var seguiu = calc.Calcular(Rampa(60, 78, 0.5m, "manter peso", "seguiu"), "manter peso", MetaBase, Agora);
        var comeuMais = calc.Calcular(Rampa(60, 78, 0.5m, "manter peso", "comeu_mais"), "manter peso", MetaBase, Agora);

        // Seguindo a dieta: todo o ganho é "erro" da fórmula -> corte máximo (-5%).
        Assert.Equal(-0.05m, seguiu.Percentual);
        // Comendo 15% a mais: boa parte do ganho é explicada pela ingestão -> corte menor.
        Assert.True(comeuMais.Percentual > seguiu.Percentual, $"comeu_mais={comeuMais.Percentual}, seguiu={seguiu.Percentual}");
        Assert.True(comeuMais.Percentual < 0m);
        Assert.Equal(MotivoAjusteAdaptativo.PesoAcimaDoEsperado, comeuMais.Motivo);
        Assert.Equal(seguiu.RitmoRealKgSemana, comeuMais.RitmoRealKgSemana);
    }

    // 5) Peso no ritmo esperado -> zona morta, sem ajuste
    [Fact]
    public void Ritmo_Dentro_Do_Esperado_Cai_Na_Zona_Morta()
    {
        // Perdendo ~0,5% do peso por semana (≈ -0,4 kg/sem em torno de 80 kg).
        var resultado = NovaCalculadora().Calcular(Rampa(60, 83.5m, -0.4m, "perder peso"), "perder peso", MetaBase, Agora);

        Assert.Equal(0m, resultado.Percentual);
        Assert.False(resultado.Aplicado);
        Assert.Equal(MotivoAjusteAdaptativo.DentroDoEsperado, resultado.Motivo);
        Assert.NotNull(resultado.RitmoRealKgSemana);
        Assert.True(Math.Abs(resultado.RitmoRealKgSemana!.Value - resultado.RitmoEsperadoKgSemana!.Value) < 0.02m);
    }

    // 6) Troca de objetivo corta a janela: só conta o período do objetivo atual
    [Fact]
    public void Troca_De_Objetivo_Corta_A_Janela()
    {
        var calc = NovaCalculadora();
        // 9 pesagens nos últimos 24 dias; as 5 mais antigas ainda eram "ganhar peso".
        var historico = Rampa(24, 80, 0, "perder peso");
        var comTroca = historico.Select((r, i) => i < 5
            ? Registro((int)Math.Round((Agora - r.Data).TotalDays), r.Peso, "ganhar peso")
            : r).ToList();

        var semTroca = calc.Calcular(historico, "perder peso", MetaBase, Agora);
        var resultado = calc.Calcular(comTroca, "perder peso", MetaBase, Agora);

        Assert.Equal(9, semTroca.PontosUsados);
        Assert.NotEqual(MotivoAjusteAdaptativo.HistoricoInsuficiente, semTroca.Motivo);
        Assert.Equal(4, resultado.PontosUsados);
        Assert.Equal(MotivoAjusteAdaptativo.HistoricoInsuficiente, resultado.Motivo);
        Assert.Equal(0m, resultado.Percentual);
    }

    // 7) Ajuste 0% -> metas idênticas às da fórmula de hoje (gerador real, Gemini simulado)
    [Fact]
    public async Task Fator_Zero_Mantem_As_Metas_Da_Formula()
    {
        // Masculino, 175 cm, 30 anos, 80 kg, manter peso, sem modalidades:
        // TMB = 10·80 + 6,25·175 − 5·30 + 5 = 1748,75 kcal todos os dias.
        // Proteína 1,8·80 = 144 g; gordura 1,0·80 = 80 g; carboidrato (1748,75 − 576 − 720)/4 = 113,1875 g.
        var contexto = ContextoBase(historico: new() { Registro(0, 80, "manter peso") });
        var gerador = new GeradorDietaGemini(
            new HttpClient(new GeminiFalso()), new CalculadoraGastoCalorico(),
            NovaCalculadora(), new GeminiOptions { ApiKey = "teste" });

        var dieta = await gerador.GerarDietaAsync(contexto);

        Assert.Equal(7, dieta.Dias.Count);
        foreach (var dia in dieta.Dias)
        {
            Assert.Equal(1748.75m, dia.MetaCalculada.Calorias);
            Assert.Equal(144m, dia.MetaCalculada.ProteinasG);
            Assert.Equal(80m, dia.MetaCalculada.GordurasG);
            Assert.Equal(113.1875m, dia.MetaCalculada.CarboidratosG);
        }
        Assert.NotNull(dieta.AjusteAdaptativo);
        Assert.Equal(0m, dieta.AjusteAdaptativo!.Percentual);
        Assert.Equal(MotivoAjusteAdaptativo.HistoricoInsuficiente, dieta.AjusteAdaptativo.Motivo);
    }

    // Complemento do 7: com ajuste, ele entra na caloria e cai inteiro no carboidrato.
    // Altura 185 cm (TMB 1811,25): com -5% o carboidrato fica em ~106 g, acima do
    // piso de 100 g da CalculadoraMacros — abaixo dele, o piso mudaria a divisão.
    [Fact]
    public async Task Ajuste_Entra_Na_Caloria_E_Cai_No_Carboidrato()
    {
        var gerador = new GeradorDietaGemini(
            new HttpClient(new GeminiFalso()), new CalculadoraGastoCalorico(),
            new AjusteFixo(-0.05m), new GeminiOptions { ApiKey = "teste" });

        var dieta = await gerador.GerarDietaAsync(ContextoBase(historico: new(), altura: 185));

        foreach (var dia in dieta.Dias)
        {
            Assert.Equal(1811.25m * 0.95m, dia.MetaCalculada.Calorias);
            Assert.Equal(144m, dia.MetaCalculada.ProteinasG);
            Assert.Equal(80m, dia.MetaCalculada.GordurasG);
            Assert.Equal((1811.25m * 0.95m - 576m - 720m) / 4m, dia.MetaCalculada.CarboidratosG);
            Assert.False(dia.MetaElevadaPeloPiso);
        }
        Assert.Equal(-0.05m, dieta.AjusteAdaptativo!.Percentual);
    }

    // O enum vai como texto no JSON salvo (contrato com o dashboard)
    [Fact]
    public void Motivo_Serializa_Como_Texto()
    {
        var json = JsonSerializer.Serialize(new AjusteAdaptativoDto { Percentual = -0.03m, Motivo = MotivoAjusteAdaptativo.PesoAcimaDoEsperado });
        Assert.Contains("\"Motivo\":\"PesoAcimaDoEsperado\"", json);
        Assert.Contains("\"Aplicado\":true", json);
    }

    private static ContextoDietaDto ContextoBase(List<RegistroPesoAderenciaDto> historico, decimal altura = 175) => new()
    {
        Altura = altura, Sexo = "Masculino", Idade = 30, Peso = 80, Objetivo = "manter peso",
        Modalidades = new(), Preferencias = new(), HistoricoPeso = historico
    };

    // Ajuste fixo, para isolar a aplicação do percentual no gerador.
    private sealed class AjusteFixo(decimal percentual) : ICalculadoraAjusteAdaptativo
    {
        public AjusteAdaptativoDto Calcular(IReadOnlyList<RegistroPesoAderenciaDto> historico, string objetivoAtual, decimal metaBaseMediaDiaria, DateTime agoraUtc) =>
            new() { Percentual = percentual, Motivo = MotivoAjusteAdaptativo.PesoAcimaDoEsperado };
    }

    // Responde no formato da API do Gemini (candidates[0].content.parts[0].text com
    // o JSON da dieta), sem rede: os 7 dias, sem refeições.
    private sealed class GeminiFalso : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var nomes = new[] { "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado", "Domingo" };
            var dias = nomes.Select(n => new
            {
                diaSemana = n,
                refeicoes = Array.Empty<object>(),
                totalDoDia = new { calorias = 0, proteinasG = 0, carboidratosG = 0, gordurasG = 0 }
            });
            var texto = JsonSerializer.Serialize(new { dias });
            var envelope = JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = texto } } } } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "application/json")
            });
        }
    }
}
