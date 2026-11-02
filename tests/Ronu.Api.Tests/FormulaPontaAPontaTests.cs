using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Fórmula de manutenção ponta a ponta no GeradorDietaGemini (Gemini
/// simulado): as metas que vão para a dieta e o texto do prompt.
/// </summary>
public class FormulaPontaAPontaTests
{
    private static readonly string[] Semana =
    {
        "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira",
        "Sexta-feira", "Sábado", "Domingo"
    };

    // Perfil A do g3 com o MET composto do jiu-jitsu (7,8): homem 80 kg, 178 cm,
    // 30 anos, manter peso, 1,5 h seg/ter/qui/sex.
    // Treino: 1767,5 × 1,4 + 6,8 · 80 · 1,5 = 2474,5 + 816 = 3290,5. Descanso: 2474,5.
    private static ContextoDietaDto PerfilA() => new()
    {
        Sexo = "Masculino", Peso = 80, Altura = 178, Idade = 30, Objetivo = "manter peso",
        Modalidades = new()
        {
            new() { Nome = "Jiu-jitsu", MetReferencia = 7.8m, DuracaoHoras = 1.5m, DiasSemana = new[] { 1, 2, 4, 5 } }
        },
        Preferencias = new(), HistoricoPeso = new()
    };

    [Fact]
    public async Task Metas_Do_Perfil_A_E_Texto_Do_Prompt()
    {
        var gemini = new GeminiQueGuardaOPrompt();
        var gerador = new GeradorDietaGemini(
            new HttpClient(gemini), new CalculadoraGastoCalorico(),
            new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
            new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance);

        var dieta = await gerador.GerarDietaAsync(PerfilA());

        var metas = dieta.Dias.ToDictionary(d => d.DiaSemana, d => d.MetaCalculada.Calorias);
        Assert.Equal(3290.5m, metas["Segunda-feira"]);
        Assert.Equal(2474.5m, metas["Quarta-feira"]);

        // A manutenção por dia segue para a MetaDieta (manter peso: igual à meta).
        Assert.Equal(new[] { 3290.5m, 3290.5m, 2474.5m, 3290.5m, 3290.5m, 2474.5m, 2474.5m }, dieta.ManutencaoPorDia);

        // A composição salva em cada dia soma a meta mostrada (3290,5 → 3291).
        var segunda = dieta.Dias.Single(d => d.DiaSemana == "Segunda-feira").ComposicaoMeta!;
        Assert.Equal((1768, 707, 816, 0, 0, 0, 3291),
            (segunda.GastoRepouso, segunda.AtividadeForaDoTreino, Assert.Single(segunda.Treino).Kcal,
             segunda.Objetivo, segunda.AjusteProgresso, segunda.PisoCarboidrato, segunda.Meta));
        Assert.Equal("Jiu-jitsu", segunda.Treino[0].Nome);
        var quarta = dieta.Dias.Single(d => d.DiaSemana == "Quarta-feira").ComposicaoMeta!;
        Assert.Empty(quarta.Treino);
        Assert.Equal(2475, quarta.Meta);
        Assert.All(dieta.Dias, d => Assert.Equal(
            d.ComposicaoMeta!.Meta,
            d.ComposicaoMeta.GastoRepouso + d.ComposicaoMeta.AtividadeForaDoTreino + d.ComposicaoMeta.Treino.Sum(t => t.Kcal)
                + d.ComposicaoMeta.Objetivo + d.ComposicaoMeta.AjusteProgresso + d.ComposicaoMeta.PisoCarboidrato));

        // O prompt leva as metas sem casas decimais (F0 arredonda o ,5 para cima).
        Assert.Contains("- Segunda-feira: 3291 kcal", gemini.Prompt);
        Assert.Contains("- Quarta-feira: 2475 kcal", gemini.Prompt);

        // E os gramas da MetaCalculada de cada dia: proteína 1,8 × 80 = 144 g;
        // gordura 25% das kcal (3290,5 → 91,4 g; 2474,5 → 68,7 g); carboidrato
        // com a sobra (472,9 g e 320,0 g).
        Assert.Contains("- Segunda-feira: 3291 kcal (proteína 144 g, carboidrato 473 g, gordura 91 g)", gemini.Prompt);
        Assert.Contains("- Quarta-feira: 2475 kcal (proteína 144 g, carboidrato 320 g, gordura 69 g)", gemini.Prompt);
        Assert.Contains("13. A soma de proteína, carboidrato e gordura de cada dia deve ficar dentro de uma margem de 10%", gemini.Prompt);
        Assert.Contains("atividade\ndo dia a dia fora do treino", gemini.Prompt.Replace("\r\n", "\n"));
        Assert.DoesNotContain("taxa metabólica basal + gasto", gemini.Prompt);
        Assert.Contains("Você é um nutricionista esportivo.", gemini.Prompt);
        // Dia de treino ou de descanso, sem o nome da modalidade (plano gratuito do Gemini).
        Assert.Contains("- Segunda-feira: 3291 kcal (proteína 144 g, carboidrato 473 g, gordura 91 g) — dia de manutenção; treino\n", gemini.Prompt.Replace("\r\n", "\n"));
        Assert.Contains("- Quarta-feira: 2475 kcal (proteína 144 g, carboidrato 320 g, gordura 69 g) — dia de manutenção; descanso", gemini.Prompt);
        Assert.Contains("15. Monte pelo menos 5 refeições em cada dia e, nos dias com meta a partir de 3500 kcal, pelo menos 6. O lanche antes e o depois do treino contam", gemini.Prompt);
        Assert.DoesNotContain("Jiu-jitsu", gemini.Prompt);
    }

    // O prompt não leva dado pessoal: sexo, idade, peso, altura, objetivo, nome da modalidade, rotina em texto e o
    // "evitar" como foi digitado ficam de fora. Vão os horários estruturados e só os trechos do "evitar" sem casamento.
    [Fact]
    public async Task Prompt_Sem_Dado_Pessoal_Com_Horarios_E_So_O_Evitar_Sem_Casamento()
    {
        var gemini = new GeminiQueGuardaOPrompt();
        var gerador = new GeradorDietaGemini(
            new HttpClient(gemini), new CalculadoraGastoCalorico(),
            new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
            new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance);
        var contexto = PerfilA();
        contexto.Estado = "SP";
        contexto.RotinaDiaria = "Rotina em texto livre que nao pode ir";
        contexto.HorarioAcordar = new TimeOnly(6, 0);
        contexto.HorarioDormir = new TimeOnly(23, 0);
        contexto.Modalidades[0].HorarioInicio = new TimeOnly(19, 30);
        contexto.Preferencias = new()
        {
            new() { Alimento = "Leite", Tipo = "evitar" },
            new() { Alimento = "Kiwano dourado", Tipo = "evitar" }
        };

        await gerador.GerarDietaAsync(contexto);
        var prompt = gemini.Prompt.Replace("\r\n", "\n");

        foreach (var proibido in new[] { "Sexo", "Masculino", "Idade", "30 anos", "80 kg", "178", "Altura", "manter peso", "Objetivo:", "Jiu-jitsu", "Rotina em texto livre", "Leite, " })
        {
            Assert.DoesNotContain(proibido, prompt);
        }
        Assert.Contains("Estado onde a pessoa mora (UF): SP", prompt);
        Assert.Contains("Horários da pessoa: acorda às 06:00 e vai dormir às 23:00.", prompt);
        Assert.Contains("- Segunda-feira: 3291 kcal (proteína 144 g, carboidrato 473 g, gordura 91 g) — dia de manutenção; treino das 19:30 às 21:00", prompt);
        Assert.Contains("- Sábado: 2475 kcal (proteína 144 g, carboidrato 320 g, gordura 69 g) — dia de manutenção; descanso", prompt);
        Assert.Contains("nem como ingrediente de outro prato): kiwano dourado", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("11. Preencha o campo \"horario\" de cada refeição no formato HH:mm (ex: \"07:30\"). Use os horários informados", prompt);
    }

    // Dormir depois da meia-noite: "durmo" menor que "acordo" vira o dia, e o prompt diz isso.
    [Theory]
    [InlineData(7, 0, 0, 30, "acorda às 07:00 e vai dormir às 00:30, já depois da meia-noite.")]
    [InlineData(6, 0, 23, 0, "acorda às 06:00 e vai dormir às 23:00.")]
    public async Task Dormir_Depois_Da_Meia_Noite_Vira_O_Dia_No_Prompt(int hA, int mA, int hD, int mD, string esperado)
    {
        var gemini = new GeminiQueGuardaOPrompt();
        var gerador = new GeradorDietaGemini(
            new HttpClient(gemini), new CalculadoraGastoCalorico(),
            new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
            new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance);
        var contexto = PerfilA();
        contexto.HorarioAcordar = new TimeOnly(hA, mA);
        contexto.HorarioDormir = new TimeOnly(hD, mD);

        await gerador.GerarDietaAsync(contexto);

        Assert.Contains("Horários da pessoa: " + esperado, gemini.Prompt);
    }

    // "Não treino no momento": a dieta sai com os 7 dias iguais (repouso + dia a
    // dia), a composição sem treino e um prompt que não presume treino.
    [Fact]
    public async Task Sem_Nenhuma_Modalidade_Gera_Sem_Treino_E_O_Prompt_Nao_Presume_Treino()
    {
        var gemini = new GeminiQueGuardaOPrompt();
        var gerador = new GeradorDietaGemini(
            new HttpClient(gemini), new CalculadoraGastoCalorico(),
            new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
            new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance);
        var contexto = PerfilA();
        contexto.Modalidades = new();

        var dieta = await gerador.GerarDietaAsync(contexto);

        Assert.All(dieta.Dias, d => Assert.Equal(2474.5m, d.MetaCalculada.Calorias));
        Assert.All(dieta.ManutencaoPorDia!, m => Assert.Equal(2474.5m, m));
        Assert.All(dieta.Dias, d =>
        {
            Assert.Empty(d.ComposicaoMeta!.Treino);
            Assert.Equal((1768, 707, 2475), (d.ComposicaoMeta.GastoRepouso, d.ComposicaoMeta.AtividadeForaDoTreino, d.ComposicaoMeta.Meta));
        });

        Assert.Contains("Você é um nutricionista. Monte", gemini.Prompt);
        Assert.Contains("gordura 69 g) — dia de manutenção\n", gemini.Prompt.Replace("\r\n", "\n"));
        Assert.DoesNotContain("; treino", gemini.Prompt);
        Assert.DoesNotContain("; descanso", gemini.Prompt);
        Assert.DoesNotContain("O lanche antes e o depois do treino", gemini.Prompt);
        Assert.Contains("com horários típicos do brasileiro", gemini.Prompt);
        Assert.Contains("o plano não deve citar treino", gemini.Prompt);
        Assert.DoesNotContain("esportivo", gemini.Prompt);
        Assert.DoesNotContain("Modalidades praticadas", gemini.Prompt);
        Assert.DoesNotContain("dias de treino têm meta mais alta", gemini.Prompt);
        Assert.Contains("- Quarta-feira: 2475 kcal", gemini.Prompt);
    }

    // Guarda o texto do prompt e devolve uma semana válida, sem refeições.
    private sealed class GeminiQueGuardaOPrompt : HttpMessageHandler
    {
        public string Prompt { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var corpo = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Prompt = corpo.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;

            var dias = Semana.Select(n => new
            {
                diaSemana = n,
                refeicoes = Array.Empty<object>(),
                totalDoDia = new { calorias = 0, proteinasG = 0, carboidratosG = 0, gordurasG = 0 }
            });
            var envelope = JsonSerializer.Serialize(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = JsonSerializer.Serialize(new { dias }) } } }, finishReason = "STOP" } }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "application/json")
            };
        }
    }
}
