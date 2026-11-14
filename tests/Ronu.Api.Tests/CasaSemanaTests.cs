using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

namespace Ronu.Api.Tests;

/// <summary>
/// Plano família, etapa 4: a lista da casa (interseção), a semana da casa (uma chamada ao Gemini, só o dono), as
/// porções de cada um sobre a mesma panela, o café e os lanches de quem está numa casa, o prompt sem dado de ninguém, a
/// lista pobre e o que cada resposta mostra dos outros. Gemini sempre falso; perfis fictícios com e-mails @exemplo.test.
/// </summary>
public class CasaSemanaTests
{
    private static readonly int VersaoCasa = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoCasa);

    // ---------- Apoio: banco, pessoas e casa ----------

    // A geração individual grava pela RepositorioDietaIA, que abre uma transação: o banco em memória só a ignora.
    private static ApplicationDbContext Banco() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private record Perfil(string Sexo, DateOnly Nascimento, decimal Altura, decimal Peso, string Nivel, string Objetivo);

    // Os 3 perfis fictícios do exemplo do passo 0 (sem o treino do A: aqui só importa que as metas sejam diferentes).
    private static readonly Perfil PerfilA = new("Masculino", new DateOnly(1996, 1, 10), 178m, 80m, "em_pe", "manter peso");
    private static readonly Perfil PerfilB = new("Feminino", new DateOnly(1998, 5, 2), 165m, 62m, "sentado", "perder peso");
    private static readonly Perfil PerfilC = new("Masculino", new DateOnly(1981, 2, 3), 175m, 95m, "andando", "perder peso");

    private static async Task<Usuario> Pessoa(ApplicationDbContext banco, string email, string nome, Perfil perfil, string uf = "SP")
    {
        var usuario = new Usuario
        {
            Nome = nome, Email = email, Estado = uf, Altura = perfil.Altura, Sexo = perfil.Sexo,
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

    private static GeradorDietaGemini Gerador(HttpMessageHandler gemini, LoggerQueGuarda<GeradorDietaGemini>? logger = null) => new(
        new HttpClient(gemini), new CalculadoraGastoCalorico(),
        new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
        new GeminiOptions { ApiKey = "teste" }, logger is null ? NullLogger<GeradorDietaGemini>.Instance : logger);

    private static GeracaoSemanaCasa Geracao(ApplicationDbContext banco, HttpMessageHandler gemini) => new(
        banco, new ContextoDietaBuilder(banco, new CalculadoraPesoTendencia()), Gerador(gemini), NullLogger<GeracaoSemanaCasa>.Instance);

    private static DietasController Dietas(ApplicationDbContext banco, int usuarioId, HttpMessageHandler gemini) => new(
        banco, new ContextoDietaBuilder(banco, new CalculadoraPesoTendencia()), Gerador(gemini), new RepositorioDietaIA(banco),
        NullLogger<DietasController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Token(usuarioId) } }
    };

    // A casa com o primeiro como dono e os outros como membros (pelo convite, como na tela).
    private static async Task MontarCasa(ApplicationDbContext banco, Usuario dono, params Usuario[] membros)
    {
        Assert.IsType<CreatedResult>(await Casa(banco, dono.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa }));
        foreach (var membro in membros)
        {
            var criado = Assert.IsType<ConviteCriadoResponse>(Assert.IsType<CreatedResult>(await Casa(banco, dono.Id).CriarConvite()).Value);
            var token = criado.Link.Split("#convite=")[1];
            Assert.IsType<OkObjectResult>(await Casa(banco, membro.Id).AceitarConvite(new AceitarConviteRequest { Token = token, VersaoConsentimento = VersaoCasa }));
        }
    }

    private static async Task<(Usuario A, Usuario B, Usuario C)> CasaDeTres(ApplicationDbContext banco)
    {
        var a = await Pessoa(banco, "ana-casa@exemplo.test", "Ana Sobrenomeficticio", PerfilA);
        var b = await Pessoa(banco, "bia-casa@exemplo.test", "Bia Sobrenomeficticio", PerfilB);
        var c = await Pessoa(banco, "caio-casa@exemplo.test", "Caio Sobrenomeficticio", PerfilC);
        await MontarCasa(banco, a, b, c);
        return (a, b, c);
    }

    private static void NaoComo(ApplicationDbContext banco, Usuario pessoa, string alimento, string tipo = TiposPreferencia.NaoComo)
    {
        banco.PreferenciasAlimentares.Add(new PreferenciaAlimentar { UsuarioId = pessoa.Id, Alimento = alimento, Tipo = tipo });
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

    private static async Task<SemanaCasaResponse> GerarSemana(ApplicationDbContext banco, Usuario dono, HttpMessageHandler gemini)
    {
        var resultado = await Casa(banco, dono.Id).GerarSemana(Geracao(banco, gemini));
        // Na falha, o status e a mensagem vão para a mensagem do teste.
        Assert.True(resultado is OkObjectResult, $"{Status(resultado)} {JsonSerializer.Serialize((resultado as ObjectResult)?.Value)}");
        return Assert.IsType<SemanaCasaResponse>(((OkObjectResult)resultado).Value);
    }

    private static DietaSemanalDto DietaAtual(ApplicationDbContext banco, Usuario pessoa) =>
        JsonSerializer.Deserialize<DietaSemanalDto>(banco.DietasIA.AsNoTracking()
            .Where(d => d.UsuarioId == pessoa.Id).OrderByDescending(d => d.DataGeracao).ThenByDescending(d => d.Id).First().ConteudoJson)!;

    private static string Item(string id) => BaseNutricional.Itens.Single(i => i.Id == id).Nome;

    // ---------- Lista da casa ----------

    [Fact]
    public async Task Nao_Como_De_Um_Tira_Para_Todos_E_Evitar_De_Outro_Fica_Leve()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        NaoComo(banco, b, "frango");
        NaoComo(banco, c, "batata doce", TiposPreferencia.Evitar);

        var lista = await ListaDaCasa.MontarAsync(banco, [a.Id, b.Id, c.Id], a.Id);

        var ids = lista.Itens.Select(i => i.Item.Id).ToHashSet();
        Assert.DoesNotContain("peito-de-frango-sem-pele-grelhado", ids);
        Assert.DoesNotContain(ids, id => id.Contains("frango"));
        // O leve continua na lista (pode aparecer, pouco) e está marcado como leve, sem o texto que a pessoa escreveu.
        Assert.Contains("batata-doce-cozida", ids);
        Assert.Contains("batata-doce-cozida", lista.ItensLeves);
        Assert.All(lista.Preferencias, p => Assert.Equal(string.Empty, p.Alimento));
        // Na lista do A, sozinho, o frango continua: a interseção é da casa, não muda a lista de ninguém.
        var listaDoA = (await ListaDaPessoa.MontarAsync(banco, a.Id)).Lista;
        Assert.Contains(listaDoA.Itens, i => i.Item.Id == "peito-de-frango-sem-pele-grelhado");
    }

    [Fact]
    public async Task Alergia_De_Um_Nunca_Entra_Na_Lista_Da_Casa_Com_A_Regra_Cruzada()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        Alergia(banco, b, "amendoim");
        Alergia(banco, c, "peixe");

        var lista = await ListaDaCasa.MontarAsync(banco, [a.Id, b.Id, c.Id], a.Id);

        Assert.DoesNotContain(lista.Itens, i => (i.Item.Alergenos & (Alergenos.Amendoim | Alergenos.Castanhas | Alergenos.Peixe)) != Alergenos.Nenhum);
        Assert.Equal(Alergenos.Amendoim | Alergenos.Peixe, lista.Alergias);
        // Sem bebida alcoólica nem cafeína no almoço e no jantar da casa.
        Assert.DoesNotContain(lista.Itens, i => i.Item.Grupo is GrupoDeTroca.BebidasAlcoolicas or GrupoDeTroca.CafesEChas);
    }

    [Fact]
    public async Task Gemini_Que_Insiste_No_Alergeno_Da_503_E_Nao_Grava_Nada()
    {
        using var banco = Banco();
        var (a, _, c) = await CasaDeTres(banco);
        Alergia(banco, c, "peixe");
        // As duas tentativas trazem tilápia: fora da lista da casa, a resposta é inválida nas duas, e a alergia nunca é
        // relaxada (nem na última).
        var gemini = new GeminiFalso { Almoco = _ => AlmocoComTilapia };

        var resultado = await Casa(banco, a.Id).GerarSemana(Geracao(banco, gemini));

        Assert.Equal(503, Status(resultado));
        Assert.Equal(2, gemini.ChamadasCasa);
        Assert.Equal(0, await banco.SemanasCasa.CountAsync());
        Assert.Equal(0, await banco.DietasIA.CountAsync());
    }

    // ---------- Quem gera ----------

    [Fact]
    public async Task So_O_Dono_Gera_A_Semana_E_Quem_Nao_E_Da_Casa_Recebe_404()
    {
        using var banco = Banco();
        var (_, b, _) = await CasaDeTres(banco);
        var fora = await Pessoa(banco, "fora-casa@exemplo.test", "Fora Sobrenomeficticio", PerfilA);
        var gemini = new GeminiFalso();

        Assert.Equal(403, Status(await Casa(banco, b.Id).GerarSemana(Geracao(banco, gemini))));
        Assert.Equal(404, Status(await Casa(banco, fora.Id).GerarSemana(Geracao(banco, gemini))));
        Assert.Equal(404, Status(await Casa(banco, fora.Id).ObterSemana()));
        Assert.Equal(0, gemini.Chamadas);
    }

    [Fact]
    public async Task Membro_Ve_A_Semana_Sem_Os_Avisos_Do_Dono_E_Sem_Semana_Recebe_404()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);

        Assert.Equal(404, Status(await Casa(banco, b.Id).ObterSemana()));

        await GerarSemana(banco, a, new GeminiFalso());
        var doMembro = Assert.IsType<SemanaCasaResponse>(Assert.IsType<OkObjectResult>(await Casa(banco, b.Id).ObterSemana()).Value);
        var doDono = Assert.IsType<SemanaCasaResponse>(Assert.IsType<OkObjectResult>(await Casa(banco, a.Id).ObterSemana()).Value);

        Assert.Equal(7, doMembro.Dias.Count);
        Assert.Null(doMembro.RefeicaoIndividual);
        Assert.False(doDono.RefeicaoIndividual);
        Assert.Equal(doDono.Dias.Select(d => d.Almoco.Alimentos.Count), doMembro.Dias.Select(d => d.Almoco.Alimentos.Count));
    }

    // ---------- Porções ----------

    [Fact]
    public async Task A_Mesma_Panela_Da_Porcoes_Diferentes_Perto_Do_Alvo_De_Cada_Um()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        var gemini = new GeminiFalso();

        await GerarSemana(banco, a, gemini);

        Assert.Equal(1, gemini.ChamadasCasa);
        var dietas = new[] { a, b, c }.Select(p => DietaAtual(banco, p)).ToList();
        var semana = await banco.SemanasCasa.SingleAsync();
        foreach (var p in new[] { a, b, c })
        {
            var linha = await banco.DietasIA.SingleAsync(d => d.UsuarioId == p.Id);
            Assert.Equal((DietaIA.OrigemCasa, (int?)semana.Id), (linha.Origem, linha.SemanaCasaId));
            // A meta adaptativa continua lendo a MetaDieta de cada um, com a meta cheia do dia.
            var meta = await banco.MetasDieta.SingleAsync(m => m.UsuarioId == p.Id);
            Assert.Equal(linha.Id, meta.DietaIAId);
        }

        foreach (var chave in new[] { "seg-almoco", "seg-jantar", "qua-almoco", "dom-jantar" })
        {
            var porcoes = dietas.Select(d => d.Dias.SelectMany(x => x.Refeicoes).Single(r => r.ChaveCasa == chave)).ToList();
            Assert.All(porcoes, r => Assert.True(r.DaCasa));
            // Os mesmos alimentos para os três...
            Assert.Single(porcoes.Select(r => string.Join(",", r.Alimentos.Select(x => x.AlimentoId))).Distinct());
            // ...em quantidades diferentes...
            Assert.Equal(3, porcoes.Select(r => string.Join(",", r.Alimentos.Select(x => x.Quantidade))).Distinct().Count());
            for (var i = 0; i < 3; i++)
            {
                var dia = dietas[i].Dias.Single(d => d.Refeicoes.Contains(porcoes[i]));
                var alvo = PorcoesCasa.Alvo(dia.MetaCalculada, chave).Calorias;
                // ...e cada uma perto do alvo da pessoa (a fatia da meta do dia dela). A margem de 10% é a do ajuste
                // quando um item bate na porção máxima da base (no passo 0, o pior caso ficou em −8,9%).
                Assert.InRange(porcoes[i].Macros.Calorias, alvo * 0.90m, alvo * 1.10m);
                Assert.All(porcoes[i].Alimentos, x => Assert.True(x.Quantidade <= BaseNutricional.Itens.Single(it => it.Id == x.AlimentoId).PorcaoMaxima));
            }
        }

        // A B tem a meta menor e o prato menor; o C, entre os dois.
        decimal Kcal(DietaSemanalDto d, string chave) => d.Dias.SelectMany(x => x.Refeicoes).Single(r => r.ChaveCasa == chave).Macros.Calorias;
        Assert.True(Kcal(dietas[1], "seg-almoco") < Kcal(dietas[2], "seg-almoco"));
        Assert.True(Kcal(dietas[2], "seg-almoco") < Kcal(dietas[0], "seg-almoco"));
    }

    [Fact]
    public void Porcao_Escala_Antes_De_Ajustar_Porque_O_Ajuste_Sozinho_Nao_Alcanca()
    {
        // A referência do passo 0 (514 kcal) para alguém com meta de 3.379 kcal: alvo de 1.037 kcal no almoço, 2 vezes
        // a referência. Só com o ajuste de 50% a 150% o prato ficaria ~38% abaixo; com a escala antes, fica a até 10%.
        var referencia = Refeicao("seg-almoco",
            ("arroz-branco-cozido", 150), ("feijao-cozido", 100), ("peito-de-frango-sem-pele-grelhado", 120),
            ("alface-crua", 30), ("tomate-cru", 50), ("azeite-de-oliva-extra-virgem", 5));
        var meta = new MacrosDto { Calorias = 3379, ProteinasG = 144, CarboidratosG = 490, GordurasG = 94 };

        var porcao = PorcoesCasa.Porcao(referencia, meta);

        var alvo = 3379m * PorcoesCasa.FatiaAlmoco;
        Assert.InRange(porcao.Macros.Calorias, alvo * 0.90m, alvo * 1.10m);
        // A referência não muda.
        Assert.Equal(150m, referencia.Alimentos[0].Quantidade);
        // O tomate é contável: o nome sai com a contagem da porção.
        Assert.Contains(porcao.Alimentos, x => x.AlimentoId == "tomate-cru" && x.Nome.Contains("unidade"));
    }

    // ---------- Café e lanches ----------

    [Fact]
    public async Task Membro_Gera_So_Cafe_E_Lanches_Com_O_Que_Sobra_Da_Meta()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);
        await GerarSemana(banco, a, new GeminiFalso());
        var daCasaAntes = DietaAtual(banco, b).Dias.SelectMany(d => d.Refeicoes).Where(r => r.DaCasa).ToList();
        var gemini = new GeminiFalso();

        var resultado = await Dietas(banco, b.Id, gemini).Gerar();

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(1, gemini.ChamadasIndividuais);
        Assert.Equal(0, gemini.ChamadasCasa);
        var prompt = gemini.Prompts.Single();
        Assert.Contains("monte: café da manhã e lanches", prompt);
        Assert.Contains("já descontam o almoço e o jantar", prompt);

        var dieta = DietaAtual(banco, b);
        var linha = banco.DietasIA.Where(d => d.UsuarioId == b.Id).OrderByDescending(d => d.Id).First();
        Assert.Equal(DietaIA.OrigemPessoa, linha.Origem);
        Assert.NotNull(linha.SemanaCasaId);
        foreach (var dia in dieta.Dias)
        {
            // O almoço e o jantar são os da casa (um de cada), e o resto é individual.
            Assert.Equal(2, dia.Refeicoes.Count(r => r.DaCasa));
            Assert.Single(dia.Refeicoes, r => r.Nome == "Almoço");
            Assert.Single(dia.Refeicoes, r => r.Nome == "Jantar");
            Assert.Contains(dia.Refeicoes, r => !r.DaCasa && r.Nome == "Café da manhã");
            // Em ordem de horário.
            Assert.Equal(dia.Refeicoes.Select(r => r.Horario).OrderBy(h => h), dia.Refeicoes.Select(r => r.Horario));
        }
        // As porções da casa são as mesmas da dieta que a casa criou (a meta do dia não mudou).
        var daCasaDepois = dieta.Dias.SelectMany(d => d.Refeicoes).Where(r => r.DaCasa).ToList();
        Assert.Equal(daCasaAntes.Select(r => (r.ChaveCasa, Itens: string.Join(",", r.Alimentos.Select(x => $"{x.AlimentoId}:{x.Quantidade}")))),
            daCasaDepois.Select(r => (r.ChaveCasa, Itens: string.Join(",", r.Alimentos.Select(x => $"{x.AlimentoId}:{x.Quantidade}")))));
    }

    [Fact]
    public async Task Almoco_Na_Resposta_Individual_De_Quem_Esta_Na_Casa_E_Recusado_Na_Primeira()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);
        await GerarSemana(banco, a, new GeminiFalso());
        // As duas respostas trazem um almoço em todo dia: a primeira é recusada; na última, o almoço sobrando sai.
        var gemini = new GeminiFalso { AlmocoNaIndividual = true };

        Assert.IsType<OkObjectResult>(await Dietas(banco, b.Id, gemini).Gerar());

        Assert.Equal(2, gemini.ChamadasIndividuais);
        Assert.All(DietaAtual(banco, b).Dias, d => Assert.Single(d.Refeicoes, r => r.Nome.Contains("Almo")));
    }

    [Fact]
    public async Task Semana_Nova_Mantem_O_Cafe_E_Os_Lanches_De_Cada_Um_Reajustados()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);
        await GerarSemana(banco, a, new GeminiFalso());
        Assert.IsType<OkObjectResult>(await Dietas(banco, b.Id, new GeminiFalso()).Gerar());
        var antes = DietaAtual(banco, b);

        // A semana nova traz outro jantar (mais leve): o café e os lanches da B ficam, com as quantidades refeitas.
        var gemini = new GeminiFalso { Jantar = _ => JantarLeve };
        await GerarSemana(banco, a, gemini);

        Assert.Equal(0, gemini.ChamadasIndividuais);
        var depois = DietaAtual(banco, b);
        foreach (var dia in depois.Dias)
        {
            var individuaisAntes = antes.Dias.Single(d => d.DiaSemana == dia.DiaSemana).Refeicoes.Where(r => !r.DaCasa).ToList();
            var individuaisDepois = dia.Refeicoes.Where(r => !r.DaCasa).ToList();
            Assert.Equal(individuaisAntes.Select(r => r.Nome), individuaisDepois.Select(r => r.Nome));
            Assert.Equal(individuaisAntes.SelectMany(r => r.Alimentos).Select(x => x.AlimentoId), individuaisDepois.SelectMany(r => r.Alimentos).Select(x => x.AlimentoId));
            // O dia inteiro (casa + individuais) fecha perto da meta cheia.
            Assert.InRange(dia.TotalDoDia.Calorias, dia.MetaCalculada.Calorias * 0.85m, dia.MetaCalculada.Calorias * 1.15m);
        }
        Assert.Equal(DietaIA.OrigemCasa, banco.DietasIA.Where(d => d.UsuarioId == b.Id).OrderByDescending(d => d.Id).First().Origem);
    }

    [Fact]
    public async Task Refeicao_Da_Casa_Com_Algo_Marcado_Depois_Vira_Individual_E_So_O_Dono_Ve_O_Aviso()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);
        await GerarSemana(banco, a, new GeminiFalso());
        // Depois da semana gerada, a B passa a não comer frango (está em todo almoço da casa).
        NaoComo(banco, b, "frango");
        var gemini = new GeminiFalso { AlmocoNaIndividual = true };

        Assert.IsType<OkObjectResult>(await Dietas(banco, b.Id, gemini).Gerar());

        Assert.Contains("monte: café da manhã, lanches e almoço", gemini.Prompts.Single());
        Assert.Equal(1, gemini.ChamadasIndividuais);
        var dieta = DietaAtual(banco, b);
        Assert.All(dieta.Dias, d =>
        {
            var almoco = Assert.Single(d.Refeicoes, r => r.Nome.Contains("Almo"));
            Assert.False(almoco.DaCasa);
            Assert.DoesNotContain(almoco.Alimentos, x => x.AlimentoId!.Contains("frango"));
            Assert.Single(d.Refeicoes, r => r.DaCasa);
        });

        var doDono = Assert.IsType<SemanaCasaResponse>(Assert.IsType<OkObjectResult>(await Casa(banco, a.Id).ObterSemana()).Value);
        var doMembro = Assert.IsType<SemanaCasaResponse>(Assert.IsType<OkObjectResult>(await Casa(banco, b.Id).ObterSemana()).Value);
        Assert.True(doDono.RefeicaoIndividual);
        Assert.Null(doMembro.RefeicaoIndividual);
    }

    // ---------- Limite de gerações ----------

    [Fact]
    public async Task Dieta_Criada_Pela_Casa_Nao_Gasta_A_Cota_Do_Membro_Mas_Gasta_A_Do_Dono()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);
        for (var i = 0; i < LimiteGeracoes.PorHora; i++)
        {
            await GerarSemana(banco, a, new GeminiFalso());
        }

        // O membro ainda gera: as 3 dietas dele vieram da casa.
        Assert.IsType<OkObjectResult>(await Dietas(banco, b.Id, new GeminiFalso()).Gerar());
        // O dono, não: nem outra semana, nem uma dieta individual.
        var gemini = new GeminiFalso();
        Assert.Equal(429, Status(await Casa(banco, a.Id).GerarSemana(Geracao(banco, gemini))));
        Assert.Equal(429, Status(await Dietas(banco, a.Id, gemini).Gerar()));
        Assert.Equal(0, gemini.Chamadas);
        // A casa guarda a atual e a anterior.
        Assert.Equal(GeracaoSemanaCasa.SemanasGuardadas, await banco.SemanasCasa.CountAsync());
    }

    // ---------- Prompt ----------

    [Fact]
    public async Task Prompt_Da_Casa_Nao_Leva_Nenhum_Dado_De_Nenhum_Membro()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        // Um "não como" que não casa com a base (o que casa já sai da lista): o texto não pode ir ao Gemini.
        NaoComo(banco, b, "javali do sítio");
        NaoComo(banco, c, "jiló por causa da gastrite", TiposPreferencia.Evitar);
        NaoComo(banco, c, "batata doce", TiposPreferencia.Evitar);
        Alergia(banco, b, "soja");
        var gemini = new GeminiFalso();

        await GerarSemana(banco, a, gemini);

        var prompt = gemini.Prompts.Single();
        var enviado = gemini.Corpos.Single();
        // A referência fixa vai; nenhuma meta de ninguém.
        Assert.Contains("650 kcal", prompt);
        Assert.Contains("550 kcal", prompt);
        var metas = new[] { a, b, c }.SelectMany(p => DietaAtual(banco, p).Dias.Select(d => d.MetaCalculada))
            .SelectMany(m => new[] { m.Calorias, m.ProteinasG, m.CarboidratosG, m.GordurasG })
            .Select(v => v.ToString("F0", CultureInfo.InvariantCulture))
            .Where(v => v.Length >= 3)
            .Distinct();
        foreach (var valor in metas)
        {
            Assert.DoesNotMatch(new Regex($@"\b{valor}\b"), prompt);
        }
        // Nem peso, altura, idade, sexo, objetivo, nome, e-mail, UF, treino, alergia ou o texto das preferências.
        foreach (var proibido in new[]
        {
            // "peso" fica de fora: o prompt fala do peso do alimento e da unidade dos contáveis. O peso das pessoas
            // está nos números e no "kg".
            "80", "62", "95", "178", "165", "175", "kg", "Masculino", "Feminino", "homem", "mulher", "anos", "idade",
            "perder", "ganhar", "manter", "déficit", "superávit", "manutenção", "treino", "descanso", "Ana", "Bia", "Caio",
            "Sobrenomeficticio", "exemplo.test", "SP", "alerg", "soja", "javali", "sítio", "jiló", "gastrite", "Não como", "meta"
        })
        {
            Assert.DoesNotMatch(new Regex($@"(?i)\b{Regex.Escape(proibido)}"), prompt);
        }
        // O leve vai pelo nome da lista, sem dizer de quem.
        Assert.Contains("alguém da casa prefere evitar", prompt);
        Assert.Contains(Item("batata-doce-cozida"), prompt);
        // O enum do nome é a lista da casa (sem a soja da B) e o da refeição, só almoço e jantar.
        Assert.DoesNotContain(Item("oleo-de-soja"), enviado);
        Assert.Contains("\"Almoço\",\"Jantar\"", enviado.Replace("\\u00E7", "ç").Replace("\\u00e7", "ç"));
    }

    // ---------- Lista pobre ----------

    [Fact]
    public async Task Papel_Vazio_Na_Casa_Da_400_Sem_Chamar_O_Gemini_E_Sem_Dizer_De_Quem()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        // Juntos, os três tiram todas as hortaliças (cada um sozinho ainda teria várias).
        var hortalicas = BaseNutricional.Itens.Where(i => i.Grupo == GrupoDeTroca.Hortalicas).Select(i => i.Nome).ToList();
        for (var i = 0; i < hortalicas.Count; i++)
        {
            NaoComo(banco, new[] { a, b, c }[i % 3], hortalicas[i]);
        }
        var gemini = new GeminiFalso();

        var resultado = await Casa(banco, a.Id).GerarSemana(Geracao(banco, gemini));

        Assert.Equal(400, Status(resultado));
        var mensagem = JsonSerializer.Serialize(Assert.IsAssignableFrom<ObjectResult>(resultado).Value);
        Assert.Contains("hortali", mensagem);
        Assert.DoesNotContain("Bia", mensagem);
        Assert.Equal(0, gemini.Chamadas);
    }

    [Fact]
    public async Task Lista_Pobre_Gera_Com_O_Aviso_So_Para_O_Dono()
    {
        using var banco = Banco();
        var (a, b, _) = await CasaDeTres(banco);
        // Sobram só alface e tomate entre as hortaliças: abaixo do mínimo de 3, sem ficar vazio.
        foreach (var h in BaseNutricional.Itens.Where(i => i.Grupo == GrupoDeTroca.Hortalicas && i.Id is not "alface-crua" and not "tomate-cru"))
        {
            NaoComo(banco, b, h.Nome);
        }

        // O jantar padrão tem brócolis, que saiu da lista da casa: aqui o jantar é o que só usa alface.
        var doDono = await GerarSemana(banco, a, new GeminiFalso { Jantar = _ => JantarLeve });
        var doMembro = Assert.IsType<SemanaCasaResponse>(Assert.IsType<OkObjectResult>(await Casa(banco, b.Id).ObterSemana()).Value);

        Assert.NotNull(doDono.AvisoListaPobre);
        Assert.Contains("Hortalica", doDono.AvisoListaPobre!.Papeis);
        Assert.Null(doMembro.AvisoListaPobre);
        // Na dieta de cada um, o aviso da casa não aparece.
        Assert.Null(DietaAtual(banco, b).AvisoListaPobre);
    }

    // ---------- Misturas ----------

    [Fact]
    public async Task Tres_Misturas_Recusam_A_Primeira_E_Sao_Cortadas_Na_Ultima()
    {
        using var banco = Banco();
        var (a, _, _) = await CasaDeTres(banco);
        var gemini = new GeminiFalso { Almoco = _ => AlmocoComTresMisturas };

        await GerarSemana(banco, a, gemini);

        Assert.Equal(2, gemini.ChamadasCasa);
        var semana = JsonSerializer.Deserialize<SemanaCasaDto>((await banco.SemanasCasa.SingleAsync()).ConteudoJson)!;
        Assert.All(semana.Dias.SelectMany(d => d.Refeicoes), r =>
            Assert.True(r.Alimentos.Count(x => GeradorDietaGemini.EMistura(BaseNutricional.Itens.Single(i => i.Id == x.AlimentoId))) <= 2));
        // Ficam as duas de maior quantidade (o frango e o ovo); a carne menor sai. O feijão não conta como mistura.
        Assert.All(semana.Dias, d => Assert.Contains(d.Refeicoes[0].Alimentos, x => x.AlimentoId == "feijao-cozido"));
    }

    // ---------- Vazamento ----------

    [Fact]
    public async Task Nenhuma_Resposta_Da_Casa_Leva_Porcao_Meta_Ou_Dado_De_Outro_Membro()
    {
        using var banco = Banco();
        var (a, b, c) = await CasaDeTres(banco);
        Alergia(banco, c, "peixe");
        NaoComo(banco, b, "batata doce", TiposPreferencia.Evitar);
        var postDono = await GerarSemana(banco, a, new GeminiFalso());
        var getMembro = Assert.IsType<OkObjectResult>(await Casa(banco, b.Id).ObterSemana()).Value!;
        var getDono = Assert.IsType<OkObjectResult>(await Casa(banco, a.Id).ObterSemana()).Value!;

        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var resposta in new object[] { postDono, getMembro, getDono })
        {
            var json = JsonSerializer.Serialize(resposta, opcoes);
            // Campo a campo: só id, revisão, data, dias com chave, horário e nomes, e os avisos do dono.
            using var doc = JsonDocument.Parse(json);
            var campos = new List<string>();
            void Andar(JsonElement e, string caminho)
            {
                if (e.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in e.EnumerateObject()) { campos.Add(p.Name); Andar(p.Value, p.Name); }
                }
                else if (e.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in e.EnumerateArray()) Andar(v, caminho);
                }
            }
            Andar(doc.RootElement, "");
            var permitidos = new HashSet<string>
            {
                "semanaId", "revisao", "geradaEm", "dias", "diaSemana", "almoco", "jantar", "chaveCasa", "horario", "alimentos",
                "avisoListaPobre", "papeis", "itensPorPapel", "gruposDeProteina", "refeicaoIndividual",
                "Proteina", "Carboidrato", "Fruta", "Hortalica"
            };
            Assert.All(campos, campo => Assert.Contains(campo, permitidos));
            foreach (var proibido in new[] { "quantidade", "calorias", "kcal", "macros", "meta", "peso", "objetivo", "email", "porc", "alerg", "peixe", "Sobrenome", "Bia", "Caio" })
            {
                Assert.DoesNotContain(proibido, json, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Nenhum alimento aparece com quantidade: a porção de cada um está só na dieta dele, e a dieta de um não está
        // na resposta de outro.
        var dietaDaBia = DietaAtual(banco, b);
        var jsonMembro = JsonSerializer.Serialize(getMembro, opcoes);
        foreach (var nomeComContagem in dietaDaBia.Dias.SelectMany(d => d.Refeicoes).Where(r => r.DaCasa).SelectMany(r => r.Alimentos).Select(x => x.Nome).Where(n => n.Contains('(')))
        {
            Assert.DoesNotContain(nomeComContagem, jsonMembro);
        }
    }

    // ---------- Exclusão ----------

    [Fact]
    public async Task Dono_Sozinho_Apaga_A_Conta_E_A_Semana_Da_Casa_Sai_Junto()
    {
        using var banco = Banco();
        var dono = await Pessoa(banco, "sozinho-casa@exemplo.test", "Sol Sobrenomeficticio", PerfilA);
        await MontarCasa(banco, dono);
        await GerarSemana(banco, dono, new GeminiFalso());
        Assert.Equal(1, await banco.SemanasCasa.CountAsync());

        Assert.True(await ExclusaoConta.ApagarAsync(banco, dono.Id));

        Assert.Equal(0, await banco.SemanasCasa.CountAsync());
        Assert.Equal(0, await banco.Casas.CountAsync());
        Assert.Equal(0, await banco.DietasIA.CountAsync());
    }

    // ---------- Gemini falso ----------

    private static object[] Itens(params (string Id, decimal Q)[] itens) =>
        itens.Select(i => (object)new { nome = Item(i.Id), quantidade = i.Q, unidade = "g" }).ToArray();

    private static readonly object[] AlmocoPadrao = Itens(
        ("arroz-branco-cozido", 150), ("feijao-cozido", 100), ("peito-de-frango-sem-pele-grelhado", 120),
        ("alface-crua", 30), ("tomate-cru", 50), ("azeite-de-oliva-extra-virgem", 5));

    private static readonly object[] JantarPadrao = Itens(
        ("macarrao-cozido", 180), ("ovo-de-galinha-cozido", 100), ("brocolis-cozido", 80), ("azeite-de-oliva-extra-virgem", 8));

    private static readonly object[] JantarLeve = Itens(
        ("batata-inglesa-cozida", 200), ("ovo-de-galinha-cozido", 100), ("alface-crua", 40), ("azeite-de-oliva-extra-virgem", 5));

    private static readonly object[] AlmocoComTilapia = Itens(
        ("arroz-branco-cozido", 150), ("feijao-cozido", 100), ("file-de-tilapia-grelhado", 120), ("alface-crua", 30));

    private static readonly object[] AlmocoComTresMisturas = Itens(
        ("arroz-branco-cozido", 150), ("feijao-cozido", 100), ("peito-de-frango-sem-pele-grelhado", 120),
        ("ovo-de-galinha-cozido", 100), ("patinho-bovino-grelhado", 60), ("alface-crua", 30));

    private static RefeicaoDto Refeicao(string chave, params (string Id, decimal Q)[] itens) => new()
    {
        Nome = PorcoesCasa.EAlmoco(chave) ? PorcoesCasa.NomeAlmoco : PorcoesCasa.NomeJantar,
        ChaveCasa = chave,
        DaCasa = true,
        Macros = new MacrosDto { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 },
        Alimentos = itens.Select(i => new AlimentoDto { Nome = Item(i.Id), Quantidade = i.Q, Unidade = "g", AlimentoId = i.Id }).ToList()
    };

    /// <summary>
    /// Responde a semana da casa (prompt "para uma casa") ou a semana individual (café da manhã e lanche da tarde; com
    /// AlmocoNaIndividual, também um almoço sem frango, que só vale nos dias em que a linha do prompt pede). Guarda
    /// cada prompt e corpo.
    /// </summary>
    private sealed class GeminiFalso : HttpMessageHandler
    {
        public Func<string, object[]> Almoco { get; init; } = _ => AlmocoPadrao;
        public Func<string, object[]> Jantar { get; init; } = _ => JantarPadrao;
        public bool AlmocoNaIndividual { get; init; }

        public List<string> Prompts { get; } = [];
        public List<string> Corpos { get; } = [];
        public int Chamadas => Prompts.Count;
        public int ChamadasCasa => Prompts.Count(p => p.Contains("para uma casa"));
        public int ChamadasIndividuais => Chamadas - ChamadasCasa;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var corpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            Corpos.Add(corpo);
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
                    : Individuais(n),
                totalDoDia = new { calorias = 1000, proteinasG = 60, carboidratosG = 120, gordurasG = 30 }
            });

            object[] Individuais(string dia)
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
