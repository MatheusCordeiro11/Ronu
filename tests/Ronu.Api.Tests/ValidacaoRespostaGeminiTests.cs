using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Data;
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

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

        Assert.Contains($"{quantidade} dias", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Dia_Repetido_Tenta_De_Novo_E_Falha()
    {
        // 7 dias, mas Segunda-feira duas vezes e nenhum Domingo.
        var nomes = Semana.Take(6).Append("Segunda-feira");
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(nomes)), Envelope(TextoDieta(nomes)));

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

        Assert.Contains("repetido", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Nome_De_Dia_Desconhecido_Tenta_De_Novo_E_Falha()
    {
        var nomes = Semana.Take(6).Append("Domingo de manhã");
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(nomes)), Envelope(TextoDieta(nomes)));

        await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

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

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

        Assert.Contains("MAX_TOKENS", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Resposta_Sem_Candidates_Tenta_De_Novo_E_Falha()
    {
        // Prompt bloqueado: sem candidates, motivo em promptFeedback.
        var bloqueada = JsonSerializer.Serialize(new { promptFeedback = new { blockReason = "SAFETY" } });
        var gemini = new GeminiRoteirizado(bloqueada, bloqueada);

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

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

        await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

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

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(Semana, dieta.Dias.Select(d => d.DiaSemana));
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Resposta_Valida_Usa_Uma_Chamada_So()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(7, dieta.Dias.Count);
        Assert.Equal(1, gemini.Chamadas);
    }

    [Fact]
    public async Task Chamada_Usa_O_Modelo_Fixo_E_Nao_O_Alias_Latest()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));

        await NovoGerador(gemini).GerarComListaAsync(Contexto());

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

        var dieta = await NovoGerador(gemini, TimeoutCurto).GerarComListaAsync(Contexto());

        Assert.Equal(7, dieta.Dias.Count);
        Assert.Equal(2, gemini.Chamadas);
    }

    // Dois timeouts viram RespostaIaInvalidaException, que o DietasController
    // transforma no 503 amigável (o mesmo caminho das respostas inválidas).
    [Fact]
    public async Task Dois_Timeouts_Viram_Resposta_Invalida()
    {
        var gemini = new GeminiRoteirizado(GeminiRoteirizado.SemResposta, GeminiRoteirizado.SemResposta);

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini, TimeoutCurto).GerarComListaAsync(Contexto()));

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

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini, TimeoutCurto).GerarComListaAsync(Contexto()));

        Assert.Contains("MAX_TOKENS", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    // Falha de rede/HTTP não é "resposta inválida": sobe na hora, sem nova
    // tentativa (o controller já a trata como 503).
    [Fact]
    public async Task Erro_Http_Nao_Tem_Nova_Tentativa()
    {
        var gemini = new GeminiRoteirizado(HttpStatusCode.ServiceUnavailable);

        await Assert.ThrowsAsync<HttpRequestException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

        Assert.Equal(1, gemini.Chamadas);
    }

    // ---------- TotalDoDia ----------

    // Fase 3: a IA declara refeições de 500 e 735 kcal e 999 kcal no dia, mas os números vêm da base:
    // 100 g de arroz branco cozido (TACO 3: 128 kcal, 2,5 g P, 28,1 g C, 0,2 g G) em cada refeição.
    [Fact]
    public async Task Kcal_E_Macros_Vem_Da_Base_E_Nao_Do_Declarado()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        foreach (var dia in dieta.Dias)
        {
            Assert.All(dia.Refeicoes, r =>
            {
                Assert.Equal(128m, r.Macros.Calorias);
                Assert.Equal(128m, r.Alimentos.Single().Macros!.Calorias);
            });
            Assert.Equal(256m, dia.TotalDoDia.Calorias);
            Assert.Equal(5m, dia.TotalDoDia.ProteinasG);
            Assert.Equal(56.2m, dia.TotalDoDia.CarboidratosG);
            Assert.Equal(0.4m, dia.TotalDoDia.GordurasG);
        }
    }

    // ---------- Unidades ----------

    // Desde a lista fechada (2b-2), a unidade vem da base, qualquer que seja a da resposta.
    [Theory]
    [InlineData("gramas")]
    [InlineData("unidades")]
    [InlineData("ml")]
    public async Task Unidade_Vem_Da_Base_E_Nao_Da_Resposta(string unidade)
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, unidade: unidade)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.All(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a => Assert.Equal("g", a.Unidade));
        Assert.Equal(1, gemini.Chamadas);
    }

    // ---------- Lista fechada (fase 2b-2) ----------

    [Fact]
    public async Task Alimento_Fora_Da_Lista_Tenta_De_Novo_E_Falha()
    {
        var texto = TextoDieta(Semana, comRefeicoes: true, alimento: "Arroz");
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

        Assert.Contains("fora da lista", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Alimento_Da_Lista_Ganha_AlimentoId_E_A_Dieta_A_VersaoBase()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.All(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a => Assert.Equal("arroz-branco-cozido", a.AlimentoId));
        Assert.Equal(Ronu.Api.Data.BaseNutricional.VersaoBase, dieta.VersaoBase);
        Assert.Null(dieta.AvisoListaPobre);
    }

    // Peso da unidade do ovo cozido na POF: 45 g. Múltiplo exato ganha a contagem; o resto fica em gramas.
    [Theory]
    [InlineData(45, "Ovo de galinha cozido (1 unidade)")]
    [InlineData(90, "Ovo de galinha cozido (2 unidades)")]
    [InlineData(90.4, "Ovo de galinha cozido (2 unidades)")]
    [InlineData(100, "Ovo de galinha cozido")]
    [InlineData(20, "Ovo de galinha cozido")]
    public async Task Contavel_Em_Multiplo_Da_Unidade_Ganha_A_Contagem(decimal gramas, string nomeExibido)
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: "Ovo de galinha cozido", quantidade: gramas)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.All(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a =>
        {
            Assert.Equal(nomeExibido, a.Nome);
            Assert.Equal("ovo-de-galinha-cozido", a.AlimentoId);
        });
    }

    [Fact]
    public async Task Fatia_Usa_O_Nome_Da_Medida_Da_Pof()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: "Pão de forma integral", quantidade: 50)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.All(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a => Assert.Equal("Pão de forma integral (2 fatias)", a.Nome));
    }

    // Couve e espinafre refogados já incluem o óleo do preparo (TACO): o azeite ou óleo da mesma refeição
    // sai, e o jantar fica só com os 100 g do refogado (couve: TACO 116, 90 kcal e 6,6 g G; espinafre: TACO
    // 120, 67 kcal e 5,4 g G).
    [Theory]
    [InlineData("Couve refogada", "Azeite de oliva extra virgem", 90, 6.6)]
    [InlineData("Espinafre refogado", "Óleo de soja", 67, 5.4)]
    public async Task Oleo_Sai_Da_Refeicao_Com_Refogado(string refogado, string oleo, decimal kcal, decimal gordura)
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: refogado,
            bebidaNoJantar: oleo, unidadeBebida: "g", quantidadeBebida: 12)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        foreach (var dia in dieta.Dias)
        {
            var jantar = dia.Refeicoes.Single(r => r.Nome == "Jantar");
            Assert.Equal(new[] { refogado }, jantar.Alimentos.Select(a => a.Nome));
            Assert.Equal(kcal, jantar.Macros.Calorias);
            Assert.Equal(gordura, jantar.Macros.GordurasG);
            Assert.Equal(2 * kcal, dia.TotalDoDia.Calorias);
        }
        Assert.Equal(1, gemini.Chamadas);
    }

    // Selênio (LimiteCastanhaDoPara): 30 g de castanha-do-pará no jantar dos 7 dias viram 8 g nos 2 primeiros e
    // saem dos outros; o jantar fica com o arroz. A ceia só com castanha sai do dia a partir do 3º dia.
    [Fact]
    public async Task Castanha_Do_Para_Fica_Em_8g_E_Em_2_Dias()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true,
            bebidaNoJantar: "Castanha-do-pará", unidadeBebida: "g", quantidadeBebida: 30, ceiaSoComBebida: "Castanha-do-pará")));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        decimal Castanha(DiaDietaDto d) => d.Refeicoes.SelectMany(r => r.Alimentos).Where(a => a.AlimentoId == "castanha-do-para").Sum(a => a.Quantidade);
        Assert.Equal(new[] { 8m, 8m, 0m, 0m, 0m, 0m, 0m }, dieta.Dias.Select(Castanha));
        Assert.All(dieta.Dias.Skip(2), d =>
        {
            Assert.Equal(new[] { "Almoço", "Jantar" }, d.Refeicoes.Select(r => r.Nome));
            Assert.Equal(new[] { "arroz-branco-cozido" }, d.Refeicoes.Single(r => r.Nome == "Jantar").Alimentos.Select(a => a.AlimentoId));
        });
        Assert.Equal(1, gemini.Chamadas);
    }

    [Fact]
    public async Task Azeite_Em_Refeicao_Sem_Refogado_Fica()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: "Abobrinha cozida",
            bebidaNoJantar: "Azeite de oliva extra virgem", unidadeBebida: "g", quantidadeBebida: 12)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        // Abobrinha cozida (TACO 73: 15 kcal por 100 g) nas duas refeições + 12 g de azeite (TACO 260:
        // 884 kcal por 100 g = 106,08 kcal), que fica.
        Assert.All(dieta.Dias, d =>
        {
            Assert.Contains(d.Refeicoes.Single(r => r.Nome == "Jantar").Alimentos, a => a.AlimentoId == "azeite-de-oliva-extra-virgem");
            Assert.Equal(15m + 15m + 106.08m, d.TotalDoDia.Calorias);
        });
    }

    [Fact]
    public async Task Nome_Regional_Da_Uf_Tambem_Liga_Ao_Item()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: "Aipim cozido")));
        var contexto = Contexto();
        contexto.Estado = "RJ";

        var dieta = await NovoGerador(gemini).GerarComListaAsync(contexto);

        Assert.All(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a => Assert.Equal("mandioca-cozida", a.AlimentoId));
    }

    [Fact]
    public async Task Lista_Abaixo_Do_Minimo_Gera_Com_Aviso_Gravado()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true)));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "vegano", Tipo = "nao_como" });

        var dieta = await NovoGerador(gemini).GerarComListaAsync(contexto);

        Assert.NotNull(dieta.AvisoListaPobre);
        Assert.Equal(["Proteina"], dieta.AvisoListaPobre.Papeis);
        Assert.Equal(4, dieta.AvisoListaPobre.ItensPorPapel["Proteina"]);
        Assert.Equal(1, dieta.AvisoListaPobre.GruposDeProteina);
        // Vai no JSON salvo (o dashboard mostra na 2b-3).
        Assert.Contains("\"AvisoListaPobre\"", JsonSerializer.Serialize(dieta));
    }

    // Dietas salvas antes da lista fechada abrem com AlimentoId, VersaoBase e o aviso nulos.
    [Fact]
    public void Dieta_Antiga_Abre_Com_Os_Campos_Novos_Nulos()
    {
        const string antiga = """
            {"Dias":[{"DiaSemana":"Segunda-feira","Refeicoes":[{"Nome":"Almoço","Alimentos":[{"Nome":"Feijão cozido","Quantidade":150,"Unidade":"g"}],
            "Macros":{"Calorias":500,"ProteinasG":30,"CarboidratosG":60,"GordurasG":15},"Horario":"12:00"}],
            "TotalDoDia":{"Calorias":500,"ProteinasG":30,"CarboidratosG":60,"GordurasG":15},
            "MetaCalculada":{"Calorias":2000,"ProteinasG":140,"CarboidratosG":250,"GordurasG":60}}]}
            """;

        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(antiga)!;

        Assert.Null(dieta.VersaoBase);
        Assert.Null(dieta.AvisoListaPobre);
        Assert.Null(dieta.Dias[0].Refeicoes[0].Alimentos[0].AlimentoId);
        Assert.Equal("Feijão cozido", dieta.Dias[0].Refeicoes[0].Alimentos[0].Nome);
    }

    [Fact]
    public async Task Schema_Leva_A_Lista_No_Enum_Do_Nome()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "peixe", Tipo = "nao_como" });

        await NovoGerador(gemini).GerarComListaAsync(contexto);

        var alimento = JsonDocument.Parse(gemini.UltimoCorpo!).RootElement.GetProperty("generationConfig").GetProperty("responseSchema")
            .GetProperty("properties").GetProperty("dias").GetProperty("items").GetProperty("properties").GetProperty("refeicoes")
            .GetProperty("items").GetProperty("properties").GetProperty("alimentos").GetProperty("items").GetProperty("properties");
        var nomes = alimento.GetProperty("nome").GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToList();
        var lista = ListaAlimentosPessoa.Montar(null, contexto.Preferencias, Alergenos.Nenhum);

        Assert.Equal(lista.Itens.Select(i => i.Nome), nomes);
        Assert.DoesNotContain("Salmão grelhado", nomes);
        Assert.Equal(["g", "ml"], alimento.GetProperty("unidade").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Prompt_Leva_A_Lista_Os_Pesos_E_So_O_Gosto_Que_Esta_Na_Base()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Whey protein", Tipo = "preferido" });
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Batata doce", Tipo = "preferido" });

        await NovoGerador(gemini).GerarComListaAsync(contexto);

        var prompt = PromptEnviado(gemini);
        Assert.Contains("- Carnes, aves e peixes: Patinho bovino grelhado;", prompt);
        Assert.Contains("Ovo de galinha cozido: 1 unidade = 45 g", prompt);
        Assert.Contains("Pão de forma integral: 1 fatia = 25 g", prompt);
        Assert.Contains("a pessoa gosta (use como base do plano, mas NÃO se limite a eles: complete com outros alimentos da lista adequados ao objetivo, variando as opções ao longo da semana): Batata doce cozida", prompt);
        Assert.DoesNotContain("Whey", prompt);
        Assert.Contains("Couve refogada\" e \"Espinafre refogado\" já incluem o óleo do preparo", prompt);
    }

    [Theory]
    [InlineData("RS", true)]
    [InlineData("sc", true)]
    [InlineData("SP", false)]
    public async Task Prompt_Prefere_Feijao_Preto_No_Sul(string uf, bool fala)
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));
        var contexto = Contexto();
        contexto.Estado = uf;

        await NovoGerador(gemini).GerarComListaAsync(contexto);

        Assert.Equal(fala, PromptEnviado(gemini).Contains("prefira \"Feijão preto cozido\""));
    }

    // ---------- Bebida alcoólica (regra 8) ----------

    // Toda refeição só com cerveja: tirar a cerveja deixaria os dias vazios, então continua 503.
    [Fact]
    public async Task Bebida_Alcoolica_Que_Esvazia_O_Dia_Tenta_De_Novo_E_Falha()
    {
        var texto = TextoDieta(Semana, comRefeicoes: true, alimento: "Cereais cerveja lata (Cerveja pilsen)");
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

        Assert.Contains("sem nenhuma refeição", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    // Na última tentativa, a cerveja sai da refeição em vez do 503. As kcal dela vão
    // para o log pela base (350 ml × TACO 474, 41 kcal por 100 ml = 143,5 kcal), e o
    // jantar fica com o que sobrou, calculado pela base (100 g de arroz = 128 kcal).
    [Fact]
    public async Task Cerveja_Na_Ultima_Tentativa_Sai_Da_Refeicao_E_Desconta_Pela_Base()
    {
        var texto = TextoDieta(Semana, comRefeicoes: true, bebidaNoJantar: "Cerveja pilsen (1 lata)");
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(2, gemini.Chamadas);
        Assert.Equal(7, dieta.BebidasRemovidas.Count);
        Assert.All(dieta.BebidasRemovidas, b =>
        {
            Assert.Equal("Cerveja pilsen (1 lata)", b.Alimento);
            Assert.Equal("Jantar", b.Refeicao);
            Assert.Equal(143.5m, b.KcalDescontadas);
        });
        foreach (var dia in dieta.Dias)
        {
            var jantar = dia.Refeicoes.Single(r => r.Nome == "Jantar");
            Assert.Equal(new[] { "Arroz branco cozido" }, jantar.Alimentos.Select(a => a.Nome));
            Assert.Equal(128m, jantar.Macros.Calorias);
            Assert.Equal(2.5m, jantar.Macros.ProteinasG);
            Assert.Equal(28.1m, jantar.Macros.CarboidratosG);
            // TotalDoDia continua sendo a soma das refeições, já sem a cerveja.
            Assert.Equal(256m, dia.TotalDoDia.Calorias);
        }
    }

    [Fact]
    public async Task Refeicao_So_Com_Cerveja_Sai_Do_Dia()
    {
        var texto = TextoDieta(Semana, comRefeicoes: true, ceiaSoComBebida: "Cerveja pilsen");
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.All(dieta.Dias, d =>
        {
            Assert.Equal(new[] { "Almoço", "Jantar" }, d.Refeicoes.Select(r => r.Nome));
            Assert.Equal(256m, d.TotalDoDia.Calorias);
        });
    }

    // Desde a fase 3, tirar a cerveja só tira o item (as kcal vêm do cálculo pela base, depois) e informa
    // as kcal dela para o log: 700 ml × 41 kcal por 100 ml = 287 kcal.
    [Fact]
    public void Tirar_A_Cerveja_So_Remove_O_Item_E_Informa_As_Kcal()
    {
        var declarado = new MacrosDto { Calorias = 100, ProteinasG = 1, CarboidratosG = 10, GordurasG = 0 };
        var refeicao = new RefeicaoDto
        {
            Nome = "Jantar",
            Alimentos = new() { new AlimentoDto { Nome = "Arroz", Quantidade = 100, Unidade = "g" }, new AlimentoDto { Nome = "Cerveja", Quantidade = 700, Unidade = "ml" } },
            Macros = declarado
        };
        var dias = new List<DiaDietaDto> { new() { DiaSemana = "Sábado", Refeicoes = new() { refeicao }, TotalDoDia = declarado, MetaCalculada = declarado } };

        var removidas = ValidadorBebidaAlcoolica.TirarNaoPermitidas(dias, Array.Empty<string>());

        Assert.Equal(287m, Assert.Single(removidas).KcalDescontadas);
        Assert.Equal(new[] { "Arroz" }, refeicao.Alimentos.Select(a => a.Nome));
        Assert.Equal(100m, refeicao.Macros.Calorias);
    }

    // Só a cerveja tem valor na base para descontar: outra bebida, ou cerveja numa
    // unidade que não seja g/ml, continua virando 503.
    [Theory]
    [InlineData("Vinho tinto seco", "ml")]
    [InlineData("Cerveja pilsen", "lata")]
    public async Task Bebida_Sem_Valor_Para_Descontar_Continua_503(string bebida, string unidade)
    {
        var texto = TextoDieta(Semana, comRefeicoes: true, bebidaNoJantar: bebida, unidadeBebida: unidade);
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));

        var ex = await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(Contexto()));

        Assert.Contains("sem valor na base", ex.Message);
        Assert.Equal(2, gemini.Chamadas);
    }

    // Quem marcou cerveja como preferida continua podendo recebê-la, mesmo na última tentativa.
    [Fact]
    public async Task Cerveja_Preferida_Nao_E_Tirada_Na_Ultima_Tentativa()
    {
        var gemini = new GeminiRoteirizado(
            Envelope("isto não é JSON"),
            Envelope(TextoDieta(Semana, comRefeicoes: true, bebidaNoJantar: "Cerveja pilsen")));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Cerveja", Tipo = "preferido" });

        var dieta = await NovoGerador(gemini).GerarComListaAsync(contexto);

        Assert.Empty(dieta.BebidasRemovidas);
        Assert.All(dieta.Dias, d => Assert.Contains(d.Refeicoes.Single(r => r.Nome == "Jantar").Alimentos, a => a.Nome == "Cerveja pilsen"));
    }

    // Regra 8 condicional: sem bebida nas preferências, o prompt só proíbe (sem o
    // exemplo "uma lata ou long neck"); com bebida, explica quando ela pode aparecer.
    [Fact]
    public async Task Prompt_Sem_Bebida_Nas_Preferencias_So_Proibe_Alcool()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Banana", Tipo = "preferido" });
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Cerveja", Tipo = "nao_como" });

        await NovoGerador(gemini).GerarComListaAsync(contexto);

        var prompt = PromptEnviado(gemini);
        Assert.Contains("8. Não inclua nenhuma bebida alcoólica", prompt);
        Assert.DoesNotContain("long neck", prompt);
    }

    [Fact]
    public async Task Prompt_Com_Bebida_Nas_Preferencias_Explica_Quando_Pode()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana)));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Cerveja", Tipo = "preferido" });

        await NovoGerador(gemini).GerarComListaAsync(contexto);

        var prompt = PromptEnviado(gemini);
        Assert.Contains("8. Bebida alcoólica só pode aparecer se a mesma bebida estiver entre os alimentos preferidos", prompt);
        Assert.DoesNotContain("Não inclua nenhuma bebida alcoólica", prompt);
    }

    private static string PromptEnviado(GeminiRoteirizado gemini) =>
        JsonDocument.Parse(gemini.UltimoCorpo!).RootElement
            .GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;

    [Fact]
    public async Task Bebida_Alcoolica_Na_Segunda_Tentativa_Some_E_Recupera()
    {
        var gemini = new GeminiRoteirizado(
            Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: "Cerveja pilsen")),
            Envelope(TextoDieta(Semana, comRefeicoes: true)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(7, dieta.Dias.Count);
        Assert.Equal(2, gemini.Chamadas);
    }

    [Fact]
    public async Task Bebida_Alcoolica_Preferida_Passa_Na_Primeira()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, alimento: "Cerveja pilsen")));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Cerveja", Tipo = "preferido" });

        var dieta = await NovoGerador(gemini).GerarComListaAsync(contexto);

        Assert.Equal(7, dieta.Dias.Count);
        Assert.Equal(1, gemini.Chamadas);
    }

    [Fact]
    public async Task Bebida_Alcoolica_Marcada_Para_Evitar_Nao_Libera()
    {
        var texto = TextoDieta(Semana, comRefeicoes: true, alimento: "Cerveja pilsen");
        var gemini = new GeminiRoteirizado(Envelope(texto), Envelope(texto));
        var contexto = Contexto();
        contexto.Preferencias.Add(new PreferenciaContextoDto { Alimento = "Cerveja", Tipo = "nao_como" });

        await Assert.ThrowsAsync<RespostaIaInvalidaException>(() => NovoGerador(gemini).GerarComListaAsync(contexto));

        Assert.Equal(2, gemini.Chamadas);
    }

    // ---------- Apoio ----------

    // Limite curto para os testes de timeout não esperarem os 60 s reais.
    private static readonly TimeSpan TimeoutCurto = TimeSpan.FromMilliseconds(200);

    // O ajuste de quantidades (fase 4) e o mínimo de refeições (regra 15) ficam desligados aqui: estes testes
    // conferem as outras etapas com as quantidades exatas de respostas de poucas refeições. O ajuste tem os próprios
    // testes (AjusteQuantidadesTests), e o mínimo, os da seção "Mínimo de refeições" deste arquivo.
    private static GeradorDietaGemini NovoGerador(HttpMessageHandler gemini, TimeSpan? timeoutPorTentativa = null, bool ajustar = false) => new(
        new HttpClient(gemini), new CalculadoraGastoCalorico(),
        new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
        new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance)
    {
        TimeoutPorTentativa = timeoutPorTentativa ?? GeradorDietaGemini.TimeoutPadraoPorTentativa,
        AjustarQuantidades = ajustar,
        ExigirMinimoDeRefeicoes = false,
        ExigirFonteDeGordura = false
    };

    // Com o ajuste ligado (como em produção): o cardápio de teste (só arroz, 256 kcal no dia) está muito
    // abaixo da meta, então o arroz sobe até o teto de 150% (150 g por refeição), sem passar dele.
    [Fact]
    public async Task Com_O_Ajuste_Ligado_As_Quantidades_Mudam_Dentro_Dos_Limites()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true)));

        var dieta = await NovoGerador(gemini, ajustar: true).GerarComListaAsync(Contexto());

        Assert.All(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a => Assert.Equal(150m, a.Quantidade));
        Assert.All(dieta.Dias, d => Assert.Equal(2 * 192m, d.TotalDoDia.Calorias));
    }

    // ---------- Mínimo de refeições (regra 15, g10c) ----------

    [Theory]
    [InlineData(1500, 5)]
    [InlineData(3499.9, 5)]
    [InlineData(3500, 6)]
    [InlineData(4200, 6)]
    public void Minimo_De_Refeicoes_Pela_Meta(double meta, int minimo) =>
        Assert.Equal(minimo, GeradorDietaGemini.MinimoDeRefeicoes((decimal)meta));

    // O perfil de teste tem meta de ~2450 kcal (sem treino): mínimo 5. Duas refeições recusam a primeira resposta, e a segunda,
    // com cinco, é a usada.
    [Fact]
    public async Task Menos_Refeicoes_Que_O_Minimo_Pede_De_Novo()
    {
        var gemini = new GeminiRoteirizado(
            Envelope(TextoDieta(Semana, comRefeicoes: true)),
            Envelope(TextoDieta(Semana, comRefeicoes: true, lanchesExtras: 3)));

        var dieta = await GeradorComMinimo(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(2, gemini.Chamadas);
        Assert.All(dieta.Dias, d => Assert.Equal(5, d.Refeicoes.Count));
    }

    // Na última tentativa a dieta é aceita mesmo abaixo do mínimo: uma refeição a menos não vira 503.
    [Fact]
    public async Task Na_Ultima_Tentativa_Aceita_Abaixo_Do_Minimo()
    {
        var gemini = new GeminiRoteirizado(
            Envelope(TextoDieta(Semana, comRefeicoes: true)),
            Envelope(TextoDieta(Semana, comRefeicoes: true, lanchesExtras: 1)));

        var dieta = await GeradorComMinimo(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(2, gemini.Chamadas);
        Assert.All(dieta.Dias, d => Assert.Equal(3, d.Refeicoes.Count));
    }

    [Fact]
    public async Task No_Minimo_Passa_Na_Primeira()
    {
        var gemini = new GeminiRoteirizado(Envelope(TextoDieta(Semana, comRefeicoes: true, lanchesExtras: 3)));

        await GeradorComMinimo(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(1, gemini.Chamadas);
    }

    // ---------- Cafeína à noite (regra 7, ValidadorCafeina) ----------

    [Fact]
    public async Task Cafeina_No_Jantar_Pede_De_Novo()
    {
        var gemini = new GeminiRoteirizado(
            Envelope(TextoDieta(Semana, comRefeicoes: true, bebidaNoJantar: "Chá mate", quantidadeBebida: 200)),
            Envelope(TextoDieta(Semana, comRefeicoes: true)));

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(2, gemini.Chamadas);
        Assert.DoesNotContain(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), a => a.AlimentoId == "cha-mate");
    }

    // Na última tentativa, sem 503: o chá mate do jantar vira chá de erva-doce, na mesma quantidade.
    [Fact]
    public async Task Na_Ultima_Tentativa_A_Cafeina_A_Noite_E_Trocada()
    {
        var comMate = Envelope(TextoDieta(Semana, comRefeicoes: true, bebidaNoJantar: "Chá mate", quantidadeBebida: 200));
        var gemini = new GeminiRoteirizado(comMate, comMate);

        var dieta = await NovoGerador(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(2, gemini.Chamadas);
        Assert.All(dieta.Dias, d =>
        {
            var jantar = d.Refeicoes.Single(r => r.Nome == "Jantar");
            Assert.DoesNotContain(jantar.Alimentos, a => a.AlimentoId == "cha-mate");
            Assert.Contains(jantar.Alimentos, a => a.AlimentoId == ValidadorCafeina.SemCafeina && a.Quantidade == 200);
        });
    }

    // ---------- Fonte de gordura (regra 16, g10d) ----------

    // Só arroz: almoço e jantar sem fonte de gordura e o dia muito abaixo da gordura da meta. A primeira resposta é
    // recusada; a segunda, com azeite no jantar, é a usada.
    [Fact]
    public async Task Dia_Sem_Fonte_De_Gordura_Pede_De_Novo()
    {
        var gemini = new GeminiRoteirizado(
            Envelope(TextoDieta(Semana, comRefeicoes: true)),
            Envelope(TextoDieta(Semana, comRefeicoes: true, bebidaNoJantar: "Azeite de oliva extra virgem", unidadeBebida: "g", quantidadeBebida: 10)));

        var dieta = await GeradorComGordura(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(2, gemini.Chamadas);
        Assert.All(dieta.Dias, d => Assert.Contains(d.Refeicoes.Single(r => r.Nome == "Jantar").Alimentos, a => a.AlimentoId == "azeite-de-oliva-extra-virgem"));
    }

    [Fact]
    public async Task Na_Ultima_Tentativa_Aceita_Sem_Fonte_De_Gordura()
    {
        var gemini = new GeminiRoteirizado(
            Envelope(TextoDieta(Semana, comRefeicoes: true)),
            Envelope(TextoDieta(Semana, comRefeicoes: true)));

        var dieta = await GeradorComGordura(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(2, gemini.Chamadas);
        Assert.Equal(7, dieta.Dias.Count);
    }

    [Fact]
    public async Task Com_Azeite_No_Jantar_Passa_Na_Primeira_E_O_Prompt_Tem_A_Regra_16()
    {
        var gemini = new GeminiRoteirizado(
            Envelope(TextoDieta(Semana, comRefeicoes: true, bebidaNoJantar: "Azeite de oliva extra virgem", unidadeBebida: "g", quantidadeBebida: 10)));

        await GeradorComGordura(gemini).GerarComListaAsync(Contexto());

        Assert.Equal(1, gemini.Chamadas);
        Assert.Contains("16. Use as fontes de gordura da lista", PromptEnviado(gemini));
    }

    private static GeradorDietaGemini GeradorComGordura(HttpMessageHandler gemini) => new(
        new HttpClient(gemini), new CalculadoraGastoCalorico(),
        new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
        new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance)
    {
        AjustarQuantidades = false,
        ExigirMinimoDeRefeicoes = false
    };

    private static GeradorDietaGemini GeradorComMinimo(HttpMessageHandler gemini) => new(
        new HttpClient(gemini), new CalculadoraGastoCalorico(),
        new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
        new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance)
    {
        AjustarQuantidades = false,
        ExigirFonteDeGordura = false
    };

    private static ContextoDietaDto Contexto() => new()
    {
        Altura = 175, Sexo = "Masculino", Idade = 30, Peso = 80, Objetivo = "manter peso",
        Modalidades = new(), Preferencias = new(), HistoricoPeso = new()
    };

    // O JSON da dieta que a IA põe em candidates[0].content.parts[0].text.
    // bebidaNoJantar: o jantar ganha esse item (350 ml) além do alimento.
    // ceiaSoComBebida: um terceiro item, a ceia, só com esse item (350 ml, 143,5 kcal declaradas).
    private static string TextoDieta(
        IEnumerable<string> nomes, bool comRefeicoes = false, string unidade = "g", string alimento = "Arroz branco cozido",
        string? bebidaNoJantar = null, string? ceiaSoComBebida = null, string unidadeBebida = "ml", decimal quantidade = 100, decimal quantidadeBebida = 350,
        int lanchesExtras = 0)
    {
        object Item(string nome, decimal quantidade, string un) => new { nome, quantidade, unidade = un };

        object Refeicao(string nome, object[] alimentos, decimal kcal, decimal p, decimal c, decimal g) => new
        {
            nome,
            alimentos,
            macros = new { calorias = kcal, proteinasG = p, carboidratosG = c, gordurasG = g },
            horario = "12:00"
        };

        object[] Refeicoes()
        {
            var jantar = bebidaNoJantar is null
                ? new[] { Item(alimento, quantidade, unidade) }
                : new[] { Item(alimento, quantidade, unidade), Item(bebidaNoJantar, quantidadeBebida, unidadeBebida) };
            var lista = new List<object>
            {
                Refeicao("Almoço", new[] { Item(alimento, quantidade, unidade) }, 500, 30, 60, 15),
                Refeicao("Jantar", jantar, 735, 40, 90, 25)
            };
            for (var i = 1; i <= lanchesExtras; i++)
            {
                lista.Add(Refeicao($"Lanche {i}", new[] { Item(alimento, quantidade, unidade) }, 128, 3, 28, 0));
            }
            if (ceiaSoComBebida is not null)
            {
                lista.Add(Refeicao("Ceia", new[] { Item(ceiaSoComBebida, 350, unidadeBebida) }, 143.5m, 2.1m, 11.55m, 0));
            }

            return lista.ToArray();
        }

        var dias = nomes.Select(n => new
        {
            diaSemana = n,
            refeicoes = comRefeicoes ? Refeicoes() : Array.Empty<object>(),
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
        public string? UltimoCorpo { get; private set; }

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
            UltimoCorpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
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
