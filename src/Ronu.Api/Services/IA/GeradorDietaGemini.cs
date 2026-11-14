using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.Troca;
using Ronu.Api.Validacao;

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
    /// Quantas vezes na semana cada item do "Prefiro evitar" (o "evitar" leve da etapa 3) pode aparecer. Calibração
    /// do projeto, não valor de tabela: "usa pouco, mas pode aparecer" (plano família, D2), e 2 deixa o item
    /// disponível para fechar a meta sem virar presença diária. Vai no prompt e é conferido depois da resposta.
    /// </summary>
    public const int MaximoPorSemanaEvitar = 2;

    /// <summary>
    /// Limite de cada chamada ao Gemini (envio e leitura da resposta). Só os
    /// testes mudam este valor.
    /// </summary>
    public TimeSpan TimeoutPorTentativa { get; init; } = TimeoutPadraoPorTentativa;

    /// <summary>
    /// Se o ajuste de quantidades (fase 4) roda. Só os testes desligam, para
    /// conferir as outras etapas com as quantidades exatas da resposta.
    /// </summary>
    public bool AjustarQuantidades { get; init; } = true;

    /// <summary>
    /// Se o mínimo de refeições por dia (regra 15) é exigido. Só os testes desligam, para conferir as outras
    /// etapas com respostas de poucas refeições.
    /// </summary>
    public bool ExigirMinimoDeRefeicoes { get; init; } = true;

    /// <summary>
    /// Se a fonte de gordura no almoço ou no jantar (regra 16) é exigida. Só os testes desligam, para conferir as
    /// outras etapas com respostas sem gordura.
    /// </summary>
    public bool ExigirFonteDeGordura { get; init; } = true;

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
        // Sem fallback desde a 2c: montar a lista aqui, só com UF e preferências, deixaria passar as alergias.
        var lista = contexto.Lista
            ?? throw new InvalidOperationException("O contexto chegou sem a lista da pessoa: ela vem do ContextoDietaBuilder (ListaDaPessoa).");
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
                var abaixoDoMinimo = ExigirMinimoDeRefeicoes ? DiasAbaixoDoMinimo(dias) : [];
                if (abaixoDoMinimo.Count > 0)
                {
                    if (tentativa < MaximoTentativas)
                    {
                        throw new RespostaIaInvalidaException(
                            $"Dias com menos refeições que o mínimo da meta: {string.Join(", ", abaixoDoMinimo)}.");
                    }
                    _logger.LogWarning("Dieta aceita com menos refeições que o mínimo na última tentativa: {Dias}",
                        string.Join(", ", abaixoDoMinimo));
                }
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
                AplicarLista(dias, lista);
                // "Prefiro evitar" (etapa 3): acima do limite da semana, a primeira resposta é recusada, como as outras
                // regras; na última, as ocorrências excedentes são trocadas depois do cálculo pela base (abaixo).
                var levesAcima = LevesAcimaDoLimite(dias, lista);
                if (levesAcima.Count > 0 && tentativa < MaximoTentativas)
                {
                    // Só os ids da base: o texto que a pessoa escreveu no "evitar" não vai para a exceção nem para o log.
                    throw new RespostaIaInvalidaException(
                        $"Grupos do \"prefiro evitar\" acima de {MaximoPorSemanaEvitar} vezes na semana: {string.Join("; ", levesAcima.Select(g => $"[{string.Join(", ", g.Grupo.Order(StringComparer.Ordinal))}] x{g.Posicoes.Count}"))}.");
                }
                // Regra 7: cafeína à noite recusa a primeira resposta; na última, o item é trocado ou tirado.
                var cafeinaANoite = ValidadorCafeina.Encontrar(dias);
                if (cafeinaANoite.Count > 0)
                {
                    if (tentativa < MaximoTentativas)
                    {
                        throw new RespostaIaInvalidaException($"Bebida com cafeína à noite: {string.Join("; ", cafeinaANoite)}.");
                    }
                    _logger.LogWarning("Cafeína à noite corrigida na última tentativa: {Mudancas}",
                        string.Join("; ", ValidadorCafeina.Corrigir(dias, lista)));
                }
                var semGordura = ExigirFonteDeGordura && ListaTemFonteDeGordura(lista) ? DiasSemFonteDeGordura(dias) : [];
                if (semGordura.Count > 0)
                {
                    if (tentativa < MaximoTentativas)
                    {
                        throw new RespostaIaInvalidaException(
                            $"Dias sem fonte de gordura no almoço e no jantar e com gordura abaixo da meta: {string.Join(", ", semGordura)}.");
                    }
                    _logger.LogWarning("Dieta aceita sem fonte de gordura suficiente na última tentativa: {Dias}",
                        string.Join(", ", semGordura));
                }
                var oleosTirados = TirarOleoDosRefogados(dias);
                // Fase 4: quantidades ajustadas para cada dia chegar perto da meta, sem trocar alimento.
                var ajustes = AjustarQuantidades ? AjusteQuantidades.Ajustar(dias) : [];
                var contaveis = RotularContaveis(dias);
                // Fase 3: kcal e macros calculados pela base no lugar dos declarados, que ficam só no log.
                CalculoPelaBase.Calcular(dias);
                // Na última tentativa: depois do cálculo pela base, porque a troca confere a meta do dia pelos totais
                // calculados (como numa dieta salva).
                if (levesAcima.Count > 0)
                {
                    TrocarLevesExcedentes(dias, lista);
                }

                RegistrarGeracao(dias, declarado, uso, versaoModelo, normalizacao, tentativa, cronometro.ElapsedMilliseconds, lista, contaveis, oleosTirados, ajustes);

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

    /// <summary>
    /// Liga cada alimento da resposta ao item da lista pelo nome (o enum garante
    /// o nome; fora da lista, a resposta é inválida), preenche o AlimentoId e põe a
    /// unidade da base (o Gemini não decide g ou ml).
    /// </summary>
    private static void AplicarLista(List<DiaDietaDto> dias, ListaAlimentos lista)
    {
        var porNome = lista.Itens.ToDictionary(i => i.Nome, StringComparer.Ordinal);

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
        }
    }

    /// <summary>
    /// Nos itens contáveis com quantidade em múltiplo exato do peso da unidade da
    /// POF, acrescenta a contagem ao nome exibido ("Ovo de galinha cozido (2
    /// unidades)"). Depois do ajuste de quantidades (fase 4), que deixa os
    /// contáveis em unidade inteira. Devolve quantos contáveis vieram e quantos
    /// ficaram fora do múltiplo, para o log.
    /// </summary>
    private static (int Contaveis, int ForaDoMultiplo) RotularContaveis(List<DiaDietaDto> dias)
    {
        int contaveis = 0, foraDoMultiplo = 0;

        foreach (var alimento in dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos))
        {
            var item = BaseNutricional.Itens.Single(i => i.Id == alimento.AlimentoId);
            if (item.MedidaUnidade is null)
            {
                continue;
            }

            contaveis++;
            if (RotuloContavel.Contagem(item, alimento.Quantidade) is null)
            {
                foraDoMultiplo++;
            }
            else
            {
                alimento.Nome = RotuloContavel.Nome(alimento.Nome, item, alimento.Quantidade);
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

    internal static readonly string[] Refogados = ["couve-refogada", "espinafre-refogado"];
    internal static readonly string[] OleosDoPreparo = ["azeite-de-oliva-extra-virgem", "oleo-de-soja"];

    private static IEnumerable<string> Preferidos(ContextoDietaDto contexto) =>
        contexto.Preferencias.Where(p => p.Tipo == TiposPreferencia.Preferido).Select(p => p.Alimento);

    // Posição de um item na semana, para achar "as últimas": pelo dia da semana (o Gemini pode devolver os dias fora
    // de ordem), depois a refeição e o item, na ordem da resposta.
    private static (int Dia, int Refeicao, int Item) OrdemNaSemana(List<DiaDietaDto> dias, PosicaoItem p) =>
        (Array.FindIndex(NomesDias, n => string.Equals(n, dias[p.Dia].DiaSemana.Trim(), StringComparison.OrdinalIgnoreCase)), p.Refeicao, p.Item);

    /// <summary>
    /// As posições na semana, na ordem dos dias, dos itens de um grupo leve (os itens de um chip do "Prefiro evitar").
    /// </summary>
    private static List<PosicaoItem> PosicoesDoGrupo(List<DiaDietaDto> dias, IReadOnlySet<string> grupo) => dias
        .SelectMany((dia, d) => dia.Refeicoes.SelectMany((refeicao, r) =>
            refeicao.Alimentos.Select((a, i) => (a.AlimentoId, Posicao: new PosicaoItem(d, r, i)))))
        .Where(x => x.AlimentoId is not null && grupo.Contains(x.AlimentoId))
        .Select(x => x.Posicao)
        .OrderBy(p => OrdemNaSemana(dias, p))
        .ToList();

    /// <summary>
    /// Os grupos leves ("Prefiro evitar", etapa 3; um grupo por chip, ListaAlimentos.GruposLeves) que aparecem mais de
    /// MaximoPorSemanaEvitar vezes na semana, somando os itens do grupo, com as posições de cada vez na ordem da
    /// semana. Por grupo, e não por item, porque um chip como "carne vermelha" casa com muitos cortes: contar por
    /// item deixaria a soma passar muito do limite que a tela promete ("o Ronu usa pouco").
    /// </summary>
    internal static List<GrupoLeveAcima> LevesAcimaDoLimite(List<DiaDietaDto> dias, ListaAlimentos lista) => lista.GruposLeves
        .Select(grupo => new GrupoLeveAcima(grupo, PosicoesDoGrupo(dias, grupo)))
        .Where(g => g.Posicoes.Count > MaximoPorSemanaEvitar)
        .ToList();

    /// <summary>
    /// Na última tentativa, as vezes de um grupo leve acima do limite (as últimas da semana) viram o melhor substituto
    /// da troca: mesmo grupo de troca, da lista da pessoa, sem nenhum item leve, dentro da porção e dos 5% do dia
    /// (CalculadoraTroca). Sem substituto, o item fica como está e o log registra. Depois de CalculoPelaBase, que é
    /// chamado de novo a cada troca para a próxima conferir a meta do dia com os totais atualizados.
    ///
    /// As posições de cada grupo são lidas de novo antes de trocar: um item em dois grupos ("carne vermelha" e
    /// "lombo") pode já ter saído pela troca do grupo anterior. Como o substituto nunca é leve, uma troca não faz
    /// outro grupo passar do limite.
    /// </summary>
    private void TrocarLevesExcedentes(List<DiaDietaDto> dias, ListaAlimentos lista)
    {
        var leves = lista.ItensLeves;
        var trocas = new List<string>();
        var semSubstituto = new List<string>();
        // Uma posição sem substituto num grupo não é tentada de novo no outro grupo do mesmo item (o resultado seria
        // o mesmo, e o log a contaria duas vezes).
        var tentadas = new HashSet<PosicaoItem>();

        foreach (var grupo in lista.GruposLeves)
        {
            foreach (var posicao in PosicoesDoGrupo(dias, grupo).Skip(MaximoPorSemanaEvitar))
            {
                if (!tentadas.Add(posicao))
                {
                    continue;
                }

                var alimentoId = CalculadoraTroca.Encontrar(dias, posicao)!.AlimentoId;
                var rotulo = $"{dias[posicao.Dia].DiaSemana.Trim()} {dias[posicao.Dia].Refeicoes[posicao.Refeicao].Nome}";
                var opcao = CalculadoraTroca.MelhorSubstituto(dias, posicao, lista, leves);
                if (opcao is null)
                {
                    semSubstituto.Add($"'{alimentoId}' ({rotulo})");
                    continue;
                }

                var ocorrencia = opcao.Ocorrencias[0];
                var substituto = BaseNutricional.Itens.Single(i => i.Id == opcao.AlimentoId);
                dias[posicao.Dia].Refeicoes[posicao.Refeicao].Alimentos[posicao.Item] = new AlimentoDto
                {
                    Nome = ocorrencia.Nome,
                    Quantidade = ocorrencia.Quantidade,
                    Unidade = substituto.Medida,
                    AlimentoId = substituto.Id
                };
                CalculoPelaBase.Calcular(dias);
                trocas.Add($"'{alimentoId}' -> '{substituto.Id}' ({rotulo})");
            }
        }

        // Só ids da base e o dia: o texto que a pessoa escreveu no "evitar" não entra no log.
        _logger.LogWarning(
            "Grupos do \"prefiro evitar\" acima de {Maximo} na semana na última tentativa: {Trocadas} trocadas ({Trocas}); {SemSubstituto} sem substituto, ficaram ({FicaramComo})",
            MaximoPorSemanaEvitar, trocas.Count, trocas.Count == 0 ? "nenhuma" : string.Join("; ", trocas),
            semSubstituto.Count, semSubstituto.Count == 0 ? "nenhum" : string.Join("; ", semSubstituto));
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
        List<DiaDietaDto> dias, Dictionary<string, MacrosDto> declaradoPorDia, UsoTokens uso, string? versaoModelo, ResultadoNormalizacao normalizacao, int tentativas, long tempoMs,
        ListaAlimentos lista, (int Contaveis, int ForaDoMultiplo) contaveis, List<OleoTirado> oleosTirados, List<AjusteDoDia> ajustes)
    {
        // Ajuste de quantidades (fase 4): por dia, o desvio de kcal antes e depois e quantos itens mudaram;
        // os dias que ficaram fora dos 5% vão marcados, para acompanhar em produção.
        _logger.LogInformation(
            "Ajuste de quantidades: {DentroDaMargem} de {Dias} dias com kcal a até 5% da meta; por dia (kcal antes -> depois, itens mudados): {PorDia}",
            ajustes.Count(a => a.DentroDaMargem), ajustes.Count,
            string.Join("; ", ajustes.Select(a => string.Create(CultureInfo.InvariantCulture,
                $"{a.Dia} {a.DesvioKcalAntes:+0.0;-0.0}% -> {a.DesvioDepois[0]:+0.0;-0.0}% ({a.ItensMudados}){(a.DentroDaMargem ? "" : " FORA")}"))));

        // Lista fechada (fase 2b): o tamanho, os mínimos, quantos "não como" e quantos "evitar" leves não casaram
        // com nada e quantos itens leves ficaram na lista (só as contagens: o texto da preferência pode revelar
        // uma condição de saúde), os contáveis fora do múltiplo da unidade e quantas vezes o azeite ou óleo saiu
        // de uma refeição com refogado.
        var refogadoComOleo = oleosTirados.Count;
        var naoComoSemCasamento = lista.Preferencias.Count(p => p.Tipo == TiposPreferencia.NaoComo && p.TrechosSemCasamento.Count > 0);
        var levesSemCasamento = lista.Preferencias.Count(p => p.Tipo == TiposPreferencia.Evitar && p.TrechosSemCasamento.Count > 0);
        var minimos = lista.Minimos.Atende
            ? "atende"
            : string.Join(", ", lista.Minimos.Vazios.Select(p => $"{p} vazio").Concat(lista.Minimos.AbaixoDoMinimo.Select(p => $"{p} abaixo")));
        _logger.LogInformation(
            "Lista de alimentos: {Itens} itens, mínimos: {Minimos}, {NaoComoSemCasamento} não como sem casamento, {LevesSemCasamento} evitar leve sem casamento, {ItensLeves} itens leves na lista em {GruposLeves} grupos; contáveis fora do múltiplo da unidade: {ForaDoMultiplo} de {Contaveis}; azeite/óleo tirado de refeição com refogado: {RefogadoComOleo}",
            lista.Itens.Count, minimos, naoComoSemCasamento, levesSemCasamento, lista.ItensLeves.Count, lista.GruposLeves.Count, contaveis.ForaDoMultiplo, contaveis.Contaveis, refogadoComOleo);

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
        // Plano gratuito do Gemini (decisão de 2026-11-02): os termos pedem para não mandar dado pessoal, e o Google
        // usa o conteúdo. Por isso o prompt não leva sexo, idade, peso, altura, objetivo, nome das modalidades nem
        // texto livre: as metas de cada dia já saem calculadas daqui com tudo isso, e o resto vai como dado
        // estruturado (dia de treino ou de descanso, horários, UF, orçamento, itens da base).

        // "Gosto" vai como os itens da lista que ele casou (CasamentoPreferencias): o que não está na base
        // (whey, granola) não pode aparecer, e a tela avisa a pessoa (fase 2b-3).
        var nomePorId = lista.Itens.ToDictionary(i => i.Item.Id, i => i.Nome);
        // Um item que casou com um gosto e também com um "evitar" leve ("frango" e "peito de frango") não vai como
        // base do plano: pedir "use como base" e "use pouco" para o mesmo item confundiria o Gemini.
        var leves = lista.ItensLeves;
        var preferidosTexto = string.Join(", ", lista.Preferencias
            .Where(p => p.Tipo == TiposPreferencia.Preferido)
            .SelectMany(p => p.ItensIds)
            .Where(nomePorId.ContainsKey)
            .Where(id => !leves.Contains(id))
            .Distinct()
            .Select(id => nomePorId[id]));
        var temGosto = contexto.Preferencias.Any(p => p.Tipo == TiposPreferencia.Preferido);

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

        // Do "não como" vão só os trechos que não casaram com a base ("carne de caça"): o que casou já saiu da lista,
        // e o enum do nome não deixa o Gemini escolher fora dela. E vão sem as palavras sobre a pessoa ou a saúde
        // dela: "tenho alergia a gergelim" chega como "gergelim" (TermoParaIA).
        var evitarTexto = TermosSemCasamento(lista, TiposPreferencia.NaoComo);

        // "Prefiro evitar" (etapa 3): os itens que casaram ficam na lista e vão aqui pelo nome da lista, um grupo por
        // chip, com o limite da semana somando o grupo (o mesmo que o C# confere depois, LevesAcimaDoLimite). O grupo
        // vai só com os nomes da lista, nunca com o texto que a pessoa digitou ("carne vermelha" chega como os cortes).
        // Os trechos sem casamento vão como "evite quando possível", pelo TermoParaIA. Sem nenhum dos dois, o
        // parágrafo some: o prompt de quem não usa o nível leve fica igual ao de antes.
        var levesGrupos = lista.GruposLeves
            .Select(grupo => string.Join("; ", lista.Itens.Where(i => grupo.Contains(i.Item.Id)).Select(i => i.Nome)))
            .ToList();
        var levesSemCasamento = TermosSemCasamento(lista, TiposPreferencia.Evitar);
        var levesTexto = (levesGrupos.Count > 0
                ? $"\n\nAlimentos da lista que a pessoa prefere evitar, em grupos (um por linha): use pouco, no máximo {MaximoPorSemanaEvitar} vezes na semana em cada grupo, somando todos os alimentos do grupo, e só quando ajudarem a chegar na meta:{string.Concat(levesGrupos.Select(g => $"\n- {g}"))}"
                : string.Empty)
            + (levesSemCasamento.Length > 0
                ? $"\n\nOutras coisas que a pessoa prefere evitar (evite quando possível, sem proibir): {levesSemCasamento}"
                : string.Empty);

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
        // "Não treino no momento" (zero modalidades): o prompt não presume
        // treino — sem "esportivo", sem marca de dia de treino ou de descanso e
        // sem a frase dos dias de treino e de descanso, que não existem.
        var semTreino = contexto.Modalidades.Count == 0;

        // Cada dia diz se é de déficit, manutenção ou superávit (no lugar do objetivo, g10c) e, para quem treina, se é de
        // treino e de quando a quando, sem o nome da modalidade.
        var marcaObjetivo = MarcaDoObjetivo(contexto.Objetivo);
        var metasTexto = string.Join("\n", NomesDias.Select((nome, i) =>
        {
            var meta = metasPorDia[nome].Macros;
            var linha = $"- {nome}: {meta.Calorias:F0} kcal (proteína {meta.ProteinasG:F0} g, carboidrato {meta.CarboidratosG:F0} g, gordura {meta.GordurasG:F0} g) — {marcaObjetivo}";
            return semTreino ? linha : $"{linha}; {MarcaDoTreino(contexto.Modalidades, i + 1)}";
        }));

        // Contas via Google ou anteriores ao campo podem estar sem estado —
        // nesse caso a linha some (sem deixar linha em branco).
        var estadoTexto = string.IsNullOrEmpty(contexto.Estado)
            ? string.Empty
            : $"\n\nEstado onde a pessoa mora (UF): {contexto.Estado}";

        // Horários estruturados no lugar da rotina em texto. Sem eles, o parágrafo some e os horários seguem o
        // padrão típico brasileiro (regra 11).
        var acordar = RegrasHorario.Formatar(contexto.HorarioAcordar);
        var dormir = RegrasHorario.Formatar(contexto.HorarioDormir);
        var horariosTexto = (acordar, dormir) switch
        {
            (null, null) => string.Empty,
            (not null, null) => $"\n\nHorário em que a pessoa acorda: {acordar}.",
            (null, not null) => $"\n\nHorário em que a pessoa vai dormir: {dormir}.",
            // Dormir mais cedo que acordar (00:30 com acordar às 07:00) é dormir depois da meia-noite: o dia vira.
            _ when contexto.HorarioDormir < contexto.HorarioAcordar
                => $"\n\nHorários da pessoa: acorda às {acordar} e vai dormir às {dormir}, já depois da meia-noite.",
            _ => $"\n\nHorários da pessoa: acorda às {acordar} e vai dormir às {dormir}."
        };
        var temHorarios = horariosTexto.Length > 0 || contexto.Modalidades.Any(m => m.HorarioInicio is not null);
        var regra11 = temHorarios
            ? "Preencha o campo \"horario\" de cada refeição no formato HH:mm (ex: \"07:30\"). Use os horários informados: todas as refeições ficam entre o horário de acordar e o de dormir. Nos dias de treino com horário, nenhuma refeição durante o treino; coloque uma refeição de 1 a 3 horas antes do início e outra até 2 horas depois do fim. O que não foi informado segue os horários típicos do brasileiro (café da manhã entre 6h30 e 8h, almoço entre 12h e 13h30, jantar entre 19h e 21h)."
            : "Preencha o campo \"horario\" de cada refeição no formato HH:mm (ex: \"07:30\"), com horários típicos do brasileiro (café da manhã entre 6h30 e 8h, almoço entre 12h e 13h30, jantar entre 19h e 21h).";

        // Orçamento é opcional — sem ele o parágrafo some e a regra 12 trata
        // como "não informado" (bom senso de custo-benefício).
        var orcamentoTexto = string.IsNullOrEmpty(contexto.OrcamentoSemanal)
            ? string.Empty
            : $"\n\nOrçamento semanal da pessoa para alimentação: {contexto.OrcamentoSemanal}";

        var papel = semTreino ? "nutricionista" : "nutricionista esportivo";
        var metasIntro = semTreino
            ? """
              Metas diárias (calorias e macronutrientes), calculadas a partir do gasto em repouso e da
              atividade do dia a dia, já com o déficit ou superávit do objetivo. A pessoa não treina
              no momento: não há dia de treino nem de descanso, e o plano não deve citar treino,
              pré-treino nem pós-treino. Cada dia diz se é de déficit, de manutenção ou de superávit:
              """
            : """
              Metas diárias (calorias e macronutrientes), calculadas a partir do gasto em repouso, da atividade
              do dia a dia fora do treino e do gasto real do treino de CADA dia específico, já
              com o déficit ou superávit do objetivo — dias de treino têm meta mais alta que
              dias de descanso. Cada dia diz se é de déficit, de manutenção ou de superávit, e se é de treino ou de descanso:
              """;

        return $"""
            Você é um {papel}. Monte um plano alimentar semanal (7 dias) para uma pessoa.{estadoTexto}{horariosTexto}

            {metasIntro}
            {metasTexto}

            Lista de alimentos permitidos. No campo "nome" de cada alimento, use EXATAMENTE um destes nomes, sem acrescentar nada, e nenhum alimento fora desta lista:
            {listaTexto}

            Peso de uma unidade dos itens contáveis: {(string.IsNullOrEmpty(pesosTexto) ? "nenhum item contável na lista" : pesosTexto)}

            Alimentos da lista que a pessoa gosta (use como base do plano, mas NÃO se limite a eles: complete com outros alimentos da lista adequados ao objetivo, variando as opções ao longo da semana): {(string.IsNullOrEmpty(preferidosTexto) ? (temGosto ? "nenhum dos que ela gosta está na lista" : "nenhuma preferência informada") : preferidosTexto)}

            O que a pessoa pediu para evitar já foi tirado da lista. Restrições que não correspondem a um item específico da lista (respeite o sentido delas: NUNCA inclua alimento que se encaixe nelas, nem em pequena quantidade, nem como ingrediente de outro prato): {(string.IsNullOrEmpty(evitarTexto) ? "nenhuma" : evitarTexto)}{levesTexto}{orcamentoTexto}

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
            11. {regra11}
            12. Se o orçamento semanal informado for "economico", priorize proteínas e ingredientes de menor custo (ex: ovo, frango, peixes populares como tilápia, feijão), evitando itens caros como salmão, camarão ou carnes nobres, sem comprometer a qualidade nutricional. Se for "moderado" ou não informado, use bom senso de custo-benefício. Se for "sem_restricao", não considere custo na escolha dos alimentos.
            13. A soma de proteína, carboidrato e gordura de cada dia deve ficar dentro de uma margem de 10% (para mais ou para menos) dos gramas da meta ESPECÍFICA daquele dia, listados acima. Se não for possível cumprir tudo ao mesmo tempo, priorize nesta ordem: (1) calorias dentro da margem de 5% da regra 1; (2) proteína; (3) carboidrato e gordura.
            14. Cada item é um alimento da lista, e preparações com mais de um ingrediente viram itens separados na mesma refeição: uma salada vira um item por ingrediente; café adoçado vira o café e o açúcar; uma vitamina vira a fruta e o leite.{regraRefogado}
            15. Monte pelo menos {RefeicoesMinimas} refeições em cada dia e, nos dias com meta a partir de {MetaComMaisRefeicoes} kcal, pelo menos {RefeicoesMinimasMetaAlta}.{(semTreino ? "" : " O lanche antes e o depois do treino contam como refeições a mais, sem tirar as outras do dia.")}{(ListaTemFonteDeGordura(lista) ? RegraGordura : "")}
            """;
    }

    // Os trechos de um tipo de preferência que não casaram com a base, já sem as palavras sobre a pessoa ou a saúde
    // dela (TermoParaIA), sem repetir.
    private static string TermosSemCasamento(ListaAlimentos lista, string tipo) => string.Join(", ", lista.Preferencias
        .Where(p => p.Tipo == tipo)
        .SelectMany(p => p.TrechosSemCasamento)
        .Select(CasamentoPreferencias.TermoParaIA)
        .Where(t => t.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase));

    // "dia de déficit", "dia de manutenção" ou "dia de superávit": a direção do objetivo, sem dizer o objetivo da
    // pessoa nem o tamanho do déficit (que já está na meta). Objetivo desconhecido conta como manutenção, como na fórmula.
    private static string MarcaDoObjetivo(string objetivo) => objetivo switch
    {
        "perder peso" => "dia de déficit",
        "ganhar peso" => "dia de superávit",
        _ => "dia de manutenção"
    };

    // "treino das 19:30 às 21:00", "treino" (sem hora informada) ou "descanso". Dois treinos no mesmo dia vão em ordem
    // de início.
    private static string MarcaDoTreino(IEnumerable<ModalidadeContextoDto> modalidades, int dia)
    {
        var doDia = modalidades.Where(m => m.DiasSemana.Contains(dia)).ToList();
        if (doDia.Count == 0)
        {
            return "descanso";
        }

        var horarios = doDia
            .Where(m => m.HorarioInicio is not null)
            .OrderBy(m => m.HorarioInicio)
            .Select(m =>
            {
                var inicio = m.HorarioInicio!.Value;
                var fim = inicio.AddMinutes((double)(m.DuracaoHoras * 60));
                return $"das {RegrasHorario.Formatar(inicio)} às {RegrasHorario.Formatar(fim)}";
            })
            .ToList();
        return horarios.Count == 0 ? "treino" : $"treino {string.Join(" e ", horarios)}";
    }

    // Regra 16 (g10d): sem o perfil no prompt, o Gemini passou a montar dias magros demais (no g10c, o perfil F ficou
    // 20,6% abaixo da gordura da meta, sem azeite). A checagem só recusa o caso claro: almoço e jantar sem nenhuma fonte
    // de gordura e o dia abaixo de 85% da gordura da meta (no g10a, que estava bom, isso não aconteceu em nenhum dia).
    private const string RegraGordura = "\n16. Use as fontes de gordura da lista (óleos e gorduras, como o azeite no almoço e no jantar, e oleaginosas e sementes) para chegar à gordura da meta de cada dia: sem elas, o dia fica abaixo da gordura da meta.";
    private const decimal FracaoMinimaDaGordura = 0.85m;

    private static bool EFonteDeGordura(AlimentoBase item) =>
        item.Categoria is CategoriaAlimento.OleosEGorduras or CategoriaAlimento.OleaginosasESementes
        || Refogados.Contains(item.Id);

    private static bool ListaTemFonteDeGordura(ListaAlimentos lista) => lista.Itens.Any(i => EFonteDeGordura(i.Item));

    private static List<string> DiasSemFonteDeGordura(IEnumerable<DiaDietaDto> dias)
    {
        var porId = BaseNutricional.Itens.ToDictionary(i => i.Id);
        AlimentoBase? Item(AlimentoDto a) => a.AlimentoId is not null && porId.TryGetValue(a.AlimentoId, out var i) ? i : null;

        var semGordura = new List<string>();
        foreach (var dia in dias)
        {
            var almocoEJantar = dia.Refeicoes.Where(r =>
                r.Nome.Contains("almo", StringComparison.OrdinalIgnoreCase) || r.Nome.Contains("jant", StringComparison.OrdinalIgnoreCase));
            var temFonte = almocoEJantar.SelectMany(r => r.Alimentos).Any(a => Item(a) is { } i && EFonteDeGordura(i));
            var gordura = dia.Refeicoes.SelectMany(r => r.Alimentos).Sum(a => (Item(a)?.GorduraG ?? 0m) * a.Quantidade / 100m);
            var meta = dia.MetaCalculada.GordurasG;
            if (!temFonte && gordura < meta * FracaoMinimaDaGordura)
            {
                semGordura.Add($"{dia.DiaSemana} ({gordura:F0} de {meta:F0} g)");
            }
        }
        return semGordura;
    }

    // Mínimo de refeições do dia pela meta (regra 15, g10c): com poucas refeições, o Gemini não chegava às metas altas.
    // Na primeira tentativa, um dia abaixo do mínimo recusa a resposta; na última, a dieta é aceita assim (como a
    // cerveja da regra 8, que sai em vez de virar erro): uma refeição a menos não vale um 503 para a pessoa.
    public const int RefeicoesMinimas = 5;
    public const int RefeicoesMinimasMetaAlta = 6;
    public const int MetaComMaisRefeicoes = 3500;

    public static int MinimoDeRefeicoes(decimal metaKcal) =>
        metaKcal >= MetaComMaisRefeicoes ? RefeicoesMinimasMetaAlta : RefeicoesMinimas;

    private static List<string> DiasAbaixoDoMinimo(IEnumerable<DiaDietaDto> dias) => dias
        .Where(d => d.Refeicoes.Count < MinimoDeRefeicoes(d.MetaCalculada.Calorias))
        .Select(d => $"{d.DiaSemana} ({d.Refeicoes.Count} de {MinimoDeRefeicoes(d.MetaCalculada.Calorias)})")
        .ToList();

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

/// <summary>
/// Um grupo leve (os itens de um chip do "Prefiro evitar") que passou do limite da semana, com as posições de cada
/// vez em que um item dele aparece, na ordem da semana.
/// </summary>
internal record GrupoLeveAcima(IReadOnlySet<string> Grupo, IReadOnlyList<PosicaoItem> Posicoes);
