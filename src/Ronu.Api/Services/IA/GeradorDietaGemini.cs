using System.Net.Http.Json;
using System.Text.Json;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de IGeradorDietaIA que integra com a API do Google Gemini,
/// usando saída estruturada (JSON Schema) para garantir que a resposta sempre
/// venha no formato esperado, sem parsing frágil de texto livre.
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
        var gastoTreinoSemanal = contexto.Modalidades
            .Sum(m => _calculadora.CalcularGastoSemanal(m.MetReferencia, contexto.Peso, m.FrequenciaSemanal));

        var metaMacros = CalcularMetaMacros(gastoTreinoSemanal, contexto);

        var prompt = MontarPrompt(contexto, metaMacros.Calorias);
        var schema = MontarSchema();

        var corpoRequisicao = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = schema
            }
        };

        using var mensagem = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-lite-latest:generateContent");

        mensagem.Headers.Add("x-goog-api-key", _options.ApiKey);
        mensagem.Content = JsonContent.Create(corpoRequisicao);

        var resposta = await _httpClient.SendAsync(mensagem);
        resposta.EnsureSuccessStatusCode();

        // A resposta da Gemini vem embrulhada em candidates[0].content.parts[0].text,
        // que por sua vez contém (como STRING) o JSON que respeita o schema que
        // enviamos — por isso desserializamos em duas etapas: uma para "desembrulhar"
        // a resposta da API, outra para extrair a dieta estruturada de dentro do texto.
        var respostaBruta = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var textoJson = respostaBruta
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;

        var opcoesDesserializacao = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var respostaIa = JsonSerializer.Deserialize<RespostaDiasIaDto>(textoJson, opcoesDesserializacao)!;

        return new DietaSemanalDto
        {
            Dias = respostaIa.Dias,
            // MetaDiariaCalculada NUNCA vem da IA — é sempre calculada em C#,
            // com fórmula determinística, para garantir precisão.
            MetaDiariaCalculada = metaMacros
        };
    }

    private static string MontarPrompt(ContextoDietaDto contexto, decimal metaCaloriasDiaria)
    {
        var modalidadesTexto = string.Join(", ", contexto.Modalidades
            .Select(m => $"{m.Nome} ({m.FrequenciaSemanal}x por semana)"));

        var preferidosTexto = string.Join(", ", contexto.Preferencias
            .Where(p => p.Tipo == "preferido")
            .Select(p => p.Alimento));

        var evitarTexto = string.Join(", ", contexto.Preferencias
            .Where(p => p.Tipo == "evitar")
            .Select(p => p.Alimento));

        return $"""
            Você é um nutricionista esportivo. Monte um plano alimentar semanal (7 dias)
            para uma pessoa com o seguinte perfil:

            - Sexo: {contexto.Sexo}
            - Idade: {contexto.Idade} anos
            - Peso: {contexto.Peso} kg
            - Altura: {contexto.Altura} cm
            - Objetivo: {contexto.Objetivo}
            - Modalidades praticadas: {modalidadesTexto}
            - Meta calórica diária: {metaCaloriasDiaria:F0} kcal (calculada a partir da taxa
              metabólica basal e do gasto calórico real das atividades acima — respeite este
              valor rigorosamente)

            Alimentos que a pessoa prefere (inclua quando fizer sentido nutricionalmente): {(string.IsNullOrEmpty(preferidosTexto) ? "nenhuma preferência informada" : preferidosTexto)}

            Alimentos que a pessoa deve evitar (NUNCA inclua nenhum destes, nem em pequena
            quantidade, nem como ingrediente de outro prato): {(string.IsNullOrEmpty(evitarTexto) ? "nenhuma restrição informada" : evitarTexto)}

            Regras obrigatórias:
            1. A soma de calorias de cada dia deve ficar entre {metaCaloriasDiaria * 0.95m:F0} e {metaCaloriasDiaria * 1.05m:F0} kcal (margem de 5% para mais ou para menos).
            2. Nunca inclua nenhum alimento da lista de restrição, em nenhuma refeição, em nenhum dia.
            3. Varie a fonte principal de proteína entre os dias da semana (ex: frango, carne vermelha, peixe, ovos, leguminosas) — não repita a mesma fonte de proteína em dias consecutivos.
            4. Não repita a mesma refeição (mesmos alimentos) em dois dias seguidos.
            5. Retorne quantidades realistas e mensuráveis para cada alimento (em gramas, mililitros ou unidades).
            """;
    }

    private static object MontarSchema()
    {
        var macros = new
        {
            type = "OBJECT",
            properties = new
            {
                calorias = new { type = "NUMBER" },
                proteinasG = new { type = "NUMBER" },
                carboidratosG = new { type = "NUMBER" },
                gordurasG = new { type = "NUMBER" }
            },
            required = new[] { "calorias", "proteinasG", "carboidratosG", "gordurasG" }
        };

        var alimento = new
        {
            type = "OBJECT",
            properties = new
            {
                nome = new { type = "STRING" },
                quantidade = new { type = "NUMBER" },
                unidade = new { type = "STRING" }
            },
            required = new[] { "nome", "quantidade", "unidade" }
        };

        var refeicao = new
        {
            type = "OBJECT",
            properties = new
            {
                nome = new { type = "STRING" },
                alimentos = new { type = "ARRAY", items = alimento },
                macros
            },
            required = new[] { "nome", "alimentos", "macros" }
        };

        var dia = new
        {
            type = "OBJECT",
            properties = new
            {
                diaSemana = new { type = "STRING" },
                refeicoes = new { type = "ARRAY", items = refeicao },
                totalDoDia = macros
            },
            required = new[] { "diaSemana", "refeicoes", "totalDoDia" }
        };

        // Só "dias" no schema — MetaDiariaCalculada NÃO é pedida à IA, porque já
        // temos o valor exato calculado em C#. Deixar a IA "ecoar" esse número de
        // volta no JSON criaria risco de um valor levemente diferente do real.
        return new
        {
            type = "OBJECT",
            properties = new
            {
                dias = new { type = "ARRAY", items = dia }
            },
            required = new[] { "dias" }
        };
    }

    private static MacrosDto CalcularMetaMacros(decimal gastoTreinoSemanal, ContextoDietaDto contexto)
    {
        var gastoTreinoDiario = gastoTreinoSemanal / 7;

        // Mifflin-St Jeor: fórmula de TMB (Taxa Metabólica Basal) mais precisa e
        // validada cientificamente — representa 60-75% do gasto calórico diário
        // total, a maior fatia, então precisa entrar na conta (o gasto do treino
        // sozinho não é o suficiente para representar a necessidade real do dia).
        var tmb = contexto.Sexo == "Masculino"
            ? (10 * contexto.Peso) + (6.25m * contexto.Altura) - (5 * contexto.Idade) + 5
            : (10 * contexto.Peso) + (6.25m * contexto.Altura) - (5 * contexto.Idade) - 161;

        var manutencao = tmb + gastoTreinoDiario;

        // Ajuste de superávit/déficit moderado sobre a manutenção (evidência:
        // Aragon & Schoenfeld), nunca agressivo, para preservar massa magra e
        // evitar ganho excessivo de gordura. Objetivo vem de um conjunto fixo
        // de valores definidos no onboarding do frontend (radio buttons), não
        // texto livre — por isso o match direto de string é seguro aqui.
        const decimal AjustePercentualMvp = 0.15m;

        var metaCalorias = contexto.Objetivo switch
        {
            "ganhar peso" => manutencao * (1 + AjustePercentualMvp),
            "perder peso" => manutencao * (1 - AjustePercentualMvp),
            "manter peso" => manutencao,
            _ => manutencao
        };

        // Baseado em diretrizes de nutrição esportiva (ACSM): proteína e gordura
        // calculadas por peso corporal (mais preciso que percentual fixo de
        // calorias), carboidrato preenche o restante calórico.
        const decimal ProteinaGramasPorKgMvp = 1.8m;
        const decimal GorduraGramasPorKgMvp = 1.0m;
        const decimal CaloriasPorGramaProteina = 4m;
        const decimal CaloriasPorGramaGordura = 9m;
        const decimal CaloriasPorGramaCarboidrato = 4m;

        var proteinaG = contexto.Peso * ProteinaGramasPorKgMvp;
        var gorduraG = contexto.Peso * GorduraGramasPorKgMvp;

        var caloriasProteina = proteinaG * CaloriasPorGramaProteina;
        var caloriasGordura = gorduraG * CaloriasPorGramaGordura;
        var caloriasCarboidrato = metaCalorias - caloriasProteina - caloriasGordura;
        var carboidratoG = caloriasCarboidrato / CaloriasPorGramaCarboidrato;

        return new MacrosDto
        {
            Calorias = metaCalorias,
            ProteinasG = proteinaG,
            CarboidratosG = carboidratoG,
            GordurasG = gorduraG
        };
    }

    // Classe auxiliar interna, só para desserializar o formato reduzido que a
    // Gemini devolve (sem MetaDiariaCalculada, calculada separadamente em C#).
    private class RespostaDiasIaDto
    {
        public required List<DiaDietaDto> Dias { get; set; }
    }
}
