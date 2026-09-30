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
    private static readonly string[] NomesDias =
    {
        "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira",
        "Sexta-feira", "Sábado", "Domingo"
    };

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
        var metasPorDia = CalcularMetasPorDia(contexto, _calculadora);

        var prompt = MontarPrompt(contexto, metasPorDia);
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

        // Casamos a meta calculada (C#) com o dia devolvido pela IA PELO NOME,
        // nunca pela posição no array — mais robusto que confiar em ordem: se
        // a IA devolver um nome de dia que não bate com nenhum dos 7
        // esperados, isso falha alto e claro aqui, em vez de silenciosamente
        // atribuir a meta errada a um dia por engano de posição.
        var dias = respostaIa.Dias.Select(d => new DiaDietaDto
        {
            DiaSemana = d.DiaSemana,
            Refeicoes = d.Refeicoes,
            TotalDoDia = d.TotalDoDia,
            MetaCalculada = metasPorDia.TryGetValue(d.DiaSemana.Trim(), out var meta)
                ? meta
                : throw new InvalidOperationException(
                    $"Dia '{d.DiaSemana}' retornado pela IA não corresponde a nenhum dos 7 dias esperados.")
        }).ToList();

        return new DietaSemanalDto { Dias = dias };
    }

    private static Dictionary<string, MacrosDto> CalcularMetasPorDia(
        ContextoDietaDto contexto, ICalculadoraGastoCalorico calculadora)
    {
        // Mifflin-St Jeor: fórmula de TMB, mais precisa e validada
        // cientificamente. É a mesma para todos os dias (não depende de
        // treino) — só o gasto de treino varia por dia.
        var tmb = contexto.Sexo == "Masculino"
            ? (10 * contexto.Peso) + (6.25m * contexto.Altura) - (5 * contexto.Idade) + 5
            : (10 * contexto.Peso) + (6.25m * contexto.Altura) - (5 * contexto.Idade) - 161;

        var metas = new Dictionary<string, MacrosDto>(StringComparer.OrdinalIgnoreCase);

        for (int dia = 1; dia <= 7; dia++)
        {
            var gastoTreinoDia = contexto.Modalidades
                .Where(m => m.DiasSemana.Contains(dia))
                .Sum(m => calculadora.CalcularGastoSessao(m.MetReferencia, contexto.Peso, m.DuracaoHoras));

            var manutencao = tmb + gastoTreinoDia;
            var metaCalorias = AplicarAjusteObjetivo(manutencao, contexto.Objetivo);

            metas[NomesDias[dia - 1]] = MontarMacros(metaCalorias, contexto.Peso);
        }

        return metas;
    }

    private static decimal AplicarAjusteObjetivo(decimal manutencao, string objetivo)
    {
        // Ajuste de superávit/déficit moderado (evidência: Aragon &
        // Schoenfeld). Objetivo vem de um conjunto fixo de valores definidos
        // no frontend (radio buttons), não texto livre — match direto seguro.
        const decimal AjustePercentualMvp = 0.15m;

        return objetivo switch
        {
            "ganhar peso" => manutencao * (1 + AjustePercentualMvp),
            "perder peso" => manutencao * (1 - AjustePercentualMvp),
            "manter peso" => manutencao,
            _ => manutencao
        };
    }

    private static MacrosDto MontarMacros(decimal metaCalorias, decimal pesoKg)
    {
        // Baseado em diretrizes de nutrição esportiva (ACSM): proteína e
        // gordura por peso corporal, carboidrato preenche o restante.
        const decimal ProteinaGramasPorKgMvp = 1.8m;
        const decimal GorduraGramasPorKgMvp = 1.0m;
        const decimal CaloriasPorGramaProteina = 4m;
        const decimal CaloriasPorGramaGordura = 9m;
        const decimal CaloriasPorGramaCarboidrato = 4m;

        var proteinaG = pesoKg * ProteinaGramasPorKgMvp;
        var gorduraG = pesoKg * GorduraGramasPorKgMvp;

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

    private static string MontarPrompt(ContextoDietaDto contexto, Dictionary<string, MacrosDto> metasPorDia)
    {
        var modalidadesTexto = string.Join(", ", contexto.Modalidades
            .Select(m => $"{m.Nome} ({string.Join(", ", m.DiasSemana.OrderBy(d => d).Select(d => NomesDias[d - 1]))})"));

        var preferidosTexto = string.Join(", ", contexto.Preferencias
            .Where(p => p.Tipo == "preferido")
            .Select(p => p.Alimento));

        var evitarTexto = string.Join(", ", contexto.Preferencias
            .Where(p => p.Tipo == "evitar")
            .Select(p => p.Alimento));

        var metasTexto = string.Join("\n", NomesDias.Select(nome =>
            $"- {nome}: {metasPorDia[nome].Calorias:F0} kcal"));

        // Contas via Google ou anteriores ao campo podem estar sem estado —
        // nesse caso a linha some do perfil (sem deixar linha em branco).
        var estadoTexto = string.IsNullOrEmpty(contexto.Estado)
            ? string.Empty
            : $"\n- Estado onde mora (UF): {contexto.Estado}";

        // Rotina é opcional — sem ela o parágrafo some e os horários seguem
        // o padrão típico brasileiro (regra 11).
        var rotinaTexto = string.IsNullOrEmpty(contexto.RotinaDiaria)
            ? string.Empty
            : $"\n\nRotina diária da pessoa (use para estimar os horários de cada refeição): {contexto.RotinaDiaria}";

        // Orçamento é opcional — sem ele o parágrafo some e a regra 12 trata
        // como "não informado" (bom senso de custo-benefício).
        var orcamentoTexto = string.IsNullOrEmpty(contexto.OrcamentoSemanal)
            ? string.Empty
            : $"\n\nOrçamento semanal da pessoa para alimentação: {contexto.OrcamentoSemanal}";

        return $"""
            Você é um nutricionista esportivo. Monte um plano alimentar semanal (7 dias)
            para uma pessoa com o seguinte perfil:

            - Sexo: {contexto.Sexo}
            - Idade: {contexto.Idade} anos
            - Peso: {contexto.Peso} kg
            - Altura: {contexto.Altura} cm
            - Objetivo: {contexto.Objetivo}
            - Modalidades praticadas (com os dias da semana de cada uma): {modalidadesTexto}{estadoTexto}

            Metas calóricas diárias (calculadas a partir da taxa metabólica basal + gasto
            real de treino de CADA dia específico — dias de treino têm meta mais alta que
            dias de descanso):
            {metasTexto}

            Alimentos que a pessoa gosta (use como base do plano, mas NÃO se limite a eles: complete com outros alimentos comuns e adequados ao objetivo, variando as opções ao longo da semana): {(string.IsNullOrEmpty(preferidosTexto) ? "nenhuma preferência informada" : preferidosTexto)}

            Alimentos que a pessoa deve evitar (NUNCA inclua nenhum destes, nem em pequena
            quantidade, nem como ingrediente de outro prato): {(string.IsNullOrEmpty(evitarTexto) ? "nenhuma restrição informada" : evitarTexto)}{rotinaTexto}{orcamentoTexto}

            Regras obrigatórias:
            1. A soma de calorias de cada dia deve ficar dentro de uma margem de 5% (para mais ou para menos) da meta ESPECÍFICA daquele dia, listada acima — cada dia tem uma meta diferente, não use um valor único para todos os 7.
            2. Nunca inclua nenhum alimento da lista de restrição, em nenhuma refeição, em nenhum dia.
            3. Varie a fonte principal de proteína entre os dias da semana — não repita a mesma fonte de proteína em dias consecutivos.
            4. Não repita a mesma refeição (mesmos alimentos) em dois dias seguidos.
            5. Retorne quantidades realistas e mensuráveis para cada alimento (em gramas, mililitros ou unidades).
            6. Use EXATAMENTE os nomes dos dias como escritos acima (ex: "Segunda-feira") no campo diaSemana de cada dia — precisa corresponder exatamente a um dos 7 nomes listados.
            7. Adeque o plano ao hábito alimentar brasileiro: use as refeições típicas (café da manhã, lanche da manhã, almoço, lanche da tarde, jantar e, se fizer sentido, ceia leve) e combinações que um brasileiro realmente consome. Bebidas com cafeína (café, chá preto, chá mate, energéticos) apenas de manhã e no início da tarde, nunca no jantar nem na ceia.
            8. Bebida alcoólica só pode aparecer se estiver entre os alimentos preferidos da pessoa, no máximo uma vez na semana, no sábado ou no domingo, em quantidade moderada (por exemplo, uma lata ou long neck), com as calorias contabilizadas no dia.
            9. Use os nomes regionais dos alimentos abaixo, de acordo com o estado (UF) da pessoa — esta é a referência oficial, não invente outras variações regionais além destas:
               - Feijão: nos estados RS, SC e PR, chame de "feijão preto". Em todos os demais estados, ou se o estado não foi informado, chame apenas de "feijão" (sem especificar o tipo).
               - Mandioca: no estado RJ, chame de "aipim". Nos estados AM, PA, AC, RO, RR, AP, TO, MA, PI, CE, RN, PB, PE, AL, SE e BA, chame de "macaxeira". Em todos os demais estados (SP, MG, ES, PR, SC, RS, MT, MS, GO, DF), ou se o estado não foi informado, chame de "mandioca".
               Para qualquer outro alimento não listado aqui, use o nome mais comum no Brasil, sem tentar adivinhar outras variações regionais.
            10. Para um mesmo alimento, use sempre a mesma unidade de medida em todas as refeições e dias da semana. Itens contáveis (ovo, fruta inteira, fatia de pão) sempre em unidades; alimentos sólidos em gramas; líquidos em mililitros. Nunca escreva o mesmo alimento em gramas em um lugar e em unidades em outro.
            11. Preencha o campo "horario" de cada refeição no formato HH:mm (ex: "07:30"). Se a rotina diária da pessoa foi informada, baseie os horários nela; caso contrário, use horários típicos do brasileiro (café da manhã entre 6h30 e 8h, almoço entre 12h e 13h30, jantar entre 19h e 21h).
            12. Se o orçamento semanal informado for "economico", priorize proteínas e ingredientes de menor custo (ex: ovo, frango, peixes populares como tilápia, feijão), evitando itens caros como salmão, camarão ou carnes nobres, sem comprometer a qualidade nutricional. Se for "moderado" ou não informado, use bom senso de custo-benefício. Se for "sem_restricao", não considere custo na escolha dos alimentos.
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
                macros,
                horario = new { type = "STRING" }
            },
            required = new[] { "nome", "alimentos", "macros", "horario" }
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

    // Classes auxiliares internas, só para desserializar o formato reduzido
    // que a Gemini devolve (sem MetaCalculada, calculada separadamente em C#).
    private class RespostaDiasIaDto
    {
        public required List<DiaDietaIaDto> Dias { get; set; }
    }

    private class DiaDietaIaDto
    {
        public required string DiaSemana { get; set; }
        public required List<RefeicaoDto> Refeicoes { get; set; }
        public required MacrosDto TotalDoDia { get; set; }
    }
}
