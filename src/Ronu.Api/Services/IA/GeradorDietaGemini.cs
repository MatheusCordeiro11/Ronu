using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Ronu.Api.Data;
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
        var fatorAtividade = CalculadoraManutencao.FatorDoNivel(contexto.NivelAtividade);
        var ajuste = _calculadoraAjuste.Calcular(
            contexto.HistoricoPeso, contexto.HistoricoMetas, contexto.Objetivo, metaBaseMedia, manutencaoPorDia,
            DateTime.UtcNow, fatorAtividade);
        RegistrarAjusteAdaptativo(ajuste, metaBaseMedia);
        var metasPorDia = MontarMetasPorDia(caloriasBasePorDia, ajuste.Percentual, contexto.Peso, contexto.Altura);
        var composicaoPorDia = MontarComposicaoPorDia(contexto, _calculadora, ajuste.Percentual, metasPorDia);

        // Lista fechada da fase 2b: vai no prompt e no enum do schema, então o Gemini só escolhe itens
        // da base (com valor por 100 g, para a fase 3 calcular de verdade).
        var lista = contexto.Lista ?? ListaAlimentosPessoa.Montar(contexto.Estado, contexto.Preferencias);
        var prompt = MontarPrompt(contexto, metasPorDia, lista);
        var schema = MontarSchema(lista);

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
                var dias = MontarDias(respostaIa, metasPorDia, composicaoPorDia);
                // O que o Gemini declarou para cada dia (soma das refeições), antes de qualquer remoção e do
                // cálculo pela base: só para o log comparar com o calculado.
                var declarado = dias.ToDictionary(d => d.DiaSemana.Trim(), d => d.TotalDoDia, StringComparer.OrdinalIgnoreCase);
                // Normaliza antes da regra 8: tirar a cerveja (última tentativa)
                // precisa da quantidade em g/ml para descontar as calorias.
                var normalizacao = NormalizadorUnidades.Normalizar(dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos));
                var bebidasRemovidas = tentativa < MaximoTentativas
                    ? ValidarBebidaAlcoolica(dias, contexto)
                    : TirarBebidaAlcoolica(dias, contexto);
                // Depois da regra 8: a rede de segurança da cerveja continua valendo antes da lista.
                var contaveis = AplicarLista(dias, lista);
                var oleosTirados = TirarOleoDosRefogados(dias);
                // Fase 3: kcal e macros calculados pela base no lugar dos declarados, que ficam só no log.
                CalculoPelaBase.Calcular(dias);

                RegistrarGeracao(dias, declarado, uso, versaoModelo, normalizacao, tentativa, cronometro.ElapsedMilliseconds, lista, contaveis, oleosTirados);

                return new DietaSemanalDto
                {
                    Dias = dias,
                    AjusteAdaptativo = ajuste,
                    VersaoBase = BaseNutricional.VersaoBase,
                    AvisoListaPobre = AvisoListaPobre(lista.Minimos),
                    ManutencaoPorDia = manutencaoPorDia,
                    FatorAtividade = fatorAtividade,
                    BebidasRemovidas = bebidasRemovidas
                };
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
    private static List<DiaDietaDto> MontarDias(
        RespostaDiasIaDto respostaIa, Dictionary<string, ResultadoMacros> metasPorDia, Dictionary<string, ComposicaoMetaDto> composicaoPorDia)
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
                MetaElevadaPeloPiso = meta.MetaElevadaPeloPiso,
                ComposicaoMeta = composicaoPorDia[nome]
            };
        }).ToList();
    }

    // Regra 8 do prompt: bebida alcoólica só se a pessoa a marcou como
    // preferida. Fora disso, na primeira tentativa a resposta é inválida (nova
    // tentativa); na última, a cerveja sai da dieta em vez do 503.
    private static List<BebidaRemovida> ValidarBebidaAlcoolica(List<DiaDietaDto> dias, ContextoDietaDto contexto)
    {
        var bebida = ValidadorBebidaAlcoolica.EncontrarNaoPermitida(
            dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), Preferidos(contexto));

        if (bebida is not null)
        {
            throw new RespostaIaInvalidaException(
                $"Resposta do Gemini com bebida alcoólica fora das preferências da pessoa: '{bebida}'.");
        }

        return [];
    }

    private static List<BebidaRemovida> TirarBebidaAlcoolica(List<DiaDietaDto> dias, ContextoDietaDto contexto)
    {
        // Só tira o item: as kcal da refeição e do dia saem do cálculo pela base, depois.
        return ValidadorBebidaAlcoolica.TirarNaoPermitidas(dias, Preferidos(contexto));
    }

    // Folga para considerar a quantidade um múltiplo exato do peso da unidade (arredondamento do Gemini).
    private const decimal ToleranciaMultiploG = 0.5m;

    /// <summary>
    /// Liga cada alimento da resposta ao item da lista pelo nome (o enum garante
    /// o nome; fora da lista, a resposta é inválida), preenche o AlimentoId, põe a
    /// unidade da base (o Gemini não decide g ou ml) e, nos itens contáveis com
    /// quantidade em múltiplo exato do peso da unidade da POF, acrescenta a
    /// contagem ao nome exibido ("Ovo de galinha cozido (2 unidades)"). Devolve
    /// quantos contáveis vieram e quantos ficaram fora do múltiplo, para o log.
    /// </summary>
    private static (int Contaveis, int ForaDoMultiplo) AplicarLista(List<DiaDietaDto> dias, ListaAlimentos lista)
    {
        var porNome = lista.Itens.ToDictionary(i => i.Nome, StringComparer.Ordinal);
        int contaveis = 0, foraDoMultiplo = 0;

        foreach (var alimento in dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos))
        {
            var nome = alimento.Nome.Trim();
            if (!porNome.TryGetValue(nome, out var item))
            {
                throw new RespostaIaInvalidaException($"Resposta do Gemini com alimento fora da lista: '{alimento.Nome}'.");
            }

            alimento.Nome = nome;
            alimento.AlimentoId = item.Item.Id;
            alimento.Unidade = item.Item.Medida;

            var unidade = item.Item.MedidaUnidade;
            if (unidade is null)
            {
                continue;
            }

            contaveis++;
            var quantas = Math.Round(alimento.Quantidade / unidade.Gramas);
            if (quantas >= 1 && Math.Abs(alimento.Quantidade - quantas * unidade.Gramas) <= ToleranciaMultiploG)
            {
                alimento.Nome = $"{nome} ({quantas:0} {(quantas == 1 ? unidade.Nome : unidade.NomePlural)})";
            }
            else
            {
                foraDoMultiplo++;
            }
        }

        return (contaveis, foraDoMultiplo);
    }

    /// <summary>
    /// Couve e espinafre refogados da TACO já incluem o óleo do preparo: o azeite
    /// ou óleo na mesma refeição contaria a gordura duas vezes. O prompt pede para
    /// não pôr, mas no g9a (2026-10-25) 2 de 7 gerações puseram mesmo assim. Aqui
    /// o C# tira esse azeite ou óleo da refeição; as kcal saem do cálculo pela
    /// base, depois (as daqui, pela TACO 260 e 272, são só para o log). Depois de
    /// AplicarLista, que preenche o AlimentoId usado aqui.
    /// </summary>
    private List<OleoTirado> TirarOleoDosRefogados(List<DiaDietaDto> dias)
    {
        var tirados = new List<OleoTirado>();
        foreach (var dia in dias)
        {
            foreach (var refeicao in dia.Refeicoes.Where(r => r.Alimentos.Any(a => Refogados.Contains(a.AlimentoId))))
            {
                foreach (var oleo in refeicao.Alimentos.Where(a => OleosDoPreparo.Contains(a.AlimentoId)).ToList())
                {
                    var item = BaseNutricional.Itens.Single(i => i.Id == oleo.AlimentoId);
                    var kcal = item.Kcal * oleo.Quantidade / 100m;
                    refeicao.Alimentos.Remove(oleo);
                    tirados.Add(new OleoTirado(dia.DiaSemana, refeicao.Nome, oleo.Nome, oleo.Quantidade, Math.Round(kcal, 1)));
                }
            }
        }

        foreach (var t in tirados)
        {
            _logger.LogWarning(
                "Azeite ou óleo tirado de refeição com refogado (o óleo do preparo já está no refogado): '{Alimento}' ({Quantidade} g, {KcalDescontadas} kcal pela base), {Dia} {Refeicao}",
                t.Alimento, t.Quantidade, t.KcalDescontadas, t.Dia, t.Refeicao);
        }

        return tirados;
    }

    private record OleoTirado(string Dia, string Refeicao, string Alimento, decimal Quantidade, decimal KcalDescontadas);

    private static AvisoListaPobreDto? AvisoListaPobre(ResultadoMinimos minimos) => minimos.AbaixoDoMinimo.Count == 0
        ? null
        : new AvisoListaPobreDto
        {
            Papeis = minimos.AbaixoDoMinimo.Select(p => p.ToString()).ToList(),
            ItensPorPapel = minimos.ItensPorPapel.ToDictionary(p => p.Key.ToString(), p => p.Value),
            GruposDeProteina = minimos.GruposDeProteina
        };

    private static readonly string[] Refogados = ["couve-refogada", "espinafre-refogado"];
    private static readonly string[] OleosDoPreparo = ["azeite-de-oliva-extra-virgem", "oleo-de-soja"];

    private static IEnumerable<string> Preferidos(ContextoDietaDto contexto) =>
        contexto.Preferencias.Where(p => p.Tipo == "preferido").Select(p => p.Alimento);

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
        List<DiaDietaDto> dias, Dictionary<string, MacrosDto> declaradoPorDia, UsoTokens uso, string? versaoModelo, ResultadoNormalizacao normalizacao, int tentativas, long tempoMs,
        ListaAlimentos lista, (int Contaveis, int ForaDoMultiplo) contaveis, List<OleoTirado> oleosTirados)
    {
        // Lista fechada (fase 2b): o tamanho, os mínimos, quantos "evitar" não casaram com nada (só a
        // contagem: o texto da preferência pode revelar uma condição de saúde), os contáveis fora do
        // múltiplo da unidade e quantas vezes o azeite ou óleo saiu de uma refeição com refogado.
        var refogadoComOleo = oleosTirados.Count;
        var evitarSemCasamento = lista.Preferencias.Count(p => p.Tipo == "evitar" && p.TrechosSemCasamento.Count > 0);
        var minimos = lista.Minimos.Atende
            ? "atende"
            : string.Join(", ", lista.Minimos.Vazios.Select(p => $"{p} vazio").Concat(lista.Minimos.AbaixoDoMinimo.Select(p => $"{p} abaixo")));
        _logger.LogInformation(
            "Lista de alimentos: {Itens} itens, mínimos: {Minimos}, {EvitarSemCasamento} evitar sem casamento; contáveis fora do múltiplo da unidade: {ForaDoMultiplo} de {Contaveis}; azeite/óleo tirado de refeição com refogado: {RefogadoComOleo}",
            lista.Itens.Count, minimos, evitarSemCasamento, contaveis.ForaDoMultiplo, contaveis.Contaveis, refogadoComOleo);

        // Por dia: o calculado pela base contra a meta (kcal e cada macro) e o que o Gemini declarou contra
        // o calculado. O declarado não vai mais para a dieta (fase 3), mas continua medido aqui: no g9a ele
        // diferia do cardápio real em 12,9% por refeição.
        var desvios = string.Join("; ", dias.Select(d =>
        {
            var calculado = d.TotalDoDia;
            var meta = d.MetaCalculada;
            var declarado = declaradoPorDia[d.DiaSemana.Trim()];
            return string.Create(CultureInfo.InvariantCulture,
                $"{d.DiaSemana.Trim()} {calculado.Calorias:F0}/{meta.Calorias:F0} kcal ({DesvioPercentual(calculado.Calorias, meta.Calorias):+0.0;-0.0}%) " +
                $"P {DesvioPercentual(calculado.ProteinasG, meta.ProteinasG):+0;-0}% C {DesvioPercentual(calculado.CarboidratosG, meta.CarboidratosG):+0;-0}% " +
                $"G {DesvioPercentual(calculado.GordurasG, meta.GordurasG):+0;-0}% [IA declarou {declarado.Calorias:F0} kcal, {DesvioPercentual(declarado.Calorias, calculado.Calorias):+0.0;-0.0}% do calculado]");
        }));

        var foraDoPadrao = normalizacao.ForaDoPadrao.Count == 0
            ? "nenhuma"
            : string.Join(", ", normalizacao.ForaDoPadrao.Select(u => $"\"{u.Key}\" x{u.Value}"));

        _logger.LogInformation(
            "Dieta gerada pelo Gemini ({VersaoModelo}) em {TempoMs} ms ({Tentativas} tentativa(s)); tokens: entrada {TokensEntrada}, resposta {TokensResposta}, raciocínio {TokensRaciocinio}, total {TokensTotal}. Unidades: {Alimentos} alimentos, {UnidadesCorrigidas} normalizados para g/ml, fora de g/ml: {UnidadesForaDoPadrao}. Calculado pela base vs. meta por dia (kcal, desvio de proteína, carboidrato e gordura, e o declarado pela IA vs. o calculado): {DesvioPorDia}",
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

    // Partes da meta de cada dia, pelo nome do dia, a partir das mesmas
    // parcelas da fórmula que deram a meta (CalculadoraManutencao).
    private static Dictionary<string, ComposicaoMetaDto> MontarComposicaoPorDia(
        ContextoDietaDto contexto, ICalculadoraGastoCalorico calculadora, decimal percentualAjuste,
        Dictionary<string, ResultadoMacros> metasPorDia) =>
        CalculadoraManutencao.ComposicaoPorDia(contexto, calculadora).ToDictionary(
            c => NomesDias[c.DiaSemana - 1],
            c => ComposicaoMeta.Montar(c, percentualAjuste, metasPorDia[NomesDias[c.DiaSemana - 1]].Macros.Calorias),
            StringComparer.OrdinalIgnoreCase);

    // Rótulos dos grupos na lista do prompt, na ordem em que aparecem.
    private static readonly (GrupoDeTroca Grupo, string Rotulo)[] GruposNoPrompt =
    [
        (GrupoDeTroca.CarnesEPeixes, "Carnes, aves e peixes"),
        (GrupoDeTroca.Ovos, "Ovos"),
        (GrupoDeTroca.Leguminosas, "Leguminosas"),
        (GrupoDeTroca.LeiteEIogurte, "Leite e iogurte"),
        (GrupoDeTroca.Queijos, "Queijos"),
        (GrupoDeTroca.Carboidratos, "Cereais, pães, massas e tubérculos"),
        (GrupoDeTroca.Frutas, "Frutas"),
        (GrupoDeTroca.Hortalicas, "Hortaliças"),
        (GrupoDeTroca.OleaginosasESementes, "Oleaginosas e sementes"),
        (GrupoDeTroca.OleosEGorduras, "Óleos e gorduras"),
        (GrupoDeTroca.Adocantes, "Açúcares e mel"),
        (GrupoDeTroca.CafesEChas, "Cafés e chás"),
        (GrupoDeTroca.SucosERefrigerantes, "Sucos, refrigerantes e água de coco"),
        (GrupoDeTroca.BebidasAlcoolicas, "Bebidas alcoólicas"),
    ];

    private static readonly string[] UfsFeijaoPreto = ["RS", "SC", "PR"];

    private static string MontarPrompt(ContextoDietaDto contexto, Dictionary<string, ResultadoMacros> metasPorDia, ListaAlimentos lista)
    {
        var modalidadesTexto = string.Join(", ", contexto.Modalidades
            .Select(m => $"{m.Nome} ({string.Join(", ", m.DiasSemana.OrderBy(d => d).Select(d => NomesDias[d - 1]))})"));

        // "Gosto" vai como os itens da lista que ele casou (CasamentoPreferencias): o que não está na base
        // (whey, granola) não pode aparecer, e a tela avisa a pessoa (fase 2b-3).
        var nomePorId = lista.Itens.ToDictionary(i => i.Item.Id, i => i.Nome);
        var preferidosTexto = string.Join(", ", lista.Preferencias
            .Where(p => p.Tipo == "preferido")
            .SelectMany(p => p.ItensIds)
            .Where(nomePorId.ContainsKey)
            .Distinct()
            .Select(id => nomePorId[id]));
        var temGosto = contexto.Preferencias.Any(p => p.Tipo == "preferido");

        var listaTexto = string.Join("\n", GruposNoPrompt
            .Select(g => (g.Rotulo, Nomes: lista.Itens.Where(i => i.Item.Grupo == g.Grupo).Select(i => i.Nome).ToList()))
            .Where(g => g.Nomes.Count > 0)
            .Select(g => $"- {g.Rotulo}: {string.Join("; ", g.Nomes)}"));

        // Peso de uma unidade dos contáveis (POF/IBGE, BaseNutricionalMedidas), para o Gemini usar
        // múltiplos e o C# poder exibir a contagem ("(2 unidades)").
        var pesosTexto = string.Join("; ", lista.Itens
            .Where(i => i.Item.MedidaUnidade is not null)
            .Select(i => $"{i.Nome}: 1 {i.Item.MedidaUnidade!.Nome} = {i.Item.MedidaUnidade.Gramas:0} g"));

        var regra9 = UfsFeijaoPreto.Contains(contexto.Estado?.Trim().ToUpperInvariant())
            ? "Os nomes da lista já seguem o costume da região da pessoa. Nesta região, o feijão mais comum é o preto: prefira \"Feijão preto cozido\" a \"Feijão cozido\"."
            : "Os nomes da lista já seguem o costume da região da pessoa: use-os como estão, sem trocar por variações regionais.";

        // Couve e espinafre refogados ficam na lista (não há versão sem óleo comum), mas a TACO já inclui o
        // óleo do refogado: azeite na mesma refeição contaria a gordura duas vezes (fase 2b, 2026-10-25).
        var refogadosNaLista = lista.Itens.Where(i => Refogados.Contains(i.Item.Id)).Select(i => $"\"{i.Nome}\"").ToList();
        var regraRefogado = refogadosNaLista.Count == 0
            ? string.Empty
            : $" {string.Join(" e ", refogadosNaLista)} já incluem o óleo do preparo: na refeição em que um deles aparecer, não coloque azeite nem óleo.";

        var evitarTexto = string.Join(", ", contexto.Preferencias
            .Where(p => p.Tipo == "evitar")
            .Select(p => p.Alimento));

        // Regra 8 só explica quando a bebida pode aparecer para quem marcou uma
        // como preferida. Para os outros, a versão que explicava (com o exemplo
        // "uma lata ou long neck") ia no prompt de todo mundo e, no g8b
        // (2026-10-25), 11 de 16 chamadas de perfis sem bebida nas preferências
        // vieram com cerveja: agora eles recebem só a proibição.
        var regra8 = ValidadorBebidaAlcoolica.PreferidosTemBebida(Preferidos(contexto))
            ? "Bebida alcoólica só pode aparecer se a mesma bebida estiver entre os alimentos preferidos da pessoa, no máximo uma vez na semana, no sábado ou no domingo, em quantidade moderada (por exemplo, uma lata ou long neck), com as calorias contabilizadas no dia."
            : "Não inclua nenhuma bebida alcoólica (cerveja, chope, vinho, destilados ou drinques), em nenhuma refeição, em nenhum dia, nem em pequena quantidade.";

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

        // "Não treino no momento" (zero modalidades): o prompt não presume
        // treino — sem "esportivo", sem lista vazia de modalidades e sem a
        // frase dos dias de treino e de descanso, que não existem.
        var semTreino = contexto.Modalidades.Count == 0;
        var papel = semTreino ? "nutricionista" : "nutricionista esportivo";
        var treinoTexto = semTreino
            ? "- Treino: não treina no momento (nenhuma modalidade cadastrada)"
            : $"- Modalidades praticadas (com os dias da semana de cada uma): {modalidadesTexto}";
        var metasIntro = semTreino
            ? """
              Metas diárias (calorias e macronutrientes), calculadas a partir do gasto em repouso e da
              atividade do dia a dia, já com o déficit ou superávit do objetivo. A pessoa não treina
              no momento: não há dia de treino nem de descanso, e o plano não deve citar treino,
              pré-treino nem pós-treino:
              """
            : """
              Metas diárias (calorias e macronutrientes), calculadas a partir do gasto em repouso, da atividade
              do dia a dia fora do treino e do gasto real do treino de CADA dia específico, já
              com o déficit ou superávit do objetivo — dias de treino têm meta mais alta que
              dias de descanso:
              """;

        return $"""
            Você é um {papel}. Monte um plano alimentar semanal (7 dias)
            para uma pessoa com o seguinte perfil:

            - Sexo: {contexto.Sexo}
            - Idade: {contexto.Idade} anos
            - Peso: {contexto.Peso} kg
            - Altura: {contexto.Altura} cm
            - Objetivo: {contexto.Objetivo}
            {treinoTexto}{estadoTexto}

            {metasIntro}
            {metasTexto}

            Lista de alimentos permitidos. No campo "nome" de cada alimento, use EXATAMENTE um destes nomes, sem acrescentar nada, e nenhum alimento fora desta lista:
            {listaTexto}

            Peso de uma unidade dos itens contáveis: {(string.IsNullOrEmpty(pesosTexto) ? "nenhum item contável na lista" : pesosTexto)}

            Alimentos da lista que a pessoa gosta (use como base do plano, mas NÃO se limite a eles: complete com outros alimentos da lista adequados ao objetivo, variando as opções ao longo da semana): {(string.IsNullOrEmpty(preferidosTexto) ? (temGosto ? "nenhum dos que ela gosta está na lista" : "nenhuma preferência informada") : preferidosTexto)}

            Alimentos que a pessoa deve evitar (já foram tirados da lista; NUNCA inclua nenhum destes, nem em pequena
            quantidade, nem como ingrediente de outro prato): {(string.IsNullOrEmpty(evitarTexto) ? "nenhuma restrição informada" : evitarTexto)}{rotinaTexto}{orcamentoTexto}

            Regras obrigatórias:
            1. A soma de calorias de cada dia deve ficar dentro de uma margem de 5% (para mais ou para menos) da meta ESPECÍFICA daquele dia, listada acima — cada dia tem uma meta diferente, não use um valor único para todos os 7.
            2. Nunca inclua nenhum alimento da lista de restrição, em nenhuma refeição, em nenhum dia.
            3. Varie a fonte principal de proteína entre os dias da semana — não repita a mesma fonte de proteína em dias consecutivos.
            4. Não repita a mesma refeição (mesmos alimentos) em dois dias seguidos.
            5. Retorne quantidades realistas e mensuráveis para cada alimento. A quantidade é o peso do alimento PRONTO PARA COMER (ex.: arroz já cozido, frango já grelhado), nunca o peso cru antes do preparo.
            6. Use EXATAMENTE os nomes dos dias como escritos acima (ex: "Segunda-feira") no campo diaSemana de cada dia — precisa corresponder exatamente a um dos 7 nomes listados.
            7. Adeque o plano ao hábito alimentar brasileiro: use as refeições típicas (café da manhã, lanche da manhã, almoço, lanche da tarde, jantar e, se fizer sentido, ceia leve) e combinações que um brasileiro realmente consome. Bebidas com cafeína (café, chá preto, chá mate, energéticos) apenas de manhã e no início da tarde, nunca no jantar nem na ceia.
            8. {regra8}
            9. {regra9}
            10. A quantidade é sempre em gramas ("g") ou, para as bebidas (leite, café, chá, suco, refrigerante, água de coco), em mililitros ("ml"), nunca em unidades, fatias ou colheres. Para os itens contáveis, use múltiplos exatos do peso de uma unidade listado acima (ex.: duas unidades de um item de 50 g = 100 g).
            11. Preencha o campo "horario" de cada refeição no formato HH:mm (ex: "07:30"). Se a rotina diária da pessoa foi informada, baseie os horários nela; caso contrário, use horários típicos do brasileiro (café da manhã entre 6h30 e 8h, almoço entre 12h e 13h30, jantar entre 19h e 21h).
            12. Se o orçamento semanal informado for "economico", priorize proteínas e ingredientes de menor custo (ex: ovo, frango, peixes populares como tilápia, feijão), evitando itens caros como salmão, camarão ou carnes nobres, sem comprometer a qualidade nutricional. Se for "moderado" ou não informado, use bom senso de custo-benefício. Se for "sem_restricao", não considere custo na escolha dos alimentos.
            13. A soma de proteína, carboidrato e gordura de cada dia deve ficar dentro de uma margem de 10% (para mais ou para menos) dos gramas da meta ESPECÍFICA daquele dia, listados acima. Se não for possível cumprir tudo ao mesmo tempo, priorize nesta ordem: (1) calorias dentro da margem de 5% da regra 1; (2) proteína; (3) carboidrato e gordura.
            14. Cada item é um alimento da lista, e preparações com mais de um ingrediente viram itens separados na mesma refeição: uma salada vira um item por ingrediente; café adoçado vira o café e o açúcar; uma vitamina vira a fruta e o leite.{regraRefogado}
            """;
    }

    // O enum do nome é a lista da pessoa (no máximo BaseNutricional.TetoDeNomes nomes: a fase 0, em
    // 2026-10-07, passou com até 180 em gemini-3.5-flash-lite). A unidade também vai em enum, mas o C#
    // a sobrescreve pela da base (AplicarLista).
    private static object MontarSchema(ListaAlimentos lista)
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
                nome = new { type = "STRING", format = "enum", @enum = lista.Itens.Select(i => i.Nome).ToArray() },
                quantidade = new { type = "NUMBER" },
                unidade = new { type = "STRING", format = "enum", @enum = new[] { NormalizadorUnidades.Gramas, NormalizadorUnidades.Mililitros } }
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
