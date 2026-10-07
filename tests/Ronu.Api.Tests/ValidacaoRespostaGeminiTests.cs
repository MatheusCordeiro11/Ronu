using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Validação da resposta do Gemini no GeradorDietaGemini: semana completa
/// (7 dias únicos, com os nomes esperados), resposta bloqueada, cortada ou com
/// JSON inválido, ou sem resposta no limite de cada tentativa -> uma nova
/// tentativa e depois RespostaIaInvalidaException (que o DietasController
/// transforma no 503 amigável), modelo fixo, e TotalDoDia recalculado a partir
/// das refeições. Gemini simulado, sem rede.
/// </summary>
public class ValidacaoRespostaGeminiTests
{
    private static readonly string[] Semana =
    {
        "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira",
        "Sexta-feira", "Sábado", "Domingo"
    };

    // ---------- Semana incompleta ou com dias errados ----------

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public async Task Quantidade_De_Dias_Diferente_De_7_Tenta_De_Novo_E_Falha(int quantidade)
    {
        var nomes = Enumerable.Range(0, quantidade).Select(i => Semana[i % 7]);
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(nomes)), Envelope(TextoDieta(nomes)));

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarDietaAsync(Contexto()));

        Assert.Contains($"{quantidade} dias", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Dia_Repetido_Tenta_De_Novo_E_Falha()
    {
        // 7 dias, mas Segunda-feira duas vezes e nenhum Domingo.
        var nomes = Semana.Take(6).Append("Segunda-feira");
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(nomes)), Envelope(TextoDieta(nomes)));

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarDietaAsync(Contexto()));

        Assert.Contains("repetido", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Nome_De_Dia_Desconhecido_Tenta_De_Novo_E_Falha()
    {
        var nomes = Semana.Take(6).Append("Domingo de manhã");
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(nomes)), Envelope(TextoDieta(nomes)));

        await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarDietaAsync(Contexto()));

        Assert.Equal(2, gemini.Chamadas);
    }

    // ---------- Resposta cortada, bloqueada ou com JSON inválido ----------

    [Fact]
    public async Task Resposta_Cortada_Tenta_De_Novo_E_Falha()
    {
        // MAX_TOKENS: o texto vem pela metade (JSON incompleto).
        var texto = TextoDieta(Semana);
        var cortada = Envelope(texto[..(texto.Length / 2)], finishReason: "MAX_TOKENS");
        var gemini = new GeminiRoteirizado(cortada, cortada);

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarDietaAsync(Contexto()));

        Assert.Contains("MAX_TOKENS", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Resposta_Sem_Candidates_Tenta_De_Novo_E_Falha()
    {
        // Prompt bloqueado: sem candidates, motivo em promptFeedback.
        var bloqueada = JsonSerializer.Serialize(new { promptFeedback = new { blockReason = "SAFETY" } });
        var gemini = new GeminiRoteirizado(bloqueada, bloqueada);

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarDietaAsync(Contexto()));

        Assert.Contains("SAFETY", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Theory]
    [InlineData("isto não é JSON")]
    [InlineData("{\"dias\": [")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Json_Invalido_Tenta_De_Novo_E_Falha(string texto)
    {
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));

        await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarDietaAsync(Contexto()));

        Assert.Equal(2, gemini.Chamadas);
    }

    // ---------- Nova tentativa e caminho feliz ----------

    [Fact]
    public async Task Nova_Tentativa_Recupera_Quando_A_Segunda_Resposta_Vem_Valida()
    {
        var texto = TextoDieta(Semana);
        var gemini = new GeminiRoteirizado(
            Envelope(texto[..(texto.Length / 2)], finishReason: "MAX_TOKENS"),
            Envelope(texto));

        var dieta = await NovoGerador(gemini).GerarDietaAsync(Contexto());

        Assert.Equal(Semana, dieta.Dias.Select(d => d.DiaSemana));
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Resposta_Valida_Usa_Uma_Chamada_So()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));

        var dieta = await NovoGerador(gemini).GerarDietaAsync(Contexto());

        Assert.Equal(7, dieta.Dias.Count);
        Assert.Equal(1, gemini.Chamadas);
    }

    [Fact]
    public async Task Chamada_Usa_O_Modelo_Fixo_E_Nao_O_Alias_Latest()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));

        await NovoGerador(gemini).GerarDietaAsync(Contexto());

        Assert.Equal("gemini-3.5-flash-lite", GeradorDietaGemini.Modelo);
        // Pior caso: cold start (~45 s) + 2 × 60 s = ~165 s, abaixo dos 230 s do Azure.
        Assert.Equal(TimeSpan.FromSeconds(60), GeradorDietaGemini.TimeoutPadraoPorTentativa);
        Assert.Contains("/models/gemini-3.5-flash-lite:generateContent", gemini.UltimaUri!.AbsoluteUri);
        Assert.DoesNotContain("latest", gemini.UltimaUri.AbsoluteUri);
    }

    // ---------- Timeout por tentativa ----------

    [Fact]
    public async Task Timeout_Na_Primeira_Tentativa_Recupera_Na_Segunda()
    {
        var gemini = new GeminiRoteirizado(GeminiRoteirizado.SemResposta, Envelope(TextoDieta(Semana)));

        var dieta = await NovoGerador(gemini, TimeoutCurto).GerarDietaAsync(Contexto());

        Assert.Equal(7, dieta.Dias.Count);
        Assert.Equal(2, gemini.Chamadas);
    }

    // Dois timeouts viram RespostaIaInvalidaException, que o DietasController
    // transforma no 503 amigável (o mesmo caminho das respostas inválidas).
    [Fact]
    public async Task Dois_Timeouts_Viram_Resposta_Invalida()
    {
        var gemini = new GeminiRoteirizado(GeminiRoteirizado.SemResposta, GeminiRoteirizado.SemResposta);

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini, TimeoutCurto).GerarDietaAsync(Contexto()));

        Assert.Contains("não respondeu", ex.Message);
        Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);
        Assert.Equal(2, gemini.Chamadas);
    }

    // Timeout e resposta inválida dividem as mesmas 2 tentativas: nunca uma
    // terceira chamada (o pior caso de tempo conta com no máximo 2).
    [Fact]
    public async Task Timeout_E_Resposta_Invalida_Dividem_As_Duas_Tentativas()
    {
        var texto = TextoDieta(Semana);
        var gemini = new GeminiRoteirizado(
            GeminiRoteirizado.SemResposta,
            Envelope(texto[..(texto.Length / 2)], finishReason: "MAX_TOKENS"));

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini, TimeoutCurto).GerarDietaAsync(Contexto()));

        Assert.Contains("MAX_TOKENS", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    // Falha de rede/HTTP não é "resposta inválida": sobe na hora, sem nova
    // tentativa (o controller já a trata como 503).
    [Fact]
    public async Task Erro_Http_Nao_Tem_Nova_Tentativa()
    {
        var gemini = new GeminiRoteirizado(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<HttpRequestException>(() => NovoGerador(gemini).GerarDietaAsync(Contexto()));

        Assert.Equal(1, gemini.Chamadas);
    }

    // ---------- TotalDoDia ----------

    [Fact]
    public async Task TotalDoDia_E_A_Soma_Das_Refeicoes_E_Nao_O_Declarado()
    {
        // Cada dia: refeições de 500 kcal (30/60/15 g) e 735 kcal (40/90/25 g),
        // mas a IA declara 999 kcal e macros zerados no total do dia.
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true)));

        var dieta = await NovoGerador(gemini).GerarDietaAsync(Contexto());

        foreach (var dia in dieta.Dias)
        {
            Assert.Equal(1235m, dia.TotalDoDia.Calorias);
            Assert.Equal(70m, dia.TotalDoDia.ProteinasG);
            Assert.Equal(150m, dia.TotalDoDia.CarboidratosG);
            Assert.Equal(40m, dia.TotalDoDia.GordurasG);
        }
    }

    // ---------- Unidades ----------

    [Fact]
    public async Task Unidades_Da_Resposta_Saem_Normalizadas_E_A_Fora_Do_Padrao_Nao_Rejeita()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, unidade: "gramas")));

        var dieta = await NovoGerador(gemini).GerarDietaAsync(Contexto());

        Assert.All(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a => Assert.Equal("g", a.Unidade));

        var comUnidades = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, unidade: "unidades")));

        var dietaComUnidades = await NovoGerador(comUnidades).GerarDietaAsync(Contexto());

        Assert.All(dietaComUnidades.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a => Assert.Equal("unidades", a.Unidade));
        Assert.Equal(1, comUnidades.Chamadas);
    }

    // ---------- Bebida alcoólica (regra 8) ----------

    [Fact]
    public async Task Bebida_Alcoolica_Fora_Das_Preferencias_Tenta_De_Novo_E_Falha()
    {
        var texto = TextoDieta(Semana, comRefeicoes: true, alimento: "Cereais cerveja lata (Cerveja pilsen)");
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarDietaAsync(Contexto()));

        Assert.Contains("bebida alcoólica", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Bebida_Alcoolica_Na_Segunda_Tentativa_Some_E_Recupera()
    {
        var gemini = new GeminiRoteirizado(
            Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: "Cerveja pilsen")),
            Envelope(TextoDieta(Semana, comRefeicoes: true)));

        var dieta = await NovoGerador(gemini).GerarDietaAsync(Contexto());

        Assert.Equal(7, dieta.Dias.Count);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Bebida_Alcoolica_Preferida_Passa_Na_Primeira()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: "Cerveja pilsen")));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Cerveja", Tipo = "preferido" });

        var dieta = await NovoGerador(gemini).GerarDietaAsync(contexto);

        Assert.Equal(7, dieta.Dias.Count);
        Assert.Equal(1, gemini.Chamadas);
    }

    [Fact]
    public async Task Bebida_Alcoolica_Marcada_Para_Evitar_Nao_Libera()
    {
        var texto = TextoDieta(Semana, comRefeicoes: true, alimento: "Cerveja pilsen");
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Cerveja", Tipo = "evitar" });

        await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarDietaAsync(contexto));

        Assert.Equal(2, gemini.Chamadas);
    }

    // ---------- Apoio ----------

    // Limite curto para os testes de timeout não esperarem os 60 s reais.
    private static readonly TimeSpan TimeoutCurto = TimeSpan.FromMilliseconds(200);

    private static GeradorDietaGemini NovoGerador(HttpMessageHandler gemini, TimeSpan? timeoutPorTentativa = null) => new(
        new HttpClient(gemini), new CalculadoraGastoCalorico(),
        new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
        new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance)
    {
        TimeoutPorTentativa = timeoutPorTentativa ?? GeradorDietaGemini.TimeoutPadraoPorTentativa
    };

    private static ContextoDietaDto Contexto() => new()
    {
        Altura = 175, Sexo = "Masculino", Idade = 30, Peso = 80, Objetivo = "manter peso",
        Modalidades = new(), Preferencias = new(), HistoricoPeso = new()
    };

    // O JSON da dieta que a IA põe em candidates[0].content.parts[0].text.
    private static string TextoDieta(IEnumerable<string> nomes, bool comRefeicoes = false, string unidade = "g", string alimento = "Arroz")
    {
        object Refeicao(string nome, decimal kcal, decimal p, decimal c, decimal g) => new
        {
            nome,
            alimentos = new[] { new { nome = alimento, quantidade = 100, unidade } },
            macros = new { calorias = kcal, proteinasG = p, carboidratosG = c, gordurasG = g },
            horario = "12:00"
        };

        var dias = nomes.Select(n => new
        {
            diaSemana = n,
            refeicoes = comRefeicoes
                ? new[] { Refeicao("Almoço", 500, 30, 60, 15), Refeicao("Jantar", 735, 40, 90, 25) }
                : Array.Empty<object>(),
            totalDoDia = new { calorias = 999, proteinasG = 0, carboidratosG = 0, gordurasG = 0 }
        });

        return JsonSerializer.Serialize(new { dias });
    }

    // O envelope da API do Gemini, com finishReason e usageMetadata.
    private static string Envelope(string texto, string finishReason = "STOP") => JsonSerializer.Serialize(new
    {
        candidates = new[]
        {
            new { content = new { parts = new[] { new { text = texto } } }, finishReason }
        },
        usageMetadata = new { promptTokenCount = 1500, candidatesTokenCount = 4000, totalTokenCount = 5500 }
    });

    // Responde cada chamada com o próximo corpo da fila (status 200) e conta
    // as chamadas. Com um status de erro, responde sempre com ele. SemResposta
    // na fila simula o Gemini travado: a chamada só termina quando é cancelada.
    private sealed class GeminiRoteirizado : HttpMessageHandler
    {
        public const string SemResposta = "<sem resposta>";

        private readonly Queue<string> _respostas;
        private readonly HttpStatusCode _status = HttpStatusCode.OK;

        public int Chamadas { get; private set; }
        public Uri? UltimaUri { get; private set; }

        public GeminiRoteirizado(params string[] respostas) => _respostas = new Queue<string>(respostas);

        public GeminiRoteirizado(HttpStatusCode status)
        {
            _respostas = new Queue<string>();
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            UltimaUri = request.RequestUri;
            var corpo = _status == HttpStatusCode.OK ? _respostas.Dequeue() : "{}";

            if (corpo == SemResposta)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            };
        }
    }
}
