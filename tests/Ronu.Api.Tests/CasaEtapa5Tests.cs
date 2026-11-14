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
using Ronu.Api.Services.PlanoFamilia;
using Ronu.Api.Services.Troca;

namespace Ronu.Api.Tests;

/// <summary>
/// Plano família, etapa 5: os pedidos (sem autor, contador só para o dono), a troca na casa (só o dono, vale para todos,
/// porção de cada um refeita, restrições de todos), a recusa da troca individual num item da casa, a entrada no meio
/// da semana (porções sem IA, refeição individual e aviso sem nome), a saída e a remoção (as refeições ficam, sem a marca
/// da casa) e a transferência. Gemini sempre falso; perfis fictícios com e-mails @exemplo.test.
/// </summary>
public class CasaEtapa5Tests
{
    private static readonly int VersaoCasa = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoCasa);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ---------- Apoio ----------

    private static ApplicationDbContext Banco() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private record Perfil(string Sexo, DateOnly Nascimento, decimal Altura, decimal Peso, string Nivel, string Objetivo);

    private static readonly Perfil PerfilA = new("Masculino", new DateOnly(1996, 1, 10), 178m, 80m, "em_pe", "manter peso");
    private static readonly Perfil PerfilB = new("Feminino", new DateOnly(1998, 5, 2), 165m, 62m, "sentado", "perder peso");
    private static readonly Perfil PerfilC = new("Masculino", new DateOnly(1981, 2, 3), 175m, 95m, "andando", "perder peso");

    private static async Task<Usuario> Pessoa(ApplicationDbContext banco, string email, string nome, Perfil perfil)
    {
        var usuario = new Usuario
        {
            Nome = nome, Email = email, Estado = "SP", Altura = perfil.Altura, Sexo = perfil.Sexo,
            DataNascimento = perfil.Nascimento, NivelAtividade = perfil.Nivel, CriadoEm = DateTime.UtcNow
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

    private static CasaController Casa(ApplicationDbContext banco, int usuarioId) => new(banco, BancoEmMemoria.Configuracao())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Token(usuarioId) } }
    };

    private static TrocasController Trocas(ApplicationDbContext banco, int usuarioId) => new(banco, NullLogger<TrocasController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Token(usuarioId) } }
    };

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
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Token(usuarioId) } }
    };

    private static async Task<string> Convite(ApplicationDbContext banco, Usuario dono)
    {
        var criado = Assert.IsType<ConviteCriadoResponse>(Assert.IsType<CreatedResult>(await Casa(banco, dono.Id).CriarConvite()).Value);
        return criado.Link.Split("#convite=")[1];
    }

    // A casa com o primeiro como dono e os outros como membros (pelo convite, como na tela), antes de qualquer semana.
    private static async Task MontarCasa(ApplicationDbContext banco, Usuario dono, params Usuario[] membros)
    {
        Assert.IsType<CreatedResult>(await Casa(banco, dono.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa }));
        foreach (var membro in membros)
        {
            var token = await Convite(banco, dono);
            Assert.IsType<OkObjectResult>(await Casa(banco, membro.Id).AceitarConvite(new AceitarConviteRequest { Token = token, VersaoConsentimento = VersaoCasa }));
        }
    }

    private static async Task<(Usuario A, Usuario B, Usuario C)> CasaDeTresComSemana(ApplicationDbContext banco, GeminiFalso? gemini = null)
    {
        var a = await Pessoa(banco, "ana-e5@exemplo.test", "Ana Sobrenomeficticio", PerfilA);
        var b = await Pessoa(banco, "bia-e5@exemplo.test", "Bia Sobrenomeficticio", PerfilB);
        var c = await Pessoa(banco, "caio-e5@exemplo.test", "Caio Sobrenomeficticio", PerfilC);
        await MontarCasa(banco, a, b, c);
        await GerarSemana(banco, a, gemini ?? new GeminiFalso());
        return (a, b, c);
    }

    private static void NaoComo(ApplicationDbContext banco, Usuario pessoa, string alimento)
    {
        banco.PreferenciasAlimentares.Add(new PreferenciaAlimentar { UsuarioId = pessoa.Id, Alimento = alimento, Tipo = TiposPreferencia.NaoComo });
        banco.SaveChanges();
    }

    private static void Alergia(ApplicationDbContext banco, Usuario pessoa, string codigo)
    {
        banco.AlergiasUsuario.Add(new AlergiaUsuario { UsuarioId = pessoa.Id, Codigo = codigo, MarcadoEm = DateTime.UtcNow });
        banco.SaveChanges();
    }

    private static int Status(IActionResult resultado) => resultado switch
    {
        ObjectResult objeto => objeto.StatusCode ?? 200,
        StatusCodeResult codigo => codigo.StatusCode,
        _ => throw new InvalidOperationException(resultado.GetType().Name)
    };

    private static string Json(object? valor) => JsonSerializer.Serialize(valor, Web);

    private static async Task<SemanaCasaResponse> GerarSemana(ApplicationDbContext banco, Usuario dono, HttpMessageHandler gemini)
    {
        var resultado = await Casa(banco, dono.Id).GerarSemana(Geracao(banco, gemini));
        Assert.True(resultado is OkObjectResult, $"{Status(resultado)} {Json((resultado as ObjectResult)?.Value)}");
        return Assert.IsType<SemanaCasaResponse>(((OkObjectResult)resultado).Value);
    }

    private static DietaIA LinhaAtual(ApplicationDbContext banco, Usuario pessoa) => banco.DietasIA.AsNoTracking()
        .Where(d => d.UsuarioId == pessoa.Id).OrderByDescending(d => d.DataGeracao).ThenByDescending(d => d.Id).First();

    private static DietaSemanalDto DietaAtual(ApplicationDbContext banco, Usuario pessoa) =>
        JsonSerializer.Deserialize<DietaSemanalDto>(LinhaAtual(banco, pessoa).ConteudoJson)!;

    private static RefeicaoDto RefeicaoDaCasa(DietaSemanalDto dieta, string chave) =>
        dieta.Dias.SelectMany(d => d.Refeicoes).Single(r => r.ChaveCasa == chave);

    private static CasaResponse CasaDe(ApplicationDbContext banco, Usuario pessoa) =>
        Assert.IsType<CasaResponse>(Assert.IsType<OkObjectResult>(Casa(banco, pessoa.Id).Obter().Result).Value);

    private static SemanaCasaResponse SemanaDe(ApplicationDbContext banco, Usuario pessoa) =>
        Assert.IsType<SemanaCasaResponse>(Assert.IsType<OkObjectResult>(Casa(banco, pessoa.Id).ObterSemana().Result).Value);

    private static string Item(string id) => BaseNutricional.Itens.Single(i => i.Id == id).Nome;

    // O item do frango no almoço da casa (posição 2 do AlmocoPadrao).
    private const int PosicaoFrango = 2;
    private const string Frango = "peito-de-frango-sem-pele-grelhado";

    private static OpcoesTrocaCasaResponse Opcoes(ApplicationDbContext banco, Usuario dono, string chave = "seg-almoco", int item = PosicaoFrango)
    {
        var resultado = Casa(banco, dono.Id).OpcoesTroca(chave, item).Result;
        Assert.True(resultado is OkObjectResult, $"{Status(resultado)} {Json((resultado as ObjectResult)?.Value)}");
        return Assert.IsType<OpcoesTrocaCasaResponse>(((OkObjectResult)resultado).Value);
    }

    private static List<OpcaoTrocaDto> Todas(EscopoTrocaDto escopo) => escopo.DasPreferencias.Concat(escopo.Parecidos).Concat(escopo.VerMais).ToList();

    private static async Task<IActionResult> TrocarNaCasa(ApplicationDbContext banco, Usuario quem, string novo, string escopo = "refeicao",
        string chave = "seg-almoco", int item = PosicaoFrango, string atual = Frango)
    {
        var semana = await banco.SemanasCasa.AsNoTracking().OrderByDescending(s => s.Id).FirstAsync();
        return await Casa(banco, quem.Id).TrocarNaCasa(new TrocaCasaRequest
        {
            SemanaId = semana.Id, Revisao = semana.Revisao, ChaveCasa = chave, Item = item,
            AlimentoIdAtual = atual, AlimentoIdNovo = novo, Escopo = escopo
        });
    }

    // ---------- Pedidos ----------

    [Fact]
    public async Task Pedido_Nao_Guarda_Autor_No_Banco_Nem_Em_Nenhuma_Resposta()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        var semanaId = SemanaDe(banco, b).SemanaId;

        var deB = await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana, SemanaId = semanaId });
        var deC = await Casa(banco, c.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = semanaId, ChaveCasa = "qua-jantar", Item = 1 });

        // A tabela não tem coluna de pessoa: nem UsuarioId, nem autor, nem nada derivado.
        var colunas = banco.Model.FindEntityType(typeof(PedidoCasa))!.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Equal(new HashSet<string> { "Id", "CasaId", "SemanaCasaId", "Tipo", "ChavesCasa", "Item", "AlimentoId", "CriadoEm" }, colunas);
        Assert.Equal(2, await banco.PedidosCasa.CountAsync());

        // A resposta ao membro é só a semana (a mesma para os dois).
        foreach (var resposta in new[] { deB, deC })
        {
            var aceito = Assert.IsType<AcceptedResult>(resposta);
            Assert.Equal($"{{\"semanaId\":{semanaId}}}", Json(aceito.Value));
        }

        // O dono vê os dois, campo a campo, sem nada de quem pediu (nem data).
        var casa = CasaDe(banco, a);
        Assert.Equal(2, casa.PedidosAbertos);
        var json = Json(casa.Pedidos);
        using var doc = JsonDocument.Parse(json);
        foreach (var pedido in doc.RootElement.EnumerateArray())
        {
            Assert.Equal(new[] { "id", "tipo", "semanaId", "chavesCasa", "item", "alimentoId", "alimento", "texto" },
                pedido.EnumerateObject().Select(p => p.Name).ToArray());
        }
        foreach (var proibido in new[] { "Bia", "Caio", "Sobrenome", "exemplo.test", "usuario", "autor", "criado", "membro" })
        {
            Assert.DoesNotContain(proibido, json, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains(casa.Pedidos!, p => p.Texto == "Alguém pediu uma semana nova.");
        Assert.Contains(casa.Pedidos!, p => p.Texto == $"Alguém pediu trocar {char.ToLowerInvariant(Item("ovo-de-galinha-cozido")[0])}{Item("ovo-de-galinha-cozido")[1..]} no jantar de quarta.");
    }

    [Fact]
    public async Task Contador_E_Pedidos_Sao_So_Do_Dono()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana });

        Assert.Equal(1, CasaDe(banco, a).PedidosAbertos);
        foreach (var membro in new[] { b, c })
        {
            var deMembro = CasaDe(banco, membro);
            Assert.Null(deMembro.Pedidos);
            Assert.Null(deMembro.PedidosAbertos);
        }

        // O dono não pede (ele gera ou troca direto), e quem não é da casa recebe 404.
        Assert.Equal(400, Status(await Casa(banco, a.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana })));
        var fora = await Pessoa(banco, "fora-e5@exemplo.test", "Fora Sobrenomeficticio", PerfilA);
        Assert.Equal(404, Status(await Casa(banco, fora.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana })));
        // Membro não dispensa.
        var id = CasaDe(banco, a).Pedidos!.Single().Id;
        Assert.Equal(403, Status(await Casa(banco, b.Id).DispensarPedido(id)));
    }

    [Fact]
    public async Task Pedido_Igual_Nao_Duplica_Dispensar_Apaga_E_Semana_Nova_Atende_Todos()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        var semanaId = SemanaDe(banco, b).SemanaId;

        // O mesmo pedido de duas pessoas: uma linha só, e a mesma resposta (ninguém descobre que outro já pediu).
        var primeira = Json(Assert.IsType<AcceptedResult>(await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana, SemanaId = semanaId })).Value);
        var segunda = Json(Assert.IsType<AcceptedResult>(await Casa(banco, c.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana, SemanaId = semanaId })).Value);
        Assert.Equal(primeira, segunda);
        Assert.Equal(1, await banco.PedidosCasa.CountAsync());

        // Erros sem dizer nada de ninguém: refeição que não existe, item fora da refeição, semana velha, tipo inválido.
        Assert.Equal(400, Status(await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = semanaId, ChaveCasa = "xyz-almoco", Item = 0 })));
        Assert.Equal(400, Status(await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = semanaId, ChaveCasa = "seg-almoco", Item = 99 })));
        Assert.Equal(409, Status(await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = semanaId + 100, ChaveCasa = "seg-almoco", Item = 0 })));
        Assert.Equal(409, Status(await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = semanaId, ChaveCasa = "seg-almoco", Item = 0, AlimentoId = "pao-frances" })));
        Assert.Equal(400, Status(await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = "outra_coisa" })));

        // Dispensar apaga (sem histórico); o id de outra casa dá 404.
        var id = CasaDe(banco, a).Pedidos!.Single().Id;
        Assert.IsType<NoContentResult>(await Casa(banco, a.Id).DispensarPedido(id));
        Assert.Equal(0, await banco.PedidosCasa.CountAsync());
        Assert.Equal(404, Status(await Casa(banco, a.Id).DispensarPedido(id)));

        // Uma semana nova atende tudo o que estava aberto.
        await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana, SemanaId = semanaId });
        await Casa(banco, c.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = semanaId, ChaveCasa = "seg-almoco", Item = PosicaoFrango });
        Assert.Equal(2, await banco.PedidosCasa.CountAsync());
        var nova = await GerarSemana(banco, a, new GeminiFalso());
        Assert.Equal(0, await banco.PedidosCasa.CountAsync());
        Assert.Equal(0, CasaDe(banco, a).PedidosAbertos);
        // Pedido sobre a semana que saiu: 409.
        Assert.Equal(409, Status(await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana, SemanaId = semanaId })));
        Assert.NotEqual(semanaId, nova.SemanaId);
    }

    [Fact]
    public async Task Casa_Tem_Teto_De_Pedidos_Abertos()
    {
        using var banco = Banco();
        var (_, b, _) = await CasaDeTresComSemana(banco);
        var semanaId = SemanaDe(banco, b).SemanaId;
        var refeicoes = SemanaDe(banco, b).Dias.SelectMany(d => new[] { d.Almoco, d.Jantar }).ToList();
        var pedidos = refeicoes.SelectMany(r => Enumerable.Range(0, r.Alimentos.Count).Select(i => (r.ChaveCasa, i))).ToList();

        var status = new List<int>();
        foreach (var (chave, i) in pedidos.Take(RegrasPedidosCasa.MaximoAbertos + 1))
        {
            status.Add(Status(await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = semanaId, ChaveCasa = chave, Item = i })));
        }

        Assert.All(status.Take(RegrasPedidosCasa.MaximoAbertos), s => Assert.Equal(202, s));
        Assert.Equal(409, status[^1]);
        Assert.Equal(RegrasPedidosCasa.MaximoAbertos, await banco.PedidosCasa.CountAsync());
    }

    // ---------- Troca na casa ----------

    [Fact]
    public async Task Troca_Do_Dono_Vale_Para_Todos_Com_A_Porcao_De_Cada_Um_Refeita()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = SemanaDe(banco, b).SemanaId, ChaveCasa = "seg-almoco", Item = PosicaoFrango });
        var revisoesAntes = new[] { a, b, c }.Select(p => LinhaAtual(banco, p).Revisao).ToList();
        var opcoes = Opcoes(banco, a);
        var escolhida = Todas(opcoes.Refeicao).First();

        var resultado = await TrocarNaCasa(banco, a, escolhida.AlimentoId);

        var semana = Assert.IsType<SemanaCasaResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(1, semana.Revisao);
        Assert.True(semana.PodeDesfazer);
        Assert.Equal([PosicaoFrango], semana.Dias[0].Almoco.Trocados);
        var porcoes = new[] { a, b, c }.Select(p => (Pessoa: p, Dieta: DietaAtual(banco, p))).ToList();
        foreach (var (pessoa, dieta) in porcoes)
        {
            var almoco = RefeicaoDaCasa(dieta, "seg-almoco");
            Assert.True(almoco.DaCasa);
            Assert.Equal(escolhida.AlimentoId, almoco.Alimentos[PosicaoFrango].AlimentoId);
            // As outras refeições com frango (terça em diante) não mudaram: o escopo foi a refeição.
            Assert.Equal(Frango, RefeicaoDaCasa(dieta, "ter-almoco").Alimentos[PosicaoFrango].AlimentoId);
            // A porção é a da pessoa: perto do alvo dela, e o total do dia somado de novo.
            var dia = dieta.Dias.Single(d => d.Refeicoes.Contains(almoco));
            var alvo = PorcoesCasa.Alvo(dia.MetaCalculada, "seg-almoco").Calorias;
            Assert.InRange(almoco.Macros.Calorias, alvo * 0.90m, alvo * 1.10m);
            Assert.Equal(dia.Refeicoes.Sum(r => r.Macros.Calorias), dia.TotalDoDia.Calorias);
        }
        // A mesma panela, com quantidades diferentes...
        Assert.Equal(3, porcoes.Select(p => RefeicaoDaCasa(p.Dieta, "seg-almoco").Alimentos[PosicaoFrango].Quantidade).Distinct().Count());
        // ...e a porção do dono é a que a tela de opções mostrou.
        Assert.Equal(escolhida.Quantidade, RefeicaoDaCasa(porcoes[0].Dieta, "seg-almoco").Alimentos[PosicaoFrango].Quantidade);
        // A revisão de cada dieta subiu (uma tela aberta recarrega) e o pedido de troca daquele item foi atendido.
        Assert.Equal(revisoesAntes.Select(r => r + 1), new[] { a, b, c }.Select(p => LinhaAtual(banco, p).Revisao));
        Assert.Equal(0, await banco.PedidosCasa.CountAsync());
        // O membro não vê a marca de troca nem o desfazer.
        var doMembro = SemanaDe(banco, b);
        Assert.Null(doMembro.PodeDesfazer);
        Assert.Null(doMembro.Dias[0].Almoco.Trocados);
        // A tela velha (revisão 0) recebe 409.
        Assert.Equal(409, Status(await Casa(banco, a.Id).TrocarNaCasa(new TrocaCasaRequest
        {
            SemanaId = semana.SemanaId, Revisao = 0, ChaveCasa = "ter-almoco", Item = PosicaoFrango,
            AlimentoIdAtual = Frango, AlimentoIdNovo = escolhida.AlimentoId, Escopo = "refeicao"
        })));
    }

    [Fact]
    public async Task Troca_Na_Semana_Muda_Todas_As_Refeicoes_Da_Casa_Com_O_Item()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTresComSemana(banco);
        var escolhida = Todas(Opcoes(banco, a).Semana).First();

        Assert.IsType<OkObjectResult>(await TrocarNaCasa(banco, a, escolhida.AlimentoId, escopo: "semana"));

        var dieta = DietaAtual(banco, b);
        Assert.All(dieta.Dias.SelectMany(d => d.Refeicoes).Where(r => r.ChaveCasa!.EndsWith("-almoco")),
            r => Assert.Equal(escolhida.AlimentoId, r.Alimentos[PosicaoFrango].AlimentoId));
        Assert.DoesNotContain(dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos), x => x.AlimentoId == Frango);
    }

    [Fact]
    public async Task Substituto_Respeita_O_Nao_Como_E_As_Alergias_De_Todos()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        Alergia(banco, b, "peixe");
        NaoComo(banco, c, "patinho");

        var opcoes = Opcoes(banco, a);

        foreach (var escopo in new[] { opcoes.Refeicao, opcoes.Semana })
        {
            Assert.NotEmpty(Todas(escopo));
            Assert.DoesNotContain(Todas(escopo), o => o.AlimentoId == "patinho-bovino-grelhado");
            Assert.DoesNotContain(Todas(escopo), o => (BaseNutricional.Itens.Single(i => i.Id == o.AlimentoId).Alergenos & Alergenos.Peixe) != Alergenos.Nenhum);
        }
        // Uma contagem só para o que a casa não come, sem separar a alergia (seria dado de saúde de alguém).
        Assert.True(opcoes.ForaPelasRestricoesDaCasa > 0);
        var json = Json(opcoes);
        foreach (var proibido in new[] { "alerg", "peixe\"", "patinho", "Bia", "Caio", "naoComo" })
        {
            Assert.DoesNotContain(proibido, json, StringComparison.OrdinalIgnoreCase);
        }

        // Mandar o substituto mesmo assim: recusado, nada gravado.
        Assert.Equal(400, Status(await TrocarNaCasa(banco, a, "file-de-tilapia-grelhado")));
        Assert.Equal(400, Status(await TrocarNaCasa(banco, a, "patinho-bovino-grelhado")));
        Assert.Equal(0, (await banco.SemanasCasa.SingleAsync()).Revisao);
        Assert.Equal(Frango, RefeicaoDaCasa(DietaAtual(banco, b), "seg-almoco").Alimentos[PosicaoFrango].AlimentoId);
    }

    [Fact]
    public async Task Desfazer_E_Voltar_Ao_Original_Valem_Para_Todos()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        var antes = new[] { a, b, c }.Select(p => Json(RefeicaoDaCasa(DietaAtual(banco, p), "seg-almoco"))).ToList();
        var escolhida = Todas(Opcoes(banco, a).Refeicao).First();
        Assert.IsType<OkObjectResult>(await TrocarNaCasa(banco, a, escolhida.AlimentoId));

        // Membro não desfaz.
        Assert.Equal(403, Status(await Casa(banco, b.Id).DesfazerNaCasa(1, 1)));
        var desfeita = Assert.IsType<SemanaCasaResponse>(Assert.IsType<OkObjectResult>(await Casa(banco, a.Id).DesfazerNaCasa(SemanaDe(banco, a).SemanaId, 1)).Value);
        Assert.Null(desfeita.PodeDesfazer);
        // A porção de cada um volta exatamente à de antes (a mesma conta sobre a mesma referência).
        Assert.Equal(antes, new[] { a, b, c }.Select(p => Json(RefeicaoDaCasa(DietaAtual(banco, p), "seg-almoco"))));
        Assert.Equal(409, Status(await Casa(banco, a.Id).DesfazerNaCasa(desfeita.SemanaId, desfeita.Revisao)));

        // Troca de novo e volta ao original pelo item.
        Assert.IsType<OkObjectResult>(await TrocarNaCasa(banco, a, escolhida.AlimentoId));
        var semana = SemanaDe(banco, a);
        Assert.IsType<OkObjectResult>(await Casa(banco, a.Id).VoltarAoOriginalNaCasa(semana.SemanaId, semana.Revisao, "seg-almoco", PosicaoFrango, "refeicao"));
        Assert.All(new[] { a, b, c }, p => Assert.Equal(Frango, RefeicaoDaCasa(DietaAtual(banco, p), "seg-almoco").Alimentos[PosicaoFrango].AlimentoId));
        semana = SemanaDe(banco, a);
        Assert.Equal(400, Status(await Casa(banco, a.Id).VoltarAoOriginalNaCasa(semana.SemanaId, semana.Revisao, "seg-almoco", PosicaoFrango, "refeicao")));
    }

    [Fact]
    public async Task Desfazer_Nao_Traz_De_Volta_O_Que_Alguem_Marcou_Depois()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTresComSemana(banco);
        var escolhida = Todas(Opcoes(banco, a).Refeicao).First();
        Assert.IsType<OkObjectResult>(await TrocarNaCasa(banco, a, escolhida.AlimentoId));
        // Depois da troca, a B passa a não comer frango: voltar ao frango não serve mais para a casa.
        NaoComo(banco, b, "frango");

        var resultado = await Casa(banco, a.Id).DesfazerNaCasa(SemanaDe(banco, a).SemanaId, 1);

        Assert.Equal(422, Status(resultado));
        var json = Json(((ObjectResult)resultado).Value);
        Assert.Contains(TrocaNaCasa.MotivoRestricaoDaCasa, json);
        Assert.DoesNotContain("Bia", json);
        Assert.Equal(1, (await banco.SemanasCasa.SingleAsync()).Revisao);
    }

    [Fact]
    public async Task Membro_Nao_Troca_Item_Da_Casa_Nem_Pela_Troca_Individual()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTresComSemana(banco);

        // Rotas da troca na casa: só o dono.
        Assert.Equal(403, Status(await Casa(banco, b.Id).OpcoesTroca("seg-almoco", PosicaoFrango)));
        Assert.Equal(403, Status(await TrocarNaCasa(banco, b, "patinho-bovino-grelhado")));

        // Troca individual num item da refeição da casa: recusada (era a pendência da etapa 4), para o membro e para o dono.
        foreach (var pessoa in new[] { b, a })
        {
            var linha = LinhaAtual(banco, pessoa);
            var dieta = DietaAtual(banco, pessoa);
            var refeicao = dieta.Dias[0].Refeicoes.FindIndex(r => r.DaCasa);
            var item = dieta.Dias[0].Refeicoes[refeicao].Alimentos[0];

            var opcoes = await Trocas(banco, pessoa.Id).Opcoes(0, refeicao, 0);
            var trocar = await Trocas(banco, pessoa.Id).Trocar(new TrocaRequest
            {
                DietaId = linha.Id, Revisao = linha.Revisao, Dia = 0, Refeicao = refeicao, Item = 0, AlimentoIdAtual = item.AlimentoId,
                AlimentoIdNovo = "arroz-integral-cozido", Escopo = "refeicao"
            });

            foreach (var resultado in new[] { opcoes, trocar })
            {
                Assert.Equal(409, Status(resultado));
                Assert.Contains(TrocasController.MotivoRefeicaoDaCasa, Json(((ObjectResult)resultado).Value));
            }
            Assert.Equal(linha.Revisao, LinhaAtual(banco, pessoa).Revisao);
        }
    }

    [Fact]
    public void Troca_Individual_Na_Semana_Nao_Cruza_A_Refeicao_Da_Casa()
    {
        // O mesmo arroz no café (individual) e no almoço (da casa): o "na semana toda" do café não chega ao almoço.
        RefeicaoDto Refeicao(string nome, bool daCasa) => new()
        {
            Nome = nome, DaCasa = daCasa, ChaveCasa = daCasa ? "seg-almoco" : null,
            Macros = new MacrosDto { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 },
            Alimentos = [new AlimentoDto { Nome = Item("arroz-branco-cozido"), Quantidade = 100, Unidade = "g", AlimentoId = "arroz-branco-cozido" }]
        };
        var dias = new List<DiaDietaDto>
        {
            new()
            {
                DiaSemana = "Segunda", Refeicoes = [Refeicao("Café da manhã", false), Refeicao("Almoço", true)],
                TotalDoDia = new MacrosDto { Calorias = 256, ProteinasG = 5, CarboidratosG = 56, GordurasG = 0 },
                MetaCalculada = new MacrosDto { Calorias = 256, ProteinasG = 5, CarboidratosG = 56, GordurasG = 0 }
            }
        };
        var lista = new ListaAlimentos(BaseNutricional.Itens.Select(i => new ItemDaLista(i, i.Nome)).ToList(), [], MinimosPorPapel.Conferir(BaseNutricional.Itens));

        var r = CalculadoraTroca.Calcular(dias, new PosicaoItem(0, 0, 0), lista);

        Assert.Equal(1, r.OcorrenciasNaSemana);
    }

    // ---------- Entrada no meio da semana ----------

    [Fact]
    public async Task Quem_Entra_No_Meio_Da_Semana_Recebe_As_Porcoes_Sem_Ia()
    {
        using var banco = Banco();
        var a = await Pessoa(banco, "ana-e5@exemplo.test", "Ana Sobrenomeficticio", PerfilA);
        var b = await Pessoa(banco, "bia-e5@exemplo.test", "Bia Sobrenomeficticio", PerfilB);
        await MontarCasa(banco, a, b);
        await GerarSemana(banco, a, new GeminiFalso());
        var d = await Pessoa(banco, "dani-e5@exemplo.test", "Dani Sobrenomeficticio", PerfilC);
        var gemini = new GeminiFalso();

        var token = await Convite(banco, a);
        Assert.IsType<OkObjectResult>(await Casa(banco, d.Id).AceitarConvite(new AceitarConviteRequest { Token = token, VersaoConsentimento = VersaoCasa }, Geracao(banco, gemini)));

        Assert.Equal(0, gemini.Chamadas);
        var linha = LinhaAtual(banco, d);
        Assert.Equal((DietaIA.OrigemCasa, (int?)(await banco.SemanasCasa.SingleAsync()).Id), (linha.Origem, linha.SemanaCasaId));
        var dieta = DietaAtual(banco, d);
        foreach (var dia in dieta.Dias)
        {
            Assert.Equal(2, dia.Refeicoes.Count(r => r.DaCasa));
            foreach (var refeicao in dia.Refeicoes.Where(r => r.DaCasa))
            {
                var alvo = PorcoesCasa.Alvo(dia.MetaCalculada, refeicao.ChaveCasa!).Calorias;
                Assert.InRange(refeicao.Macros.Calorias, alvo * 0.90m, alvo * 1.10m);
            }
        }
        // Tudo serviu: nenhum aviso, e a cota de gerações da pessoa não foi gasta (Origem = casa).
        Assert.Equal(0, CasaDe(banco, a).PedidosAbertos);
        Assert.False(SemanaDe(banco, a).RefeicaoIndividual);
        Assert.Null(await LimiteGeracoes.ConferirAsync(banco, d.Id, ehDonoDeCasa: false, DateTime.UtcNow));
    }

    [Fact]
    public async Task Quem_Entra_E_Nao_Come_Um_Item_Fica_Com_A_Refeicao_Individual_E_O_Dono_Ve_Aviso_Sem_Nome()
    {
        using var banco = Banco();
        var a = await Pessoa(banco, "ana-e5@exemplo.test", "Ana Sobrenomeficticio", PerfilA);
        var b = await Pessoa(banco, "bia-e5@exemplo.test", "Bia Sobrenomeficticio", PerfilB);
        await MontarCasa(banco, a, b);
        // O jantar de quinta tem tilápia; os outros, o jantar padrão.
        await GerarSemana(banco, a, new GeminiFalso { Jantar = dia => dia == "Quinta-feira" ? JantarComTilapia : JantarPadrao });
        // A Dani tinha a própria dieta (com almoço e jantar dela) e tem alergia a peixe.
        var d = await Pessoa(banco, "dani-e5@exemplo.test", "Dani Sobrenomeficticio", PerfilC);
        Alergia(banco, d, "peixe");
        Assert.IsType<OkObjectResult>(await Dietas(banco, d.Id, new GeminiFalso { AlmocoNaIndividual = true, JantarNaIndividual = true }).Gerar());
        var gemini = new GeminiFalso();

        var token = await Convite(banco, a);
        Assert.IsType<OkObjectResult>(await Casa(banco, d.Id).AceitarConvite(new AceitarConviteRequest { Token = token, VersaoConsentimento = VersaoCasa }, Geracao(banco, gemini)));

        Assert.Equal(0, gemini.Chamadas);
        var dieta = DietaAtual(banco, d);
        var quinta = dieta.Dias.Single(x => x.DiaSemana == "Quinta-feira");
        // Na quinta, o jantar da casa (com tilápia) não entra: fica o jantar que ela já tinha, individual.
        Assert.DoesNotContain(quinta.Refeicoes, r => r.ChaveCasa == "qui-jantar");
        var jantar = Assert.Single(quinta.Refeicoes, r => r.Nome.Contains("Jant"));
        Assert.False(jantar.DaCasa);
        Assert.DoesNotContain(jantar.Alimentos, x => x.AlimentoId!.Contains("tilapia"));
        Assert.Single(quinta.Refeicoes, r => r.DaCasa);
        // Nos outros dias, as duas são da casa (o jantar individual dela não fica duplicado).
        Assert.All(dieta.Dias.Where(x => x.DiaSemana != "Quinta-feira"), x =>
        {
            Assert.Equal(2, x.Refeicoes.Count(r => r.DaCasa));
            Assert.Single(x.Refeicoes, r => r.Nome.Contains("Jant"));
        });

        // O dono vê o aviso, sem nome e sem dizer qual item nem por quê; o membro não vê nada.
        var casa = CasaDe(banco, a);
        var aviso = Assert.Single(casa.Pedidos!);
        Assert.Equal(PedidoCasa.TipoAvisoEntrada, aviso.Tipo);
        Assert.Equal(["qui-jantar"], aviso.ChavesCasa);
        Assert.Equal("Alguém que entrou esta semana não come um item do jantar de quinta. Essa refeição ficou individual para essa pessoa; uma semana nova resolve.", aviso.Texto);
        Assert.Null(aviso.AlimentoId);
        var json = Json(casa.Pedidos);
        foreach (var proibido in new[] { "Dani", "Sobrenome", "peixe", "tilapia", "tilápia", "alerg" })
        {
            Assert.DoesNotContain(proibido, json, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Null(CasaDe(banco, b).Pedidos);
        Assert.True(SemanaDe(banco, a).RefeicaoIndividual);
        Assert.Null(SemanaDe(banco, b).RefeicaoIndividual);
    }

    // ---------- Saída e remoção ----------

    [Fact]
    public async Task Quem_Sai_Fica_Com_As_Refeicoes_Sem_A_Marca_Da_Casa()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        var antes = DietaAtual(banco, b);
        var revisao = LinhaAtual(banco, b).Revisao;

        Assert.IsType<NoContentResult>(await Casa(banco, b.Id).Sair());

        ConferirSoltas(banco, b, antes, revisao);
        Assert.False(await banco.MembrosCasa.AnyAsync(m => m.UsuarioId == b.Id));
        // A troca na casa depois disso não mexe na dieta de quem saiu, e a lista da casa já é sem ela.
        Alergia(banco, b, "peixe");
        Assert.Contains(Todas(Opcoes(banco, a).Refeicao).Concat(Todas(Opcoes(banco, a).Semana)), o => (BaseNutricional.Itens.Single(i => i.Id == o.AlimentoId).Alergenos & Alergenos.Peixe) != Alergenos.Nenhum);
        var escolhida = Todas(Opcoes(banco, a).Refeicao).First();
        Assert.IsType<OkObjectResult>(await TrocarNaCasa(banco, a, escolhida.AlimentoId));
        Assert.Equal(Frango, RefeicaoDaCasa2(DietaAtual(banco, b), 0).Alimentos[PosicaoFrango].AlimentoId);
        Assert.Equal(escolhida.AlimentoId, RefeicaoDaCasa(DietaAtual(banco, c), "seg-almoco").Alimentos[PosicaoFrango].AlimentoId);
        // E a troca individual volta a valer no almoço dela.
        var dieta = DietaAtual(banco, b);
        var refeicao = dieta.Dias[0].Refeicoes.FindIndex(r => r.Nome == "Almoço");
        Assert.IsType<OkObjectResult>(await Trocas(banco, b.Id).Opcoes(0, refeicao, PosicaoFrango));
    }

    [Fact]
    public async Task Quem_E_Removido_Tambem_Fica_Com_As_Refeicoes_Sem_A_Marca_Da_Casa()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTresComSemana(banco);
        var antes = DietaAtual(banco, b);
        var revisao = LinhaAtual(banco, b).Revisao;
        var membroId = CasaDe(banco, a).Membros.Single(m => m.PrimeiroNome == "Bia").MembroId;

        Assert.IsType<NoContentResult>(await Casa(banco, a.Id).RemoverMembro(membroId));

        ConferirSoltas(banco, b, antes, revisao);
        Assert.Equal(404, Status(await Casa(banco, b.Id).ObterSemana()));
    }

    // As mesmas comidas e porções, sem DaCasa, sem ChaveCasa, sem SemanaCasaId, com a revisão nova.
    private static void ConferirSoltas(ApplicationDbContext banco, Usuario pessoa, DietaSemanalDto antes, int revisaoAntes)
    {
        var depois = DietaAtual(banco, pessoa);
        var linha = LinhaAtual(banco, pessoa);
        Assert.Null(linha.SemanaCasaId);
        Assert.Equal(revisaoAntes + 1, linha.Revisao);
        Assert.All(depois.Dias.SelectMany(d => d.Refeicoes), r =>
        {
            Assert.False(r.DaCasa);
            Assert.Null(r.ChaveCasa);
        });
        string Itens(DietaSemanalDto d) => string.Join("|", d.Dias.SelectMany(x => x.Refeicoes).Select(r => r.Nome + ":" + string.Join(",", r.Alimentos.Select(x => $"{x.AlimentoId}={x.Quantidade}"))));
        Assert.Equal(Itens(antes), Itens(depois));
        Assert.DoesNotContain("daCasa", linha.ConteudoJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chaveCasa", linha.ConteudoJson, StringComparison.OrdinalIgnoreCase);
    }

    // O almoço do dia (pela posição, sem a chave da casa, que saiu).
    private static RefeicaoDto RefeicaoDaCasa2(DietaSemanalDto dieta, int dia) => dieta.Dias[dia].Refeicoes.Single(r => r.Nome == "Almoço");

    // ---------- Transferência ----------

    [Fact]
    public async Task Transferencia_Aceitar_Recusar_Cancelar_E_Ultimo_Apaga_A_Casa()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        var membroB = CasaDe(banco, a).Membros.Single(m => m.PrimeiroNome == "Bia").MembroId;
        var membroC = CasaDe(banco, a).Membros.Single(m => m.PrimeiroNome == "Caio").MembroId;

        // Com membros, o dono não sai sem passar a casa.
        Assert.Equal(409, Status(await Casa(banco, a.Id).Sair()));

        // Oferece à B: o dono vê "Aguardando Bia aceitar" (a pendência na linha dela); a B recusa.
        Assert.IsType<NoContentResult>(await Casa(banco, a.Id).OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroB }));
        Assert.True(CasaDe(banco, a).Membros.Single(m => m.MembroId == membroB).TransferenciaPendente);
        Assert.IsType<NoContentResult>(await Casa(banco, b.Id).CancelarTransferencia());
        Assert.Equal(409, Status(await Casa(banco, b.Id).AceitarTransferencia()));

        // Oferece ao C e cancela: o C não consegue mais aceitar.
        Assert.IsType<NoContentResult>(await Casa(banco, a.Id).OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroC }));
        Assert.IsType<NoContentResult>(await Casa(banco, a.Id).CancelarTransferencia());
        Assert.Equal(409, Status(await Casa(banco, c.Id).AceitarTransferencia()));

        // Oferece à B de novo, ela aceita: vira dona, vê os pedidos, e a Ana vira membro (e pode pedir).
        await Casa(banco, c.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana });
        Assert.IsType<NoContentResult>(await Casa(banco, a.Id).OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroB }));
        Assert.IsType<OkObjectResult>(await Casa(banco, b.Id).AceitarTransferencia());
        Assert.True(CasaDe(banco, b).SouDono);
        Assert.Equal(1, CasaDe(banco, b).PedidosAbertos);
        Assert.Null(CasaDe(banco, a).Pedidos);
        Assert.Equal(202, Status(await Casa(banco, a.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = SemanaDe(banco, a).SemanaId, ChaveCasa = "seg-almoco", Item = 0 })));
        Assert.Equal(403, Status(await Casa(banco, a.Id).OpcoesTroca("seg-almoco", 0)));
        Assert.IsType<OkObjectResult>(Casa(banco, b.Id).OpcoesTroca("seg-almoco", PosicaoFrango).Result);

        // Todos saem; o último (a dona) leva a casa, as semanas e os pedidos junto.
        Assert.IsType<NoContentResult>(await Casa(banco, a.Id).Sair());
        Assert.IsType<NoContentResult>(await Casa(banco, c.Id).Sair());
        Assert.IsType<NoContentResult>(await Casa(banco, b.Id).Sair());
        Assert.Equal(0, await banco.Casas.CountAsync());
        Assert.Equal(0, await banco.SemanasCasa.CountAsync());
        Assert.Equal(0, await banco.PedidosCasa.CountAsync());
        Assert.All(new[] { a, b, c }, p => Assert.Null(LinhaAtual(banco, p).SemanaCasaId));
    }

    // ---------- Vazamento e retenção ----------

    [Fact]
    public async Task Opcoes_Da_Casa_Levam_So_A_Porcao_Do_Dono_Campo_A_Campo()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);

        var opcoes = Opcoes(banco, a);

        var json = Json(opcoes);
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
            "semanaId", "revisao", "chaveCasa", "item", "original", "alimentoId", "nome", "quantidade", "unidade", "kcal", "nutrienteG",
            "nutriente", "ocorrenciasNaSemana", "foraPelasRestricoesDaCasa", "refeicao", "semana", "dasPreferencias", "parecidos",
            "verMais", "fora", "mensagemSemOpcoes", "contagem", "difKcal", "outroUso", "ocorrencias", "dias", "dia", "diaSemana",
            "totalAntes", "totalDepois", "meta", "desvioPct",
            // As chaves do "fora" (motivos, sem dizer de quem).
            "usoDiferente", "jaNaRefeicao", "refogadoComOleo", "porcaoMaxima", "foraDaMeta", TrocaNaCasa.MotivoNaoServeParaACasa
        };
        Assert.All(campos, campo => Assert.Contains(campo, permitidos));

        // O original e a prévia são os do dono: a porção dele, o total do dia dele e a meta dele; nada da B ou do C.
        var doDono = DietaAtual(banco, a);
        Assert.Equal(RefeicaoDaCasa(doDono, "seg-almoco").Alimentos[PosicaoFrango].Quantidade, opcoes.Original.Quantidade);
        var segunda = doDono.Dias.Single(d => d.DiaSemana == "Segunda-feira");
        Assert.All(Todas(opcoes.Refeicao), o =>
        {
            var previa = Assert.Single(o.Dias);
            Assert.Equal(segunda.MetaCalculada.Calorias, previa.Meta);
            Assert.Equal(segunda.TotalDoDia.Calorias, previa.TotalAntes);
        });
        foreach (var outro in new[] { b, c })
        {
            var dia = DietaAtual(banco, outro).Dias.Single(d => d.DiaSemana == "Segunda-feira");
            Assert.DoesNotContain(Todas(opcoes.Refeicao).SelectMany(o => o.Dias), p => p.Meta == dia.MetaCalculada.Calorias || p.TotalAntes == dia.TotalDoDia.Calorias);
        }
        foreach (var proibido in new[] { "Bia", "Caio", "Sobrenome", "exemplo.test", "peso", "objetivo", "alerg", "usuario" })
        {
            Assert.DoesNotContain(proibido, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Respostas_Da_Casa_Ao_Membro_Nao_Levam_Pedidos_Nem_Porcao_De_Ninguem()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        await Casa(banco, c.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana });
        Assert.IsType<OkObjectResult>(await TrocarNaCasa(banco, a, Todas(Opcoes(banco, a).Refeicao).First().AlimentoId));

        var casa = Json(CasaDe(banco, b));
        var semana = Json(SemanaDe(banco, b));

        Assert.Contains("\"pedidos\":null", casa);
        Assert.Contains("\"pedidosAbertos\":null", casa);
        Assert.DoesNotContain("Alguém", casa);
        foreach (var json in new[] { casa, semana })
        {
            foreach (var proibido in new[] { "quantidade", "kcal", "calorias", "meta", "peso", "objetivo", "email", "porc", "alerg", "trocados", "podeDesfazer", "Sobrenome" })
            {
                Assert.DoesNotContain(proibido, json, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task Limpeza_Apaga_Pedido_Esquecido_Ha_Mais_De_30_Dias_E_Exclusao_Do_Dono_Sozinho_Leva_Os_Pedidos()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTresComSemana(banco);
        await Casa(banco, b.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoNovaSemana });
        var antigo = await banco.PedidosCasa.SingleAsync();
        antigo.CriadoEm = DateTime.UtcNow - LimpezaRetencao.PrazoPedidosCasa - TimeSpan.FromDays(1);
        await banco.SaveChangesAsync();
        await Casa(banco, c.Id).Pedir(new PedidoCasaRequest { Tipo = PedidoCasa.TipoTroca, SemanaId = SemanaDe(banco, c).SemanaId, ChaveCasa = "seg-almoco", Item = 0 });

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, DateTime.UtcNow);

        Assert.Equal(1, resultado.PedidosCasaApagados);
        Assert.Equal(PedidoCasa.TipoTroca, (await banco.PedidosCasa.SingleAsync()).Tipo);

        // Os membros saem; a dona sozinha apaga a conta: a casa sai com o pedido que sobrou.
        await Casa(banco, b.Id).Sair();
        await Casa(banco, c.Id).Sair();
        Assert.True(await ExclusaoConta.ApagarAsync(banco, a.Id));
        Assert.Equal(0, await banco.PedidosCasa.CountAsync());
        Assert.Equal(0, await banco.Casas.CountAsync());
    }

    [Fact]
    public async Task Substituto_Que_Nao_Serve_Para_A_Porcao_De_Alguem_Sai_Das_Opcoes_Sem_Dizer_De_Quem()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTresComSemana(banco);

        // O arroz do almoço (posição 0): na referência, vários substitutos passam; refeita a porção de cada um, alguns
        // passam da porção máxima de alguém (a Ana tem a meta alta) e ficam fora como "não serve para a casa".
        var opcoes = Opcoes(banco, a, "seg-almoco", 0);
        var semana = await banco.SemanasCasa.AsNoTracking().SingleAsync();
        var referencia = JsonSerializer.Deserialize<SemanaCasaDto>(semana.ConteudoJson)!;
        var lista = await ListaDaCasa.MontarAsync(banco, await banco.MembrosCasa.Select(m => m.UsuarioId).ToListAsync(), a.Id);
        var calculadas = CalculadoraTroca.Calcular(referencia.Dias, new PosicaoItem(0, 0, 0), lista).Refeicao;
        var naReferencia = calculadas.DasPreferencias.Concat(calculadas.Parecidos).Concat(calculadas.VerMais).Select(o => o.AlimentoId).ToList();
        var naoServem = naReferencia.Except(Todas(opcoes.Refeicao).Select(o => o.AlimentoId)).ToList();

        Assert.NotEmpty(naoServem);
        Assert.Equal(naoServem.Count, opcoes.Refeicao.Fora[TrocaNaCasa.MotivoNaoServeParaACasa]);

        // Mandar um deles mesmo assim: 400 sem nome de ninguém, nada gravado.
        var resultado = await TrocarNaCasa(banco, a, naoServem[0], chave: "seg-almoco", item: 0, atual: "arroz-branco-cozido");
        Assert.Equal(400, Status(resultado));
        var json = Json(((ObjectResult)resultado).Value);
        Assert.DoesNotContain("Ana", json);
        Assert.DoesNotContain("Bia", json);
        Assert.Equal(0, (await banco.SemanasCasa.SingleAsync()).Revisao);
        Assert.Equal("arroz-branco-cozido", RefeicaoDaCasa(DietaAtual(banco, b), "seg-almoco").Alimentos[0].AlimentoId);
    }

    // ---------- Gemini falso ----------

    private static object[] Itens(params (string Id, decimal Q)[] itens) =>
        itens.Select(i => (object)new { nome = Item(i.Id), quantidade = i.Q, unidade = "g" }).ToArray();

    private static readonly object[] AlmocoPadrao = Itens(
        ("arroz-branco-cozido", 150), ("feijao-cozido", 100), (Frango, 120),
        ("alface-crua", 30), ("tomate-cru", 50), ("azeite-de-oliva-extra-virgem", 5));

    private static readonly object[] JantarPadrao = Itens(
        ("macarrao-cozido", 180), ("ovo-de-galinha-cozido", 100), ("brocolis-cozido", 80), ("azeite-de-oliva-extra-virgem", 8));

    private static readonly object[] JantarComTilapia = Itens(
        ("batata-inglesa-cozida", 200), ("file-de-tilapia-grelhado", 120), ("alface-crua", 40), ("azeite-de-oliva-extra-virgem", 5));

    /// <summary>
    /// Responde a semana da casa (prompt "para uma casa") ou a semana individual (café da manhã e lanche da tarde; com
    /// AlmocoNaIndividual / JantarNaIndividual, também um almoço e um jantar sem frango nem peixe).
    /// </summary>
    private sealed class GeminiFalso : HttpMessageHandler
    {
        public Func<string, object[]> Almoco { get; init; } = _ => AlmocoPadrao;
        public Func<string, object[]> Jantar { get; init; } = _ => JantarPadrao;
        public bool AlmocoNaIndividual { get; init; }
        public bool JantarNaIndividual { get; init; }

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
                    ? new[] { Refeicao("Almoço", Almoco(n), "12:30"), Refeicao("Jantar", Jantar(n), "19:30") }
                    : Individuais(),
                totalDoDia = new { calorias = 1000, proteinasG = 60, carboidratosG = 120, gordurasG = 30 }
            });

            object[] Individuais()
            {
                var lista = new List<object>
                {
                    Refeicao("Café da manhã", Itens(("iogurte-natural", 170), ("banana-nanica", 100), ("aveia-em-flocos", 30)), "07:30"),
                    Refeicao("Lanche da tarde", Itens(("pao-frances", 50), ("queijo-minas-frescal", 30)), "16:00")
                };
                if (AlmocoNaIndividual)
                {
                    lista.Add(Refeicao("Almoço", Itens(("arroz-branco-cozido", 150), ("feijao-cozido", 100), ("ovo-de-galinha-cozido", 100), ("alface-crua", 30)), "12:30"));
                }
                if (JantarNaIndividual)
                {
                    lista.Add(Refeicao("Jantar", Itens(("macarrao-cozido", 150), ("patinho-bovino-grelhado", 100), ("brocolis-cozido", 80)), "19:30"));
                }
                return lista.ToArray();
            }

            var envelope = JsonSerializer.Serialize(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = JsonSerializer.Serialize(new { dias }) } } }, finishReason = "STOP" } },
                usageMetadata = new { promptTokenCount = 1500, candidatesTokenCount = 4000, totalTokenCount = 5500 }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(envelope, Encoding.UTF8, "application/json") };
        }
    }
}
