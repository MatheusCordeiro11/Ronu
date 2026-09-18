using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de IGeradorDietaIA que integra com a API do Google Gemini.
/// Ainda incompleta de propósito (NotImplementedException) — o corpo real
/// (montagem do prompt, schema JSON, chamada HTTP, parsing da resposta)
/// será implementado em uma etapa seguinte, não nesta.
/// </summary>
public class GeradorDietaGemini : IGeradorDietaIA
{
    private readonly HttpClient _httpClient;
    private readonly ICalculadoraGastoCalorico _calculadora;
    private readonly GeminiOptions _options;

    public GeradorDietaGemini(HttpClient httpClient, ICalculadoraGastoCalorico calculadora, GeminiOptions options)
    {
        _httpClient = httpClient;
        _calculadora = calculadora;
        _options = options;
    }

    public async Task<DietaSemanalDto> GerarDietaAsync(ContextoDietaDto contexto)
    {
        var gastoTotalSemanal = contexto.Modalidades
            .Sum(m => _calculadora.CalcularGastoSemanal(m.MetReferencia, contexto.Peso, m.FrequenciaSemanal));

        var metaCaloriasDiaria = gastoTotalSemanal / 7;

        // TODO: montar o prompt com o contexto + metaCaloriasDiaria
        // TODO: montar o JSON Schema equivalente a DietaSemanalDto
        // TODO: chamar a API da Gemini via _httpClient com saída estruturada
        // TODO: desserializar a resposta para DietaSemanalDto

        throw new NotImplementedException();
    }
}
