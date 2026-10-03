using System.Diagnostics;
using System.Globalization;
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
    private readonly ICalculadoraAjusteAdaptativo _calculadoraAjuste;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeradorDietaGemini> _logger;

    // Resposta inválida (bloqueada, cortada, JSON quebrado, dias errados) ganha
    // UMA nova chamada: cada uma leva ~11–19 s, então mesmo com o cold start do
    // F1 (~45 s) as duas cabem nos 100 s do HttpClient e nos 230 s do Azure.
    // Falha de rede/HTTP não entra aqui — continua indo direto para o 503.
    private const int MaximoTentativas = 2;

    public GeradorDietaGemini(
        HttpClient httpClient,
        ICalculadoraGastoCalorico calculadora,
        ICalculadoraAjusteAdaptativo calculadoraAjuste,
        GeminiOptions options,
        ILogger<GeradorDietaGemini> logger)
    {
        _httpClient = httpClient;
        _calculadora = calculadora;
        _calculadoraAjuste = calculadoraAjuste;
        _options = options;
        _logger = logger;
    }

    public async Task<DietaSemanalDto> GerarDietaAsync(ContextoDietaDto contexto)
    {
        // Meta da fórmula (sem mudança) -> ajuste adaptativo sobre ela, a partir
        // do histórico real de peso -> metas finais, que vão para o prompt e
        // para a MetaCalculada de cada dia. Ajuste 0% = metas idênticas às da
        // fórmula.
        var caloriasBasePorDia = CalcularCaloriasBasePorDia(contexto, _calculadora);
        var ajuste = _calculadoraAjuste.Calcular(
            contexto.HistoricoPeso, contexto.Objetivo, caloriasBasePorDia.Values.Average(), DateTime.UtcNow);
        var metasPorDia = MontarMetasPorDia(caloriasBasePorDia, ajuste.Percentual, contexto.Peso, contexto.Altura);

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

        var cronometro = Stopwatch.StartNew();

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                var (respostaIa, uso) = await ChamarGeminiAsync(corpoRequisicao);
                var dias = MontarDias(respostaIa, metasPorDia);

                RegistrarGeracao(dias, respostaIa, uso, tentativa, cronometro.ElapsedMilliseconds);

                return new DietaSemanalDto { Dias = dias, AjusteAdaptativo = ajuste };
            }
            catch (RespostaIaInvalidaException ex) when (tentativa < MaximoTentativas)
            {
                _logger.LogWarning(ex, "Resposta inválida do Gemini na tentativa {Tentativa} ({TempoMs} ms até aqui); tentando de novo",
                    tentativa, cronometro.ElapsedMilliseconds);
            }
        }
    }

    // Uma chamada ao Gemini. Erro de rede/HTTP sobe como HttpRequestException
    // (sem nova tentativa); resposta 200 que não serve vira RespostaIaInvalidaException.
    private async Task<(RespostaDiasIaDto Resposta, UsoTokens Uso)> ChamarGeminiAsync(object corpoRequisicao)
    {
        using var mensagem = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-lite-latest:generateContent");

        mensagem.Headers.Add("x-goog-api-key", _options.ApiKey);
        mensagem.Content = JsonContent.Create(corpoRequisicao);

        var resposta = await _httpClient.SendAsync(mensagem);
        resposta.EnsureSuccessStatusCode();

        JsonElement respostaBruta;
        try
        {
            respostaBruta = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch (JsonException ex)
        {
            throw new RespostaIaInvalidaException("Envelope da resposta do Gemini não é JSON válido.", ex);
        }

        // A resposta da Gemini vem embrulhada em candidates[0].content.parts[0].text,
        // que por sua vez contém (como STRING) o JSON que respeita o schema que
        // enviamos — por isso desserializamos em duas etapas: uma para "desembrulhar"
        // a resposta da API, outra para extrair a dieta estruturada de dentro do texto.
        // Prompt bloqueado chega sem candidates (o motivo vem em promptFeedback).
        if (!respostaBruta.TryGetProperty("candidates", out var candidatos)
            || candidatos.ValueKind != JsonValueKind.Array
            || candidatos.GetArrayLength() == 0)
        {
            var motivoBloqueio = respostaBruta.TryGetProperty("promptFeedback", out var feedback)
                && feedback.TryGetProperty("blockReason", out var bloqueio)
                ? bloqueio.ToString()
                : "não informado";
            throw new RespostaIaInvalidaException($"Resposta do Gemini sem candidates (blockReason: {motivoBloqueio}).");
        }

        var candidato = candidatos[0];

        // STOP é o fim normal. MAX_TOKENS (cortada), SAFETY, RECITATION etc.
        // deixam o JSON incompleto ou ausente, mesmo quando ainda vem algum texto.
        if (candidato.TryGetProperty("finishReason", out var finishReason) && finishReason.GetString() != "STOP")
        {
            throw new RespostaIaInvalidaException($"Gemini encerrou a resposta com finishReason {finishReason.GetString()}.");
        }

        if (!candidato.TryGetProperty("content", out var conteudo)
            || !conteudo.TryGetProperty("parts", out var partes)
            || partes.ValueKind != JsonValueKind.Array
            || partes.GetArrayLength() == 0
            || !partes[0].TryGetProperty("text", out var texto)
            || texto.ValueKind != JsonValueKind.String)
        {
            throw new RespostaIaInvalidaException("Resposta do Gemini sem o texto em content.parts[0].text.");
        }

        RespostaDiasIaDto? respostaIa;
        try
        {
            var opcoesDesserializacao = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            respostaIa = JsonSerializer.Deserialize<RespostaDiasIaDto>(texto.GetString()!, opcoesDesserializacao);
        }
        catch (JsonException ex)
        {
            throw new RespostaIaInvalidaException("Texto da resposta do Gemini não é um JSON de dieta válido.", ex);
        }

        if (respostaIa?.Dias is null)
        {
            throw new RespostaIaInvalidaException("JSON da resposta do Gemini sem a lista de dias.");
        }

        return (respostaIa, LerUsoTokens(respostaBruta));
    }

    // Casamos a meta calculada (C#) com o dia devolvido pela IA PELO NOME,
    // nunca pela posição no array — mais robusto que confiar em ordem. A
    // semana precisa vir completa: exatamente 7 dias, cada um com um dos 7
    // nomes esperados e sem repetição (7 únicos dentre 7 nomes = todos).
    // O TotalDoDia é recalculado como a soma das refeições: o declarado pela
    // IA já veio diferente da soma (até 27%) e o dashboard mostra os dois.
    private static List<DiaDietaDto> MontarDias(RespostaDiasIaDto respostaIa, Dictionary<string, ResultadoMacros> metasPorDia)
    {
        if (respostaIa.Dias.Count != NomesDias.Length)
        {
            throw new RespostaIaInvalidaException($"Resposta do Gemini com {respostaIa.Dias.Count} dias, em vez de {NomesDias.Length}.");
        }

        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return respostaIa.Dias.Select(d =>
        {
            var nome = d.DiaSemana.Trim();

            if (!metasPorDia.TryGetValue(nome, out var meta))
            {
                throw new RespostaIaInvalidaException(
                    $"Dia '{d.DiaSemana}' retornado pela IA não corresponde a nenhum dos 7 dias esperados.");
            }

            if (!vistos.Add(nome))
            {
                throw new RespostaIaInvalidaException($"Dia '{d.DiaSemana}' veio repetido na resposta do Gemini.");
            }

            return new DiaDietaDto
            {
                DiaSemana = d.DiaSemana,
                Refeicoes = d.Refeicoes,
                TotalDoDia = SomarRefeicoes(d.Refeicoes),
                MetaCalculada = meta.Macros,
                MetaElevadaPeloPiso = meta.MetaElevadaPeloPiso
            };
        }).ToList();
    }

    private static MacrosDto SomarRefeicoes(List<RefeicaoDto> refeicoes) => new()
    {
        Calorias = refeicoes.Sum(r => r.Macros.Calorias),
        ProteinasG = refeicoes.Sum(r => r.Macros.ProteinasG),
        CarboidratosG = refeicoes.Sum(r => r.Macros.CarboidratosG),
        GordurasG = refeicoes.Sum(r => r.Macros.GordurasG)
    };

    // usageMetadata é só para o log: se faltar algum campo, fica nulo.
    private static UsoTokens LerUsoTokens(JsonElement respostaBruta)
    {
        if (!respostaBruta.TryGetProperty("usageMetadata", out var uso))
        {
            return new UsoTokens(null, null, null, null);
        }

        int? Ler(string campo) =>
            uso.TryGetProperty(campo, out var valor) && valor.TryGetInt32(out var numero) ? numero : null;

        // thoughtsTokenCount só vem quando o modelo "pensa" antes de responder;
        // fica fora do candidatesTokenCount, mas entra no total.
        return new UsoTokens(Ler("promptTokenCount"), Ler("candidatesTokenCount"), Ler("thoughtsTokenCount"), Ler("totalTokenCount"));
    }

    // Uma linha por geração: tempo total (com eventual nova tentativa), tokens
    // da chamada que deu certo e, por dia, a soma das refeições contra a meta —
    // com o total que a IA declarou quando ele não bate com a soma.
    private void RegistrarGeracao(
        List<DiaDietaDto> dias, RespostaDiasIaDto respostaIa, UsoTokens uso, int tentativas, long tempoMs)
    {
        var declaradoPorDia = respostaIa.Dias.ToDictionary(d => d.DiaSemana.Trim(), d => d.TotalDoDia.Calorias, StringComparer.OrdinalIgnoreCase);

        var desvios = string.Join("; ", dias.Select(d =>
        {
            var soma = d.TotalDoDia.Calorias;
            var meta = d.MetaCalculada.Calorias;
            var desvio = meta == 0 ? 0 : (soma - meta) / meta * 100;
            var declarado = declaradoPorDia[d.DiaSemana.Trim()];
            var aviso = declarado == soma ? string.Empty : $" [IA declarou {declarado.ToString("F0", CultureInfo.InvariantCulture)}]";
            return string.Create(CultureInfo.InvariantCulture, $"{d.DiaSemana.Trim()} {soma:F0}/{meta:F0} kcal ({desvio:+0.0;-0.0}%){aviso}");
        }));

        _logger.LogInformation(
            "Dieta gerada pelo Gemini em {TempoMs} ms ({Tentativas} tentativa(s)); tokens: entrada {TokensEntrada}, resposta {TokensResposta}, raciocínio {TokensRaciocinio}, total {TokensTotal}. Soma das refeições vs. meta por dia: {DesvioPorDia}",
            tempoMs, tentativas, uso.Entrada, uso.Resposta, uso.Raciocinio ?? 0, uso.Total, desvios);
    }

    // Calorias da fórmula por dia (CalculadoraManutencao), antes da meta
    // adaptativa, pelo nome do dia.
    private static Dictionary<string, decimal> CalcularCaloriasBasePorDia(
        ContextoDietaDto contexto, ICalculadoraGastoCalorico calculadora) =>
        CalculadoraManutencao.CaloriasBasePorDia(contexto, calculadora)
            .ToDictionary(d => NomesDias[d.Key - 1], d => d.Value, StringComparer.OrdinalIgnoreCase);

    // Aplica o ajuste adaptativo nas calorias de cada dia e monta os macros.
    // Proteína e gordura dependem do peso corporal, então o ajuste cai no
    // carboidrato — até o piso de 100 g (CalculadoraMacros). Percentual 0 =
    // metas exatamente as da fórmula.
    private static Dictionary<string, ResultadoMacros> MontarMetasPorDia(
        Dictionary<string, decimal> caloriasBasePorDia, decimal percentualAjuste, decimal pesoKg, decimal alturaCm)
    {
        var metas = new Dictionary<string, ResultadoMacros>(StringComparer.OrdinalIgnoreCase);

        foreach (var (dia, caloriasBase) in caloriasBasePorDia)
        {
            metas[dia] = CalculadoraMacros.Calcular(caloriasBase * (1 + percentualAjuste), pesoKg, alturaCm);
        }

        return metas;
    }

    private static string MontarPrompt(ContextoDietaDto contexto, Dictionary<string, ResultadoMacros> metasPorDia)
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
            $"- {nome}: {metasPorDia[nome].Macros.Calorias:F0} kcal"));

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

            Metas calóricas diárias (calculadas a partir do gasto em repouso, da atividade
            do dia a dia fora do treino e do gasto real do treino de CADA dia específico, já
            com o déficit ou superávit do objetivo — dias de treino têm meta mais alta que
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

        // O total que a IA declarou: não vai para a dieta (ela recebe a soma das
        // refeições), só para o log quando os dois não batem.
        public required MacrosDto TotalDoDia { get; set; }
    }

    // usageMetadata da chamada que deu certo (só para o log).
    private record UsoTokens(int? Entrada, int? Resposta, int? Raciocinio, int? Total);
}
