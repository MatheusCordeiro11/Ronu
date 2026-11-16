using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;
using Ronu.Api.Services.Conta;
using Ronu.Api.Services.IA;
using Ronu.Api.Services.ListaCompras;
using Ronu.Api.Services.PlanoFamilia;

namespace Ronu.Api.Tests;

/// <summary>
/// Plano família, etapa 6: a lista de compras da casa (a semana inteira de todos, somada, sem dizer de quem), o "já tenho"
/// da casa (compartilhado e sem autor), a "Só a minha parte" (a lista individual de sempre), a entrada e a saída no meio
/// da semana, a marca de alergia (só a de quem vê), o título do compartilhar e o orçamento da casa. Gemini sempre falso;
/// perfis fictícios com e-mails @exemplo.test.
/// </summary>
public class CasaListaComprasTests
{
    private static readonly int VersaoCasa = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoCasa);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ---------- Apoio ----------

    private static ApplicationDbContext Banco() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private record Perfil(string Sexo, DateOnly Nascimento, decimal Altura, decimal Peso, string Nivel, string Objetivo);

    // Os 3 perfis fictícios do passo 0: metas diferentes, então porções diferentes na mesma panela.
    private static readonly Perfil PerfilA = new("Masculino", new DateOnly(1996, 1, 10), 178m, 80m, "em_pe", "manter peso");
    private static readonly Perfil PerfilB = new("Feminino", new DateOnly(1998, 5, 2), 165m, 62m, "sentado", "perder peso");
    private static readonly Perfil PerfilC = new("Masculino", new DateOnly(1981, 2, 3), 175m, 95m, "andando", "perder peso");

    private static async Task<Usuario> Pessoa(ApplicationDbContext banco, string email, string nome, Perfil perfil, string? orcamento = null)
    {
        var usuario = new Usuario
        {
            Nome = nome, Email = email, Estado = "SP", Altura = perfil.Altura, Sexo = perfil.Sexo,
            DataNascimento = perfil.Nascimento, NivelAtividade = perfil.Nivel, CriadoEm = DateTime.UtcNow,
            OrcamentoSemanal = orcamento
        };
        banco.Usuarios.Add(usuario);
        await banco.SaveChangesAsync();
        banco.Consentimentos.Add(new Consentimento { UsuarioId = usuario.Id, Tipo = TextosConsentimento.TipoGeral, Versao = 1, AceitoEm = DateTime.UtcNow });
        banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = perfil.Peso, Objetivo = perfil.Objetivo, DataRegistro = DateTime.UtcNow.AddDays(-1) });
        await banco.SaveChangesAsync();
        return usuario;
    }

    private static ClaimsPrincipal Token(int usuarioId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"));

    private static ControllerContext Contexto(int usuarioId) =>
        new() { HttpContext = new DefaultHttpContext { User = Token(usuarioId) } };

    private static CasaController Casa(ApplicationDbContext banco, int usuarioId) =>
        new(banco, BancoEmMemoria.Configuracao()) { ControllerContext = Contexto(usuarioId) };

    private static ListaComprasController ListaIndividual(ApplicationDbContext banco, int usuarioId) =>
        new(banco) { ControllerContext = Contexto(usuarioId) };

    private static GeradorDietaGemini Gerador(HttpMessageHandler gemini) => new(
        new HttpClient(gemini), new CalculadoraGastoCalorico(),
        new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
        new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance);

    private static GeracaoSemanaCasa Geracao(ApplicationDbContext banco, HttpMessageHandler gemini) => new(
        banco, new ContextoDietaBuilder(banco, new CalculadoraPesoTendencia()), Gerador(gemini), NullLogger<GeracaoSemanaCasa>.Instance);

    private static DietasController Dietas(ApplicationDbContext banco, int usuarioId, HttpMessageHandler gemini) => new(
        banco, new ContextoDietaBuilder(banco, new CalculadoraPesoTendencia()), Gerador(gemini), new RepositorioDietaIA(banco),
        NullLogger<DietasController>.Instance)
    {
        ControllerContext = Contexto(usuarioId)
    };

    private static async Task<string> Convite(ApplicationDbContext banco, Usuario dono)
    {
        var criado = Assert.IsType<ConviteCriadoResponse>(Assert.IsType<CreatedResult>(await Casa(banco, dono.Id).CriarConvite()).Value);
        return criado.Link.Split("#convite=")[1];
    }

    private static async Task MontarCasa(ApplicationDbContext banco, Usuario dono, params Usuario[] membros)
    {
        Assert.IsType<CreatedResult>(await Casa(banco, dono.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa }));
        foreach (var membro in membros)
        {
            var token = await Convite(banco, dono);
            Assert.IsType<OkObjectResult>(await Casa(banco, membro.Id).AceitarConvite(new AceitarConviteRequest { Token = token, VersaoConsentimento = VersaoCasa }));
        }
    }

    private static async Task GerarSemana(ApplicationDbContext banco, Usuario dono, HttpMessageHandler? gemini = null)
    {
        var resultado = await Casa(banco, dono.Id).GerarSemana(Geracao(banco, gemini ?? new GeminiFalso()));
        Assert.True(resultado is OkObjectResult, $"{Status(resultado)} {Json((resultado as ObjectResult)?.Value)}");
    }

    // O café e os lanches de cada um são diferentes: o que é só de uma pessoa também entra na soma da casa.
    private static GeminiFalso CafeDaAna() => new()
    {
        Cafe = Itens(("iogurte-natural", 170), ("banana-nanica", 100), ("aveia-em-flocos", 30)),
        Lanche = Itens(("pao-frances", 50), ("queijo-minas-frescal", 30))
    };

    private static GeminiFalso CafeDaBia() => new()
    {
        Cafe = Itens(("banana-nanica", 120), ("aveia-em-flocos", 40)),
        Lanche = Itens(("pao-frances", 50), ("queijo-minas-frescal", 30))
    };

    private static GeminiFalso CafeDoCaio() => new()
    {
        Cafe = Itens(("ovo-de-galinha-cozido", 100), ("pao-frances", 50)),
        Lanche = Itens(("banana-nanica", 100))
    };

    /// <summary>A casa de 3 com a semana gerada e o café e os lanches de cada um (cada um com o seu).</summary>
    private static async Task<(Usuario A, Usuario B, Usuario C)> CasaDeTres(ApplicationDbContext banco)
    {
        var a = await Pessoa(banco, "ana-e6@exemplo.test", "Ana Sobrenomeficticio", PerfilA);
        var b = await Pessoa(banco, "bia-e6@exemplo.test", "Bia Sobrenomeficticio", PerfilB);
        var c = await Pessoa(banco, "caio-e6@exemplo.test", "Caio Sobrenomeficticio", PerfilC);
        await MontarCasa(banco, a, b, c);
        await GerarSemana(banco, a);
        foreach (var (pessoa, gemini) in new[] { (a, CafeDaAna()), (b, CafeDaBia()), (c, CafeDoCaio()) })
        {
            var resultado = await Dietas(banco, pessoa.Id, gemini).Gerar();
            Assert.True(resultado is OkObjectResult, $"{Status(resultado)} {Json((resultado as ObjectResult)?.Value)}");
        }
        return (a, b, c);
    }

    private static void Alergia(ApplicationDbContext banco, Usuario pessoa, string codigo)
    {
        banco.AlergiasUsuario.Add(new AlergiaUsuario { UsuarioId = pessoa.Id, Codigo = codigo, MarcadoEm = DateTime.UtcNow });
        banco.SaveChanges();
    }

    // Os primeiros nomes como palavra inteira e com maiúscula: "banana" tem "ana" dentro.
    private static void SemNomes(string texto)
    {
        foreach (var nome in new[] { "Ana", "Bia", "Caio", "Sol", "Dona", "Fora" })
        {
            Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex(@"\b" + nome + @"\b"), texto);
        }
    }

    private static int Status(IActionResult resultado) => resultado switch
    {
        ObjectResult objeto => objeto.StatusCode ?? 200,
        StatusCodeResult codigo => codigo.StatusCode,
        _ => throw new InvalidOperationException(resultado.GetType().Name)
    };

    private static string Json(object? valor) => JsonSerializer.Serialize(valor, Web);

    private static DietaSemanalDto DietaAtual(ApplicationDbContext banco, Usuario pessoa) =>
        JsonSerializer.Deserialize<DietaSemanalDto>(banco.DietasIA.AsNoTracking()
            .Where(d => d.UsuarioId == pessoa.Id).OrderByDescending(d => d.DataGeracao).ThenByDescending(d => d.Id).First().ConteudoJson)!;

    private static ListaCasaResponse ListaDaCasa(ApplicationDbContext banco, Usuario pessoa)
    {
        var resultado = Casa(banco, pessoa.Id).ObterListaCompras().Result;
        Assert.True(resultado is OkObjectResult, $"{Status(resultado)} {Json((resultado as ObjectResult)?.Value)}");
        return Assert.IsType<ListaCasaResponse>(((OkObjectResult)resultado).Value);
    }

    private static ListaComprasResponse MinhaParte(ApplicationDbContext banco, Usuario pessoa) =>
        Assert.IsType<ListaComprasResponse>(Assert.IsType<OkObjectResult>(ListaIndividual(banco, pessoa.Id).Obter().Result).Value);

    private static Task<IActionResult> Marcar(ApplicationDbContext banco, Usuario pessoa, ItemListaComprasDto item, int semanaId, bool tenho = true) =>
        Casa(banco, pessoa.Id).MarcarListaCompras(item.Chave, new MarcacaoListaCasaRequest { SemanaId = semanaId, Tenho = tenho, Quantidade = item.Quantidade });

    private static ItemListaComprasDto ItemDa(ListaCasaResponse lista, string chave) => lista.Secoes.SelectMany(s => s.Itens).Single(i => i.Chave == chave);

    // A soma da semana por AlimentoId, como a dieta guarda (g ou ml).
    private static Dictionary<string, decimal> PorAlimento(params DietaSemanalDto[] dietas) => dietas
        .SelectMany(d => d.Dias).SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos)
        .Where(x => x.Quantidade > 0)
        .GroupBy(x => x.AlimentoId!)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantidade));

    private static Dictionary<string, decimal> Origens(IEnumerable<SecaoListaComprasDto> secoes) => secoes
        .SelectMany(s => s.Itens).SelectMany(i => i.Detalhe.Origens)
        .ToDictionary(o => o.AlimentoId, o => o.QuantidadeNaDieta);

    // ---------- Soma ----------

    [Fact]
    public async Task Soma_A_Semana_Inteira_Dos_Tres_Com_A_Porcao_De_Cada_Um_E_Os_Cafes_Sem_Dizer_De_Quem()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        var dietas = new[] { a, b, c }.Select(p => DietaAtual(banco, p)).ToArray();

        var lista = ListaDaCasa(banco, b);

        Assert.True(lista.Disponivel);
        Assert.True(lista.Completa);
        // Cada alimento da semana de todos (almoço e jantar da casa nas 3 porções, e o café e os lanches de cada um), somado.
        var esperado = PorAlimento(dietas);
        var origens = Origens(lista.Secoes);
        Assert.Equal(esperado.OrderBy(x => x.Key), origens.OrderBy(x => x.Key));
        // O iogurte é só do café da Ana, e o ovo do café do Caio entra junto com o ovo do jantar da casa.
        Assert.Equal(PorAlimento(dietas[0])["iogurte-natural"], origens["iogurte-natural"]);
        Assert.Equal(PorAlimento(dietas[0])["ovo-de-galinha-cozido"] + PorAlimento(dietas[1])["ovo-de-galinha-cozido"] + PorAlimento(dietas[2])["ovo-de-galinha-cozido"],
            origens["ovo-de-galinha-cozido"]);

        // As porções da casa são diferentes (a mesma panela), e o total da casa não é a porção de ninguém: nenhum número da
        // lista é o de uma pessoa só quando o alimento é de mais de uma.
        var porPessoa = dietas.Select(d => PorAlimento(d)).ToList();
        Assert.Equal(3, porPessoa.Select(p => p["arroz-branco-cozido"]).Distinct().Count());
        foreach (var (alimento, total) in origens)
        {
            var deQuem = porPessoa.Where(p => p.ContainsKey(alimento)).ToList();
            if (deQuem.Count > 1)
            {
                Assert.All(deQuem, p => Assert.NotEqual(p[alimento], total));
            }
        }

        // O arredondamento para a compra é feito uma vez, sobre o total da casa: nunca mais que a soma das listas de cada um
        // arredondadas separadas, e nunca menos que o exato.
        var individuais = new[] { a, b, c }.Select(p => MinhaParte(banco, p)).ToList();
        foreach (var item in lista.Secoes.SelectMany(s => s.Itens))
        {
            Assert.True(item.Quantidade >= item.Detalhe.QuantidadeExata);
            var somaDasPartes = individuais.SelectMany(l => l.Secoes).SelectMany(s => s.Itens).Where(i => i.Chave == item.Chave).Sum(i => i.Quantidade);
            Assert.True(item.Quantidade <= somaDasPartes, item.Chave);
            // O exato da casa é a soma dos exatos de cada um (cada exato vem com 2 casas: a diferença fica no centésimo).
            var somaDosExatos = individuais.SelectMany(l => l.Secoes).SelectMany(s => s.Itens).Where(i => i.Chave == item.Chave).Sum(i => i.Detalhe.QuantidadeExata);
            Assert.InRange(Math.Abs(somaDosExatos - item.Detalhe.QuantidadeExata), 0m, 0.05m);
        }
        // Na ordem do mercado, como a individual.
        Assert.Equal(lista.Secoes.Select(s => s.Chave), lista.Secoes.Select(s => s.Chave).OrderBy(s => new[] { "hortifruti", "acougue-e-peixaria", "padaria", "frios-laticinios-e-ovos", "mercearia", "bebidas", "congelados" }.ToList().IndexOf(s)));

        // Todos os membros veem a mesma soma.
        Assert.Equal(Json(lista.Secoes), Json(ListaDaCasa(banco, a).Secoes));
        Assert.Equal(Json(lista.Secoes), Json(ListaDaCasa(banco, c).Secoes));
    }

    // Embalagens (2026-11-16): calculadas uma vez, sobre o total da casa (uma compra só), e não somando as de cada um.
    [Fact]
    public async Task Embalagem_Da_Casa_E_Calculada_Sobre_O_Total_Da_Casa()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        var dietas = new[] { a, b, c }.Select(p => DietaAtual(banco, p)).ToArray();

        var lista = ListaDaCasa(banco, b);
        var juntas = MontadorListaCompras.Montar(new DietaSemanalDto { Dias = dietas.SelectMany(d => d.Dias).ToList() }, "SP");

        var itens = lista.Secoes.SelectMany(s => s.Itens).ToList();
        Assert.Equal(Json(juntas.Secoes.SelectMany(s => s.Itens).Select(i => new { i.Chave, i.Embalagem })),
            Json(itens.Select(i => new { i.Chave, i.Embalagem })));
        Assert.Contains(itens, i => i.Embalagem is not null);

        // O arroz da casa é de todos: a embalagem cobre o exato da casa, e o texto e a quantidade da linha não mudam.
        var arroz = ItemDa(lista, "arroz-branco");
        Assert.NotNull(arroz.Embalagem);
        Assert.True(arroz.Embalagem!.TamanhoTotal >= arroz.Detalhe.QuantidadeExata);
        Assert.Equal(MontadorListaCompras.Texto(arroz.Quantidade, ConversaoCompras.Itens.Single(i => i.Chave == "arroz-branco")), arroz.Texto);

        // A marca da casa continua pela quantidade da linha (em gramas), como antes.
        Assert.IsType<NoContentResult>(await Marcar(banco, b, arroz, lista.SemanaId));
        Assert.True(ItemDa(ListaDaCasa(banco, a), "arroz-branco").Tenho);
    }

    [Fact]
    public async Task So_A_Minha_Parte_E_A_Lista_Individual_De_Hoje_Com_As_Marcacoes_Individuais()
    {
        using var banco = Banco();
        var (_, b, _) = await CasaDeTres(banco);

        var minha = MinhaParte(banco, b);
        var casa = ListaDaCasa(banco, b);

        // A parte da Bia é a lista de hoje, montada só da dieta dela, item por item.
        var esperada = MontadorListaCompras.Montar(DietaAtual(banco, b), "SP");
        Assert.Equal(Json(esperada.Secoes), Json(minha.Secoes));
        Assert.Equal(PorAlimento(DietaAtual(banco, b)).OrderBy(x => x.Key), Origens(minha.Secoes).OrderBy(x => x.Key));
        Assert.NotEqual(Json(casa.Secoes), Json(minha.Secoes));

        // As marcações não se misturam: o "já tenho" individual não marca a casa, e o da casa não marca o individual.
        var arroz = minha.Secoes.SelectMany(s => s.Itens).Single(i => i.Chave == "arroz-branco");
        Assert.IsType<NoContentResult>(await ListaIndividual(banco, b.Id).Marcar(arroz.Chave, new MarcacaoListaComprasRequest { DietaId = minha.DietaId, Tenho = true }));
        Assert.False(ItemDa(ListaDaCasa(banco, b), "arroz-branco").Tenho);

        var banana = ItemDa(casa, "banana-nanica");
        Assert.IsType<NoContentResult>(await Marcar(banco, b, banana, casa.SemanaId));
        Assert.False(MinhaParte(banco, b).Secoes.SelectMany(s => s.Itens).Single(i => i.Chave == "banana-nanica").Tenho);
        Assert.True(MinhaParte(banco, b).Secoes.SelectMany(s => s.Itens).Single(i => i.Chave == "arroz-branco").Tenho);
    }

    // ---------- "Já tenho" da casa ----------

    [Fact]
    public async Task Ja_Tenho_Da_Casa_Vale_Para_Todos_E_Nao_Guarda_Quem_Marcou()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        var lista = ListaDaCasa(banco, b);
        var arroz = ItemDa(lista, "arroz-branco");

        // A Bia marca; a Ana e o Caio veem marcado.
        Assert.IsType<NoContentResult>(await Marcar(banco, b, arroz, lista.SemanaId));
        Assert.True(ItemDa(ListaDaCasa(banco, a), "arroz-branco").Tenho);
        Assert.True(ItemDa(ListaDaCasa(banco, c), "arroz-branco").Tenho);
        // Marcar de novo (outro membro, mesmo item) não duplica nem é erro.
        Assert.IsType<NoContentResult>(await Marcar(banco, a, arroz, lista.SemanaId));
        Assert.Equal(1, await banco.ListaCasaMarcacoes.CountAsync());

        // O banco não tem como saber quem marcou: nenhuma coluna de pessoa.
        var colunas = banco.Model.FindEntityType(typeof(ListaCasaMarcacao))!.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Equal(new HashSet<string> { "Id", "CasaId", "SemanaCasaId", "Chave", "Quantidade", "MarcadoEm" }, colunas);

        // O Caio desmarca o que a Bia marcou: desmarcado para todos. Desmarcar de novo também dá 204.
        Assert.IsType<NoContentResult>(await Marcar(banco, c, arroz, lista.SemanaId, tenho: false));
        Assert.IsType<NoContentResult>(await Marcar(banco, c, arroz, lista.SemanaId, tenho: false));
        Assert.False(ItemDa(ListaDaCasa(banco, a), "arroz-branco").Tenho);
        Assert.Equal(0, await banco.ListaCasaMarcacoes.CountAsync());
    }

    [Fact]
    public async Task Marcacao_Recusa_Chave_Invalida_Item_Fora_Semana_Velha_E_Quantidade_Que_Subiu()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);
        var lista = ListaDaCasa(banco, b);
        var arroz = ItemDa(lista, "arroz-branco");
        var controller = Casa(banco, b.Id);

        Assert.Equal(400, Status(await controller.MarcarListaCompras("ARROZ!", new MarcacaoListaCasaRequest { SemanaId = lista.SemanaId, Tenho = true, Quantidade = 1 })));
        Assert.Equal(400, Status(await controller.MarcarListaCompras("salmao", new MarcacaoListaCasaRequest { SemanaId = lista.SemanaId, Tenho = true, Quantidade = 1 })));
        Assert.Equal(400, Status(await controller.MarcarListaCompras(arroz.Chave, new MarcacaoListaCasaRequest { SemanaId = lista.SemanaId, Tenho = true })));
        Assert.Equal(409, Status(await controller.MarcarListaCompras(arroz.Chave, new MarcacaoListaCasaRequest { SemanaId = lista.SemanaId + 100, Tenho = true, Quantidade = arroz.Quantidade })));
        // A tela mostrava menos do que a lista pede agora: 409, sem marcar.
        Assert.Equal(409, Status(await controller.MarcarListaCompras(arroz.Chave, new MarcacaoListaCasaRequest { SemanaId = lista.SemanaId, Tenho = true, Quantidade = arroz.Quantidade - 1 })));
        Assert.Equal(0, await banco.ListaCasaMarcacoes.CountAsync());

        // Semana nova: a marca da semana anterior não vale, e a lista começa sem marcações.
        Assert.IsType<NoContentResult>(await Marcar(banco, b, arroz, lista.SemanaId));
        await GerarSemana(banco, a);
        var nova = ListaDaCasa(banco, b);
        Assert.NotEqual(lista.SemanaId, nova.SemanaId);
        Assert.All(nova.Secoes.SelectMany(s => s.Itens), i => Assert.False(i.Tenho));
        Assert.Equal(409, Status(await Marcar(banco, b, arroz, lista.SemanaId)));
    }

    // ---------- Entrada e saída ----------

    [Fact]
    public async Task Quem_Entra_Passa_A_Contar_Quem_Sai_Deixa_De_Contar_E_O_Ja_Tenho_Cai_Quando_A_Quantidade_Sobe()
    {
        using var banco = Banco();
        var a = await Pessoa(banco, "ana-e6b@exemplo.test", "Ana Sobrenomeficticio", PerfilA);
        var b = await Pessoa(banco, "bia-e6b@exemplo.test", "Bia Sobrenomeficticio", PerfilB);
        await MontarCasa(banco, a, b);
        await GerarSemana(banco, a);
        var antes = ListaDaCasa(banco, a);
        var arroz = ItemDa(antes, "arroz-branco");
        Assert.IsType<NoContentResult>(await Marcar(banco, b, arroz, antes.SemanaId));

        // O Caio entra no meio da semana e recebe as porções na hora (sem IA): a lista passa a ter a parte dele.
        var c = await Pessoa(banco, "caio-e6b@exemplo.test", "Caio Sobrenomeficticio", PerfilC);
        var gemini = new GeminiFalso();
        Assert.IsType<OkObjectResult>(await Casa(banco, c.Id).AceitarConvite(
            new AceitarConviteRequest { Token = await Convite(banco, a), VersaoConsentimento = VersaoCasa }, Geracao(banco, gemini)));
        Assert.Equal(0, gemini.Chamadas);

        var comCaio = ListaDaCasa(banco, c);
        Assert.Equal(antes.SemanaId, comCaio.SemanaId);
        Assert.True(comCaio.Completa);
        Assert.Equal(PorAlimento(DietaAtual(banco, a), DietaAtual(banco, b), DietaAtual(banco, c)).OrderBy(x => x.Key), Origens(comCaio.Secoes).OrderBy(x => x.Key));
        Assert.True(ItemDa(comCaio, "arroz-branco").Quantidade > arroz.Quantidade);
        // O arroz passou a pedir mais do que estava marcado: o "já tenho" cai (a casa não tem tudo).
        Assert.False(ItemDa(comCaio, "arroz-branco").Tenho);

        // O Caio sai: a lista volta a ser só da Ana e da Bia, e a marca volta a valer (a quantidade cabe de novo).
        Assert.IsType<NoContentResult>(await Casa(banco, c.Id).Sair());
        var semCaio = ListaDaCasa(banco, a);
        Assert.Equal(Json(antes.Secoes.SelectMany(s => s.Itens).Select(i => (i.Chave, i.Quantidade))),
            Json(semCaio.Secoes.SelectMany(s => s.Itens).Select(i => (i.Chave, i.Quantidade))));
        Assert.True(ItemDa(semCaio, "arroz-branco").Tenho);
        // E quem saiu não vê mais a lista da casa.
        Assert.Equal(404, Status(await Casa(banco, c.Id).ObterListaCompras()));
    }

    [Fact]
    public async Task Membro_Sem_A_Semana_Na_Dieta_Fica_De_Fora_E_A_Lista_Diz_So_Que_Nao_Esta_Completa()
    {
        using var banco = Banco();
        var a = await Pessoa(banco, "ana-e6c@exemplo.test", "Ana Sobrenomeficticio", PerfilA);
        var b = await Pessoa(banco, "bia-e6c@exemplo.test", "Bia Sobrenomeficticio", PerfilB);
        await MontarCasa(banco, a, b);
        await GerarSemana(banco, a);
        // A dieta mais recente da Bia deixa de ser da semana da casa (como a de quem ficou sem dieta nova).
        var linha = await banco.DietasIA.Where(d => d.UsuarioId == b.Id).OrderByDescending(d => d.Id).FirstAsync();
        linha.SemanaCasaId = null;
        await banco.SaveChangesAsync();

        var lista = ListaDaCasa(banco, a);

        Assert.False(lista.Completa);
        Assert.Equal(PorAlimento(DietaAtual(banco, a)).OrderBy(x => x.Key), Origens(lista.Secoes).OrderBy(x => x.Key));
        SemNomes(Json(lista));
    }

    // ---------- Alergia ----------

    [Fact]
    public async Task Alergia_Na_Lista_Da_Casa_Usa_So_A_De_Quem_Ve_E_Nao_Revela_A_De_Ninguem()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        // Depois das dietas geradas: o Caio marca leite; o iogurte e o queijo vêm do café e do lanche das outras duas.
        Alergia(banco, c, "leite");

        var doCaio = ListaDaCasa(banco, c);
        var daAna = ListaDaCasa(banco, a);
        var daBia = ListaDaCasa(banco, b);

        // O Caio vê a marca na lista da casa (é dele), com o chip e a origem.
        var iogurte = ItemDa(doCaio, "iogurte-natural");
        Assert.True(iogurte.TemAlergeno);
        Assert.Equal(["leite"], iogurte.Alergias);
        Assert.Contains("leite", Assert.Single(iogurte.Detalhe.Origens).Alergias);
        // A Ana e a Bia não veem marca nenhuma em item nenhum: a lista não conta que alguém da casa tem alergia.
        foreach (var lista in new[] { daAna, daBia })
        {
            Assert.All(lista.Secoes.SelectMany(s => s.Itens), i =>
            {
                Assert.False(i.TemAlergeno);
                Assert.Empty(i.Alergias);
                Assert.All(i.Detalhe.Origens, o => Assert.Empty(o.Alergias));
            });
            var json = Json(lista);
            Assert.DoesNotContain("\"leite\"", json);
            Assert.DoesNotContain("Leite e derivados", json);
        }
        // Fora as marcas, as três listas são iguais (a soma não muda com a alergia de ninguém).
        string SemAlergia(ListaCasaResponse l) => Json(l.Secoes.SelectMany(s => s.Itens).Select(i => (i.Chave, i.Quantidade, i.Texto)));
        Assert.Equal(SemAlergia(daAna), SemAlergia(doCaio));

        // A Ana marca ovo: ela passa a ver os ovos marcados, e o Caio continua sem a marca do ovo.
        Alergia(banco, a, "ovo");
        Assert.Contains("ovo", ItemDa(ListaDaCasa(banco, a), "ovos").Alergias);
        Assert.DoesNotContain("ovo", ItemDa(ListaDaCasa(banco, c), "ovos").Alergias);
    }

    // ---------- Compartilhar e vazamento ----------

    [Fact]
    public async Task Compartilhar_Leva_So_O_Titulo_Da_Casa_E_Os_Itens_Sem_Alergia_Nem_Nomes()
    {
        using var banco = Banco();
        var (a, _, c) = await CasaDeTres(banco);
        Alergia(banco, c, "leite");
        var semana = await banco.SemanasCasa.AsNoTracking().SingleAsync();

        var lista = ListaDaCasa(banco, c);

        var data = FusoHorarioEstado.Hoje("SP", semana.GeradaEm).ToString("dd/MM", CultureInfo.InvariantCulture);
        Assert.Equal($"Lista de compras da casa · semana de {data}", lista.TituloCompartilhar);
        // O que o compartilhar monta (o título e, de cada item, o nome, a quantidade e o começo da nota, como a tela de
        // hoje) não tem alergia nem nome de ninguém, nem para quem tem a alergia.
        var texto = string.Join("\n", new[] { lista.TituloCompartilhar }.Concat(lista.Secoes.SelectMany(s => s.Itens)
            .Select(i => $"{i.Nome}: {i.Texto} {i.Nota}")));
        foreach (var proibido in new[] { "leite", "alerg", "Sobrenome", "exemplo.test", "kcal" })
        {
            Assert.DoesNotContain(proibido, texto, StringComparison.OrdinalIgnoreCase);
        }
        SemNomes(texto);
        Assert.Equal(lista.TituloCompartilhar, ListaDaCasa(banco, a).TituloCompartilhar);
    }

    [Fact]
    public async Task Nenhum_Campo_Da_Lista_Da_Casa_Leva_Dado_De_Um_Membro()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);

        var json = Json(ListaDaCasa(banco, b));

        using var doc = JsonDocument.Parse(json);
        var campos = new HashSet<string>();
        void Andar(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in e.EnumerateObject()) { campos.Add(p.Name); Andar(p.Value); }
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in e.EnumerateArray()) Andar(v);
            }
        }
        Andar(doc.RootElement);
        var permitidos = new HashSet<string>
        {
            "semanaId", "geradaEm", "tituloCompartilhar", "versaoConversao", "disponivel", "motivo", "avisoOleo", "completa",
            "secoes", "chave", "nome", "itens", "quantidade", "unidade", "texto", "pronto", "nota", "tenho", "temAlergeno",
            "alergias", "detalhe", "quantidadeExata", "origens", "alimentoId", "quantidadeNaDieta", "medida", "fator",
            "convertido", "passos", "tipo", "descricao", "observacoes",
            // Embalagens (2026-11-16): calculadas sobre o total da casa, como a quantidade.
            "embalagem", "rotulo", "tamanhoTotal", "sobra", "sobraTexto", "fonte"
        };
        Assert.All(campos, campo => Assert.Contains(campo, permitidos));
        foreach (var proibido in new[] { "Sobrenome", "exemplo.test", "usuario", "membro", "dietaId", "kcal", "calorias", "objetivo", "email", "macros" })
        {
            Assert.DoesNotContain(proibido, json, StringComparison.OrdinalIgnoreCase);
        }
        SemNomes(json);

        // Nenhum número de quantidade da lista é a porção de uma pessoa num dia ou a soma da semana de uma pessoa só,
        // quando o alimento é de mais de uma (a porção da Ana no almoço de segunda, por exemplo, não aparece).
        var porPessoa = new[] { a, c }.Select(p => DietaAtual(banco, p)).ToList();
        var porcoesDosOutros = porPessoa
            .SelectMany(d => d.Dias).SelectMany(d => d.Refeicoes).Where(r => r.DaCasa).SelectMany(r => r.Alimentos)
            .Select(x => x.Quantidade).ToHashSet();
        var lista = ListaDaCasa(banco, b);
        Assert.All(lista.Secoes.SelectMany(s => s.Itens).SelectMany(i => i.Detalhe.Origens).Where(o => o.AlimentoId == "arroz-branco-cozido"),
            o => Assert.DoesNotContain(o.QuantidadeNaDieta, porcoesDosOutros));
    }

    // ---------- Quem vê ----------

    [Fact]
    public async Task Quem_Nao_E_Da_Casa_Recebe_404_Sem_Semana_Tambem_E_Sem_O_Aceite_409()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);
        var lista = ListaDaCasa(banco, a);
        var arroz = ItemDa(lista, "arroz-branco");

        // Quem não é da casa: 404 na leitura e na marcação, mesmo com o semanaId certo, e nada é gravado.
        var fora = await Pessoa(banco, "fora-e6@exemplo.test", "Fora Sobrenomeficticio", PerfilA);
        Assert.Equal(404, Status(await Casa(banco, fora.Id).ObterListaCompras()));
        Assert.Equal(404, Status(await Marcar(banco, fora, arroz, lista.SemanaId)));
        Assert.Equal(0, await banco.ListaCasaMarcacoes.CountAsync());

        // Outra casa, com a própria semana: o semanaId desta casa não serve lá (409), e a lista de lá é outra.
        var dona = await Pessoa(banco, "dona-e6@exemplo.test", "Dona Sobrenomeficticio", PerfilB);
        await MontarCasa(banco, dona);
        Assert.Equal(404, Status(await Casa(banco, dona.Id).ObterListaCompras()));
        await GerarSemana(banco, dona);
        Assert.NotEqual(lista.SemanaId, ListaDaCasa(banco, dona).SemanaId);
        Assert.Equal(409, Status(await Marcar(banco, dona, arroz, lista.SemanaId)));
        Assert.Equal(0, await banco.ListaCasaMarcacoes.CountAsync());

        // Sem o aceite "casa" vigente: 409 com o motivo, como as outras rotas da casa.
        foreach (var aceite in banco.Consentimentos.Where(x => x.UsuarioId == b.Id && x.Tipo == TextosConsentimento.TipoCasa))
        {
            aceite.RevogadoEm = DateTime.UtcNow;
        }
        await banco.SaveChangesAsync();
        Assert.Equal(409, Status(await Casa(banco, b.Id).ObterListaCompras()));
        Assert.Equal(409, Status(await Marcar(banco, b, arroz, lista.SemanaId)));
    }

    // ---------- Fim da semana e da casa ----------

    [Fact]
    public async Task Semana_Que_Sai_Leva_As_Marcacoes_E_A_Casa_Que_Acaba_Tambem()
    {
        using var banco = Banco();
        var dono = await Pessoa(banco, "sozinho-e6@exemplo.test", "Sol Sobrenomeficticio", PerfilA);
        await MontarCasa(banco, dono);
        await GerarSemana(banco, dono);
        var primeira = ListaDaCasa(banco, dono);
        Assert.IsType<NoContentResult>(await Marcar(banco, dono, ItemDa(primeira, "arroz-branco"), primeira.SemanaId));

        // A casa guarda 2 semanas: na terceira, a primeira sai, com a marcação dela.
        await GerarSemana(banco, dono);
        Assert.Equal(1, await banco.ListaCasaMarcacoes.CountAsync());
        await GerarSemana(banco, dono);
        Assert.Equal(0, await banco.ListaCasaMarcacoes.CountAsync());

        // O dono sozinho apaga a conta: a casa sai com as marcações da semana de agora.
        var atual = ListaDaCasa(banco, dono);
        Assert.IsType<NoContentResult>(await Marcar(banco, dono, ItemDa(atual, "arroz-branco"), atual.SemanaId));
        Assert.True(await ExclusaoConta.ApagarAsync(banco, dono.Id));
        Assert.Equal(0, await banco.ListaCasaMarcacoes.CountAsync());
        Assert.Equal(0, await banco.Casas.CountAsync());
    }

    // ---------- Orçamento da casa ----------

    [Fact]
    public async Task Orcamento_Da_Casa_So_O_Dono_Muda_Vale_So_Para_Almoco_E_Jantar_E_O_Cafe_Segue_O_De_Cada_Um()
    {
        using var banco = Banco();
        var a = await Pessoa(banco, "ana-e6d@exemplo.test", "Ana Sobrenomeficticio", PerfilA, orcamento: "sem_restricao");
        var b = await Pessoa(banco, "bia-e6d@exemplo.test", "Bia Sobrenomeficticio", PerfilB, orcamento: "moderado");
        var fora = await Pessoa(banco, "fora-e6d@exemplo.test", "Fora Sobrenomeficticio", PerfilC);
        await MontarCasa(banco, a, b);

        // Só o dono muda; o membro recebe 403 e quem não é da casa, 404. Todos da casa leem.
        Assert.Equal(403, Status(await Casa(banco, b.Id).AtualizarOrcamento(new OrcamentoCasaRequest { Orcamento = "sem_restricao" })));
        Assert.Equal(404, Status(await Casa(banco, fora.Id).AtualizarOrcamento(new OrcamentoCasaRequest { Orcamento = "economico" })));
        Assert.IsType<OkObjectResult>(await Casa(banco, a.Id).AtualizarOrcamento(new OrcamentoCasaRequest { Orcamento = "economico" }));
        var vistaDoMembro = Assert.IsType<CasaResponse>(Assert.IsType<OkObjectResult>(await Casa(banco, b.Id).Obter()).Value);
        Assert.Equal("economico", vistaDoMembro.Orcamento);

        // A semana da casa (almoço e jantar) leva o orçamento da casa, não o do perfil do dono.
        var daCasa = new GeminiFalso();
        await GerarSemana(banco, a, daCasa);
        var promptCasa = Assert.Single(daCasa.Prompts);
        Assert.Contains("Orçamento semanal da casa para alimentação: economico", promptCasa);
        Assert.DoesNotContain("alimentação: sem_restricao", promptCasa);

        // O café e os lanches da Bia seguem o orçamento do perfil dela, não o da casa.
        var daBia = new GeminiFalso();
        Assert.IsType<OkObjectResult>(await Dietas(banco, b.Id, daBia).Gerar());
        var promptBia = Assert.Single(daBia.Prompts);
        Assert.Contains("monte: café da manhã e lanches", promptBia);
        Assert.Contains("Orçamento semanal da pessoa para alimentação: moderado", promptBia);
        Assert.DoesNotContain("alimentação: economico", promptBia);
    }

    // ---------- Gemini falso ----------

    private static string Item(string id) => BaseNutricional.Itens.Single(i => i.Id == id).Nome;

    private static object[] Itens(params (string Id, decimal Q)[] itens) =>
        itens.Select(i => (object)new { nome = Item(i.Id), quantidade = i.Q, unidade = "g" }).ToArray();

    private static readonly object[] AlmocoPadrao = Itens(
        ("arroz-branco-cozido", 150), ("feijao-cozido", 100), ("peito-de-frango-sem-pele-grelhado", 120),
        ("alface-crua", 30), ("tomate-cru", 50), ("azeite-de-oliva-extra-virgem", 5));

    private static readonly object[] JantarPadrao = Itens(
        ("macarrao-cozido", 180), ("ovo-de-galinha-cozido", 100), ("brocolis-cozido", 80), ("azeite-de-oliva-extra-virgem", 8));

    /// <summary>
    /// Responde a semana da casa (prompt "para uma casa": almoço e jantar padrão) ou a semana individual (o café da manhã e
    /// o lanche da tarde configurados). Guarda cada prompt.
    /// </summary>
    private sealed class GeminiFalso : HttpMessageHandler
    {
        public object[] Cafe { get; init; } = Itens(("iogurte-natural", 170), ("banana-nanica", 100), ("aveia-em-flocos", 30));
        public object[] Lanche { get; init; } = Itens(("pao-frances", 50), ("queijo-minas-frescal", 30));

        public List<string> Prompts { get; } = [];
        public int Chamadas => Prompts.Count;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var corpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            var prompt = JsonDocument.Parse(corpo).RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
            Prompts.Add(prompt);

            object Refeicao(string nome, object[] alimentos, string horario) => new
            {
                nome, alimentos, horario,
                macros = new { calorias = 500, proteinasG = 30, carboidratosG = 60, gordurasG = 15 }
            };

            var casa = prompt.Contains("para uma casa");
            var dias = GeradorDietaGemini.NomesDias.Select(n => new
            {
                diaSemana = n,
                refeicoes = casa
                    ? new[] { Refeicao("Almoço", AlmocoPadrao, "12:30"), Refeicao("Jantar", JantarPadrao, "19:30") }
                    : new[] { Refeicao("Café da manhã", Cafe, "07:30"), Refeicao("Lanche da tarde", Lanche, "16:00") },
                totalDoDia = new { calorias = 1000, proteinasG = 60, carboidratosG = 120, gordurasG = 30 }
            });

            var envelope = JsonSerializer.Serialize(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = JsonSerializer.Serialize(new { dias }) } } }, finishReason = "STOP" } },
                usageMetadata = new { promptTokenCount = 1500, candidatesTokenCount = 4000, totalTokenCount = 5500 }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope, Encoding.UTF8, "application/json") };
        }
    }
}
