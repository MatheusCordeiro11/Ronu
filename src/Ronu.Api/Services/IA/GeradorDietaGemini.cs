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
    // Índice 0 = segunda: a mesma ordem de MetaDieta.MetasPorDia.
    public static readonly string[] NomesDias =
    {
        "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira",
        "Sexta-feira", "Sábado", "Domingo"
    };

    private readonly HttpClient _httpClient;
    private readonly ICalculadoraGastoCalorico _calculadora;
    private readonly ICalculadoraAjusteAdaptativo _calculadoraAjuste;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeradorDietaGemini> _logger;

    // Versão fixa, não o alias gemini-flash-lite-latest: o alias troca de modelo
    // a cada lançamento (com aviso só por email), o que mudaria o comportamento
    // do cardápio e os limites do schema sem nenhum deploy nosso. Era também a
    // versão por trás do alias nas medições g3–g8 e na fase 0 da base nutricional.
    public const string Modelo = "gemini-3.5-flash-lite";

    // Resposta inválida (bloqueada, cortada, JSON quebrado, dias errados) ou
    // sem resposta dentro de TimeoutPorTentativa ganha UMA nova chamada. Cada
    // chamada costuma levar ~20 s ou ~42 s; sem o limite, um Gemini travado
    // segurava a requisição até os 100 s do HttpClient (2 em 15 gerações de
    // teste). Pior caso: cold start do F1 (~45 s) + 2 × 60 s = ~165 s, abaixo
    // dos 230 s do Azure. Falha de rede/HTTP não entra aqui — continua indo
    // direto para o 503.
    private const int MaximoTentativas = 2;

    public static readonly TimeSpan TimeoutPadraoPorTentativa = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Limite de cada chamada ao Gemini (envio e leitura da resposta). Só os
    /// testes mudam este valor.
    /// </summary>
    public TimeSpan TimeoutPorTentativa { get; init; } = TimeoutPadraoPorTentativa;

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
        // do histórico real de peso e das metas das dietas anteriores -> metas
        // finais, que vão para o prompt e para a MetaCalculada de cada dia.
        // Ajuste 0% = metas idênticas às da fórmula.
        var caloriasBasePorDia = CalcularCaloriasBasePorDia(contexto, _calculadora);
        var manutencaoPorDia = CalculadoraManutencao.ManutencaoPorDia(contexto, _calculadora);
        var metaBaseMedia = caloriasBasePorDia.Values.Average();
        var ajuste = _calculadoraAjuste.Calcular(
            contexto.HistoricoPeso, contexto.HistoricoMetas, contexto.Objetivo, metaBaseMedia, manutencaoPorDia, DateTime.UtcNow);
        RegistrarAjusteAdaptativo(ajuste, metaBaseMedia);
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
                var (respostaIa, uso, versaoModelo) = await ChamarGeminiAsync(corpoRequisicao);
                var dias = MontarDias(respostaIa, metasPorDia);
                ValidarBebidaAlcoolica(dias, contexto);
                var normalizacao = NormalizadorUnidades.Normalizar(dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos));

                RegistrarGeracao(dias, respostaIa, uso, versaoModelo, normalizacao, tentativa, cronometro.ElapsedMilliseconds);

                return new DietaSemanalDto { Dias = dias, AjusteAdaptativo = ajuste, ManutencaoPorDia = manutencaoPorDia };
            }
            catch (RespostaIaInvalidaException ex) when (tentativa < MaximoTentativas)
            {
                _logger.LogWarning(ex, "Resposta inválida do Gemini na tentativa {Tentativa} ({TempoMs} ms até aqui); tentando de novo",
                    tentativa, cronometro.ElapsedMilliseconds);
            }
        }
    }

    // Uma chamada ao Gemini. Erro de rede/HTTP sobe como HttpRequestException
    // (sem nova tentativa); resposta 200 que não serve, ou nenhuma resposta
    // dentro de TimeoutPorTentativa, vira RespostaIaInvalidaException.
    private async Task<(RespostaDiasIaDto Resposta, UsoTokens Uso, string? VersaoModelo)> ChamarGeminiAsync(object corpoRequisicao)
    {
        using var mensagem = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Modelo}:generateContent");

        mensagem.Headers.Add("x-goog-api-key", _options.ApiKey);
        mensagem.Content = JsonContent.Create(corpoRequisicao);

        // O limite vale para o envio e a leitura da resposta. Fica abaixo do
        // Timeout do HttpClient (100 s), que assim nunca chega a disparar.
        using var limite = new CancellationTokenSource(TimeoutPorTentativa);

        JsonElement respostaBruta;
        try
        {
            var resposta = await _httpClient.SendAsync(mensagem, limite.Token);
            resposta.EnsureSuccessStatusCode();
            respostaBruta = await resposta.Content.ReadFromJsonAsync<JsonElement>(limite.Token);
        }
        catch (OperationCanceledException ex) when (limite.IsCancellationRequested)
        {
            throw new RespostaIaInvalidaException(
                $"Gemini não respondeu em {TimeoutPorTentativa.TotalSeconds:F0} s.", ex);
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

        // A versão que de fato respondeu (só para o log).
        var versaoModelo = respostaBruta.TryGetProperty("modelVersion", out var versao) && versao.ValueKind == JsonValueKind.String
            ? versao.GetString()
            : null;

        return (respostaIa, LerUsoTokens(respostaBruta), versaoModelo);
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

    // Regra 8 do prompt: bebida alcoólica só se a pessoa a marcou como
    // preferida. Fora disso, a resposta é inválida (nova tentativa, depois 503).
    private static void ValidarBebidaAlcoolica(List<DiaDietaDto> dias, ContextoDietaDto contexto)
    {
        var preferidos = contexto.Preferencias.Where(p => p.Tipo == "preferido").Select(p => p.Alimento);
        var bebida = ValidadorBebidaAlcoolica.EncontrarNaoPermitida(
            dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), preferidos);

        if (bebida is not null)
        {
            throw new RespostaIaInvalidaException(
                $"Resposta do Gemini com bebida alcoólica fora das preferências da pessoa: '{bebida}'.");
        }
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

    // Uma linha por geração com o diagnóstico da meta adaptativa, para medir em
    // produção (os campos de diagnóstico não vão para o JSON salvo).
    private void RegistrarAjusteAdaptativo(AjusteAdaptativoDto ajuste, decimal metaBaseMedia)
    {
        _logger.LogInformation(
            "Meta adaptativa: {Motivo}, ajuste {Percentual}, fator bruto {FatorBruto}, erro da fórmula {ErroKcal} kcal/dia; ingestão {Ingestao}, manutenção {Manutencao}, meta prescrita {MetaPrescrita} vs base {MetaBase} kcal/dia; {Pontos} pesagens, {Dias} dias com dieta; ritmos no DTO: {Ritmos}",
            ajuste.Motivo, ajuste.Percentual, ajuste.FatorBruto, ajuste.ErroFormulaKcalDia,
            ajuste.IngestaoMediaDiaria, ajuste.ManutencaoMediaDiaria, ajuste.MetaPrescritaMediaDiaria, Math.Round(metaBaseMedia, 1),
            ajuste.PontosUsados, ajuste.DiasComDieta, ajuste.RitmoRealKgSemana is null ? "nulos" : "presentes");
    }

    // Uma linha por geração: versão do modelo que respondeu, tempo total (com
    // eventual nova tentativa), tokens da chamada que deu certo e, por dia, a
    // soma das refeições contra a meta
    // (kcal e o desvio de cada macro), a divergência entre as kcal e os macros
    // declarados, e o total de kcal que a IA declarou quando ele não bate com a soma.
    // Também as unidades: quantas a normalização reescreveu e as que ficaram fora
    // de "g"/"ml" (NormalizadorUnidades), que impedem o cálculo daquele alimento.
    private void RegistrarGeracao(
        List<DiaDietaDto> dias, RespostaDiasIaDto respostaIa, UsoTokens uso, string? versaoModelo, ResultadoNormalizacao normalizacao, int tentativas, long tempoMs)
    {
        var declaradoPorDia = respostaIa.Dias.ToDictionary(d => d.DiaSemana.Trim(), d => d.TotalDoDia.Calorias, StringComparer.OrdinalIgnoreCase);

        var desvios = string.Join("; ", dias.Select(d =>
        {
            var soma = d.TotalDoDia.Calorias;
            var meta = d.MetaCalculada.Calorias;
            var desvio = DesvioPercentual(soma, meta);
            var declarado = declaradoPorDia[d.DiaSemana.Trim()];
            var aviso = declarado == soma ? string.Empty : $" [IA declarou {declarado.ToString("F0", CultureInfo.InvariantCulture)}]";
            var desvioP = DesvioPercentual(d.TotalDoDia.ProteinasG, d.MetaCalculada.ProteinasG);
            var desvioC = DesvioPercentual(d.TotalDoDia.CarboidratosG, d.MetaCalculada.CarboidratosG);
            var desvioG = DesvioPercentual(d.TotalDoDia.GordurasG, d.MetaCalculada.GordurasG);
            // Coerência interna da resposta (só registro, nada é recalculado): as
            // kcal somadas das refeições contra 4P+4C+9G dos macros declarados. No
            // g6, uma geração com kcal, carboidrato e gordura idênticos à meta tinha
            // até +4,7% aqui — sinal de números encaixados na meta.
            var kcalDosMacros = 4 * d.TotalDoDia.ProteinasG + 4 * d.TotalDoDia.CarboidratosG + 9 * d.TotalDoDia.GordurasG;
            var divergenciaMacros = DesvioPercentual(kcalDosMacros, soma);
            return string.Create(CultureInfo.InvariantCulture,
                $"{d.DiaSemana.Trim()} {soma:F0}/{meta:F0} kcal ({desvio:+0.0;-0.0}%) P {desvioP:+0;-0}% C {desvioC:+0;-0}% G {desvioG:+0;-0}% 4P+4C+9G {divergenciaMacros:+0.0;-0.0}%{aviso}");
        }));

        var foraDoPadrao = normalizacao.ForaDoPadrao.Count == 0
            ? "nenhuma"
            : string.Join(", ", normalizacao.ForaDoPadrao.Select(u => $"\"{u.Key}\" x{u.Value}"));

        _logger.LogInformation(
            "Dieta gerada pelo Gemini ({VersaoModelo}) em {TempoMs} ms ({Tentativas} tentativa(s)); tokens: entrada {TokensEntrada}, resposta {TokensResposta}, raciocínio {TokensRaciocinio}, total {TokensTotal}. Unidades: {Alimentos} alimentos, {UnidadesCorrigidas} normalizados para g/ml, fora de g/ml: {UnidadesForaDoPadrao}. Soma das refeições vs. meta por dia (kcal, desvio de proteína, carboidrato e gordura, e 4P+4C+9G dos macros declarados vs. kcal declaradas): {DesvioPorDia}",
            versaoModelo ?? "não informada", tempoMs, tentativas, uso.Entrada, uso.Resposta, uso.Raciocinio ?? 0, uso.Total,
            normalizacao.Total, normalizacao.Corrigidos, foraDoPadrao, desvios);
    }

    private static decimal DesvioPercentual(decimal valor, decimal meta) =>
        meta == 0 ? 0 : (valor - meta) / meta * 100;

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

        // Os gramas de cada macro vão junto das calorias (regra 13): sem eles, o
        // Gemini montava a própria proporção (no g4, gordura em 31% das calorias
        // contra 25% da meta).
        var metasTexto = string.Join("\n", NomesDias.Select(nome =>
        {
            var meta = metasPorDia[nome].Macros;
            return $"- {nome}: {meta.Calorias:F0} kcal (proteína {meta.ProteinasG:F0} g, carboidrato {meta.CarboidratosG:F0} g, gordura {meta.GordurasG:F0} g)";
        }));

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

            Metas diárias (calorias e macronutrientes), calculadas a partir do gasto em repouso, da atividade
            do dia a dia fora do treino e do gasto real do treino de CADA dia específico, já
            com o déficit ou superávit do objetivo — dias de treino têm meta mais alta que
            dias de descanso:
            {metasTexto}

            Alimentos que a pessoa gosta (use como base do plano, mas NÃO se limite a eles: complete com outros alimentos comuns e adequados ao objetivo, variando as opções ao longo da semana): {(string.IsNullOrEmpty(preferidosTexto) ? "nenhuma preferência informada" : preferidosTexto)}

            Alimentos que a pessoa deve evitar (NUNCA inclua nenhum destes, nem em pequena
            quantidade, nem como ingrediente de outro prato): {(string.IsNullOrEmpty(evitarTexto) ? "nenhuma restrição informada" : evitarTexto)}{rotinaTexto}{orcamentoTexto}

            Regras obrigatórias:
            1. A soma de calorias de cada dia deve ficar dentro de uma margem de 5% (para mais ou para menos) da meta ESPECÍFICA daquele dia, listada acima — cada dia tem uma meta diferente, não use um valor único para todos os 7.
            2. Nunca inclua nenhum alimento da lista de restrição, em nenhuma refeição, em nenhum dia.
            3. Varie a fonte principal de proteína entre os dias da semana — não repita a mesma fonte de proteína em dias consecutivos.
            4. Não repita a mesma refeição (mesmos alimentos) em dois dias seguidos.
            5. Retorne quantidades realistas e mensuráveis para cada alimento. A quantidade é o peso do alimento PRONTO PARA COMER (ex.: arroz já cozido, frango já grelhado), nunca o peso cru antes do preparo.
            6. Use EXATAMENTE os nomes dos dias como escritos acima (ex: "Segunda-feira") no campo diaSemana de cada dia — precisa corresponder exatamente a um dos 7 nomes listados.
            7. Adeque o plano ao hábito alimentar brasileiro: use as refeições típicas (café da manhã, lanche da manhã, almoço, lanche da tarde, jantar e, se fizer sentido, ceia leve) e combinações que um brasileiro realmente consome. Bebidas com cafeína (café, chá preto, chá mate, energéticos) apenas de manhã e no início da tarde, nunca no jantar nem na ceia.
            8. Bebida alcoólica só pode aparecer se estiver entre os alimentos preferidos da pessoa, no máximo uma vez na semana, no sábado ou no domingo, em quantidade moderada (por exemplo, uma lata ou long neck), com as calorias contabilizadas no dia.
            9. Use os nomes regionais dos alimentos abaixo, de acordo com o estado (UF) da pessoa — esta é a referência oficial, não invente outras variações regionais além destas:
               - Feijão: nos estados RS, SC e PR, chame de "feijão preto". Em todos os demais estados, ou se o estado não foi informado, chame apenas de "feijão" (sem especificar o tipo).
               - Mandioca: no estado RJ, chame de "aipim". Nos estados AM, PA, AC, RO, RR, AP, TO, MA, PI, CE, RN, PB, PE, AL, SE e BA, chame de "macaxeira". Em todos os demais estados (SP, MG, ES, PR, SC, RS, MT, MS, GO, DF), ou se o estado não foi informado, chame de "mandioca".
               Para qualquer outro alimento não listado aqui, use o nome mais comum no Brasil, sem tentar adivinhar outras variações regionais.
            10. O campo "unidade" aceita só dois valores: "g" ou "ml" (nunca "gramas", "mililitros", "unidades", "fatias" ou colheres). Bebidas (leite, café, chá, suco, vitamina) em "ml"; todo o resto em "g", inclusive azeite, mel e iogurte. Itens contáveis (ovo, fruta inteira, pão, fatia) também vão em "g", com a contagem entre parênteses no nome — ex.: nome "Ovo de galinha cozido (2 unidades)", quantidade 100, unidade "g"; nome "Pão francês (1 unidade)", quantidade 50, unidade "g". Para um mesmo alimento, use a mesma unidade em todas as refeições e dias da semana.
            11. Preencha o campo "horario" de cada refeição no formato HH:mm (ex: "07:30"). Se a rotina diária da pessoa foi informada, baseie os horários nela; caso contrário, use horários típicos do brasileiro (café da manhã entre 6h30 e 8h, almoço entre 12h e 13h30, jantar entre 19h e 21h).
            12. Se o orçamento semanal informado for "economico", priorize proteínas e ingredientes de menor custo (ex: ovo, frango, peixes populares como tilápia, feijão), evitando itens caros como salmão, camarão ou carnes nobres, sem comprometer a qualidade nutricional. Se for "moderado" ou não informado, use bom senso de custo-benefício. Se for "sem_restricao", não considere custo na escolha dos alimentos.
            13. A soma de proteína, carboidrato e gordura de cada dia deve ficar dentro de uma margem de 10% (para mais ou para menos) dos gramas da meta ESPECÍFICA daquele dia, listados acima. Se não for possível cumprir tudo ao mesmo tempo, priorize nesta ordem: (1) calorias dentro da margem de 5% da regra 1; (2) proteína; (3) carboidrato e gordura.
            14. Nome de cada alimento: genérico, sem marca, em português correto, com a parte, o corte ou a variedade quando isso muda os valores nutricionais (ex.: "Peito de frango sem pele grelhado", "Patinho bovino grelhado", "Arroz branco cozido", "Pão de forma integral", "Leite desnatado", "Maçã fuji", "Banana prata", "Mamão papaia"). Diga SEMPRE o preparo no nome de carnes, aves, peixes, ovos, arroz, feijão, massas, tubérculos e legumes ("cozido", "grelhado", "assado", "refogado", "frito" ou "cru"): escreva "Feijão cozido" (ou "Feijão preto cozido", conforme a regra 9), nunca só "Feijão". Cada item é um alimento só, e preparações com mais de um ingrediente são separadas em itens na mesma refeição: uma salada vira um item por ingrediente (ex.: "Alface crua" e "Tomate cru", nunca "Salada de alface e tomate"); café adoçado vira "Café coado" e "Açúcar" em itens separados; uma vitamina vira a fruta e o leite em itens separados.
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
