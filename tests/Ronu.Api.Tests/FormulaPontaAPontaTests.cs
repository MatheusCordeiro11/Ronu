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
