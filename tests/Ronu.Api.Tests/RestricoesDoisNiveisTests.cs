using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Migrations;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;
using Ronu.Api.Services.Troca;

namespace Ronu.Api.Tests;

/// <summary>
/// Restrições em dois níveis (plano família, etapa 3, D2): a migration que move todo "evitar" para "nao_como"; o
/// "nao_como" bloqueando como o "evitar" de antes (lista, enum, troca, conferência, sugestão de alergia); o "evitar"
/// leve, que fica na lista e no enum, ganha um parágrafo no prompt, é conferido depois da resposta (acima de 2 por
/// semana, somando os itens de cada chip: a 1ª resposta é recusada, na 2ª as excedentes são trocadas) e é oferecido
/// por último na troca. O prompt e o
/// log continuam sem dado de saúde. Gemini simulado, banco em memória, só contas fictícias (@exemplo.test).
/// </summary>
public class RestricoesDoisNiveisTests
{
    private const string ArrozBranco = "arroz-branco-cozido";
    private const string Tilapia = "file-de-tilapia-grelhado";

    private static AlimentoBase Base(string id) => BaseNutricional.Itens.Single(i => i.Id == id);

    private static PreferenciaContextoDto Gosto(string alimento) => new() { Alimento = alimento, Tipo = TiposPreferencia.Preferido };
    private static PreferenciaContextoDto NaoComo(string alimento) => new() { Alimento = alimento, Tipo = TiposPreferencia.NaoComo };
    private static PreferenciaContextoDto Leve(string alimento) => new() { Alimento = alimento, Tipo = TiposPreferencia.Evitar };

    private static ListaAlimentos Lista(params PreferenciaContextoDto[] prefs) => ListaAlimentosPessoa.Montar(null, prefs, Alergenos.Nenhum);

    private static IEnumerable<string> Ids(ListaAlimentos lista) => lista.Itens.Select(i => i.Item.Id);

    // ---------- Migration ----------

    // Só dados: um UPDATE no Up (evitar -> nao_como) e o inverso no Down, sem nenhuma mudança de esquema.
    [Fact]
    public void Migration_So_Move_Evitar_Para_Nao_Como()
    {
        var migration = new PreferenciasEmDoisNiveis();

        var up = Assert.IsType<SqlOperation>(Assert.Single(migration.UpOperations));
        var down = Assert.IsType<SqlOperation>(Assert.Single(migration.DownOperations));

        Assert.Equal("""UPDATE "PreferenciasAlimentares" SET "Tipo" = 'nao_como' WHERE "Tipo" = 'evitar';""", up.Sql);
        Assert.Equal("""UPDATE "PreferenciasAlimentares" SET "Tipo" = 'evitar' WHERE "Tipo" = 'nao_como';""", down.Sql);
    }

    // O que a migration faz com um "evitar" de antes (vira "nao_como") deixa a lista exatamente como o "evitar" de
    // antes deixava: os mesmos itens saem. Comparado com a lista sem preferência, para não depender do código antigo.
    [Fact]
    public void Nao_Como_Tira_Da_Lista_O_Mesmo_Que_O_Evitar_De_Antes_Tirava()
    {
        var semNada = Ids(Lista()).ToHashSet();
        var comNaoComo = Ids(Lista(NaoComo("peixe"))).ToHashSet();

        var casados = CasamentoPreferencias.Casar("peixe", BaseNutricional.Itens).ItensIds;
        Assert.NotEmpty(casados);
        Assert.Equal(semNada.Except(casados).OrderBy(i => i), comNaoComo.OrderBy(i => i));
    }

    // ---------- Controller ----------

    private static PreferenciasAlimentaresController Controller(ApplicationDbContext banco, int usuarioId) => new(banco)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"))
            }
        }
    };

    private static Usuario NovoUsuario(ApplicationDbContext banco, string email, params (string Tipo, string Alimento)[] prefs)
    {
        var usuario = new Usuario { Nome = "Teste", Email = email, Estado = "SP" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        foreach (var (tipo, alimento) in prefs)
        {
            banco.PreferenciasAlimentares.Add(new PreferenciaAlimentar { UsuarioId = usuario.Id, Tipo = tipo, Alimento = alimento });
        }

        banco.SaveChanges();
        return usuario;
    }

    [Theory]
    [InlineData("preferido")]
    [InlineData("nao_como")]
    [InlineData("evitar")]
    public async Task Controller_Aceita_Os_Tres_Tipos(string tipo)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, $"tipos-{tipo}@exemplo.test");

        var r = Assert.IsType<OkObjectResult>(await Controller(banco, usuario.Id).Criar(new PreferenciaAlimentarRequest { Alimento = "jiló", Tipo = tipo }));

        Assert.Equal(tipo, Assert.IsType<PreferenciaAlimentarResponse>(r.Value).Tipo);
    }

    // Mudar de nível move o item: o mesmo registro (mesmo id), sem o alimento em dois níveis.
    [Fact]
    public async Task Mudar_De_Nivel_Move_O_Item()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "mover@exemplo.test");
        var controller = Controller(banco, usuario.Id);

        var antes = (PreferenciaAlimentarResponse)((OkObjectResult)await controller.Criar(new PreferenciaAlimentarRequest { Alimento = "jiló", Tipo = "nao_como" })).Value!;
        var depois = (PreferenciaAlimentarResponse)((OkObjectResult)await controller.Criar(new PreferenciaAlimentarRequest { Alimento = "Jiló", Tipo = "evitar" })).Value!;

        Assert.Equal(antes.Id, depois.Id);
        var salva = Assert.Single(banco.PreferenciasAlimentares.Where(p => p.UsuarioId == usuario.Id));
        Assert.Equal("evitar", salva.Tipo);
    }

    // ---------- Conferência ----------

    // O "nao_como" mostra o que sai e sugere o chip de alergia, como o "evitar" de antes. O leve não sugere alergia.
    [Fact]
    public async Task Conferencia_Sugere_Alergia_So_No_Nao_Como()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "conferencia-niveis@exemplo.test", ("nao_como", "camarão"), ("evitar", "soja"));

        var r = Assert.IsType<ConferenciaPreferenciasResponse>(Assert.IsType<OkObjectResult>(await Controller(banco, usuario.Id).Conferir()).Value);

        var camarao = r.Preferencias.Single(p => p.Alimento == "camarão");
        var soja = r.Preferencias.Single(p => p.Alimento == "soja");
        Assert.Equal("nao_como", camarao.Tipo);
        Assert.Equal(["crustaceos"], camarao.SugereAlergias);
        Assert.True(camarao.Casou);
        Assert.NotEmpty(camarao.Alimentos);
        Assert.Equal(("evitar", 0), (soja.Tipo, soja.SugereAlergias.Count));
        Assert.True(soja.Casou);
    }

    // O leve não tira item da lista: muitos "evitar" não deixam a lista pobre; o mesmo texto em "nao_como" deixa.
    [Fact]
    public void Leve_Nao_Conta_No_Aviso_De_Lista_Pobre()
    {
        string[] textos = ["carne", "ovo", "leite", "feijão", "lentilha"];

        Assert.True(Lista(textos.Select(Leve).ToArray()).Minimos.Atende);
        Assert.Contains(PapelNoPrato.Proteina, Lista(textos.Select(NaoComo).ToArray()).Minimos.Vazios);
    }

    // ---------- Lista e enum ----------

    [Fact]
    public void Nao_Como_Sai_Da_Lista_E_O_Leve_Fica()
    {
        var naoComo = Lista(NaoComo("tilápia"));
        var leve = Lista(Leve("tilápia"));

        Assert.DoesNotContain(Tilapia, Ids(naoComo));
        Assert.Contains(Tilapia, Ids(leve));
        Assert.Empty(naoComo.ItensLeves);
        Assert.Contains(Tilapia, leve.ItensLeves);
    }

    // Um item no "nao_como" e no "evitar" (dois textos que casam com ele) fica fora: o bloqueio vence.
    [Fact]
    public void Nao_Como_Vence_O_Leve()
    {
        var lista = Lista(NaoComo("peixe"), Leve("tilápia"));

        Assert.DoesNotContain(Tilapia, Ids(lista));
        Assert.DoesNotContain(Tilapia, lista.ItensLeves);
    }

    [Fact]
    public async Task Enum_Do_Gemini_Nao_Tem_O_Nao_Como_E_Tem_O_Leve()
    {
        var nome = Base(Tilapia).Nome;

        var comNaoComo = new GeminiRoteirizado(Envelope(DietaVazia()));
        await Gerador(comNaoComo).GerarComListaAsync(Contexto(NaoComo("tilápia")));
        var comLeve = new GeminiRoteirizado(Envelope(DietaVazia()));
        await Gerador(comLeve).GerarComListaAsync(Contexto(Leve("tilápia")));

        Assert.DoesNotContain(nome, EnumDoNome(comNaoComo));
        Assert.Contains(nome, EnumDoNome(comLeve));
    }

    // ---------- Prompt ----------

    [Fact]
    public async Task Prompt_Tem_O_Paragrafo_Do_Leve_Com_O_Limite()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaVazia()));

        await Gerador(gemini).GerarComListaAsync(Contexto(Leve("tilápia"), Leve("kiwano dourado")));
        var prompt = Prompt(gemini);

        Assert.Contains($"Alimentos da lista que a pessoa prefere evitar, em grupos (um por linha): use pouco, no máximo {GeradorDietaGemini.MaximoPorSemanaEvitar} vezes na semana em cada grupo, somando todos os alimentos do grupo, e só quando ajudarem a chegar na meta:\n- {Base(Tilapia).Nome}", prompt);
        Assert.Contains("Outras coisas que a pessoa prefere evitar (evite quando possível, sem proibir): kiwano dourado", prompt);
        // O leve não entra nas restrições duras.
        Assert.Contains("nem como ingrediente de outro prato): nenhuma", prompt);
    }

    // Sem nenhum "evitar" leve, o parágrafo some: o prompt de quem não usa o nível leve fica como antes.
    [Fact]
    public async Task Sem_Leve_O_Prompt_Nao_Ganha_O_Paragrafo()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaVazia()));

        await Gerador(gemini).GerarComListaAsync(Contexto(NaoComo("tilápia"), Gosto("banana")));

        Assert.DoesNotContain("prefere evitar", Prompt(gemini));
    }

    // Um item que casa com um gosto e com um leve não vai como base do plano.
    [Fact]
    public async Task Item_Leve_Nao_Vai_Como_Gosto()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaVazia()));

        await Gerador(gemini).GerarComListaAsync(Contexto(Gosto("tilápia"), Leve("tilápia grelhada")));

        Assert.Contains("nenhum dos que ela gosta está na lista", Prompt(gemini));
    }

    // O prompt e o log continuam sem dado de saúde: do leve sem casamento vai só o alimento (TermoParaIA), e o log
    // leva só contagens e ids da base.
    [Fact]
    public async Task Prompt_E_Log_Sem_Dado_De_Saude_No_Leve()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaComArroz(14)), Envelope(DietaComArroz(14)));
        var logger = new LoggerQueGuarda<GeradorDietaGemini>();

        await Gerador(gemini, logger).GerarComListaAsync(Contexto(Leve("Tenho alergia a gergelim"), Leve("sou diabético"), Leve("Arroz branco cozido")));
        var prompt = Prompt(gemini);
        var log = string.Join("\n", logger.Mensagens);

        Assert.Contains("evite quando possível, sem proibir): gergelim", prompt);
        foreach (var proibido in new[] { "alergia", "tenho", "diabetic", "diabétic" })
        {
            Assert.DoesNotContain(proibido, prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(proibido, log, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("gergelim", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Arroz branco cozido", log.Split('\n').Where(l => l.Contains("prefiro evitar")).DefaultIfEmpty("").First());
        Assert.Contains("2 evitar leve sem casamento", log);
    }

    // ---------- Validação depois da resposta ----------

    // Até 2 vezes na semana: passa na primeira.
    [Fact]
    public async Task Leve_Ate_O_Limite_Passa_Na_Primeira()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaComArroz(GeradorDietaGemini.MaximoPorSemanaEvitar)));

        var dieta = await Gerador(gemini).GerarComListaAsync(Contexto(Leve("Arroz branco cozido")));

        Assert.Equal(1, gemini.Chamadas);
        Assert.Equal(GeradorDietaGemini.MaximoPorSemanaEvitar, Vezes(dieta, ArrozBranco));
    }

    // Acima do limite na 1ª resposta: inválida, nova chamada. A 2ª dentro do limite fica como veio.
    [Fact]
    public async Task Leve_Acima_Do_Limite_Recusa_A_Primeira()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaComArroz(3)), Envelope(DietaComArroz(1)));

        var dieta = await Gerador(gemini).GerarComListaAsync(Contexto(Leve("Arroz branco cozido")));

        Assert.Equal(2, gemini.Chamadas);
        Assert.Equal(1, Vezes(dieta, ArrozBranco));
    }

    // Acima do limite nas duas: na 2ª, as excedentes (as últimas da semana) viram o melhor substituto da troca, do
    // mesmo grupo, da lista e sem outro item leve. As 2 primeiras da semana ficam.
    [Fact]
    public async Task Leve_Acima_Do_Limite_Na_Segunda_Troca_As_Excedentes()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaComArroz(14)), Envelope(DietaComArroz(14)));
        var logger = new LoggerQueGuarda<GeradorDietaGemini>();
        var contexto = Contexto(Leve("Arroz branco cozido"));

        var dieta = await Gerador(gemini, logger).GerarComListaAsync(contexto);

        Assert.Equal(2, gemini.Chamadas);
        Assert.Equal(GeradorDietaGemini.MaximoPorSemanaEvitar, Vezes(dieta, ArrozBranco));
        var segunda = dieta.Dias.Single(d => d.DiaSemana == "Segunda-feira");
        Assert.All(segunda.Refeicoes, r => Assert.Equal(ArrozBranco, r.Alimentos[0].AlimentoId));
        var lista = Lista(Leve("Arroz branco cozido"));
        var trocados = dieta.Dias.Where(d => d.DiaSemana != "Segunda-feira").SelectMany(d => d.Refeicoes).Select(r => r.Alimentos[0]).ToList();
        Assert.All(trocados, a =>
        {
            Assert.Equal(GrupoDeTroca.Carboidratos, Base(a.AlimentoId!).Grupo);
            Assert.Contains(a.AlimentoId!, Ids(lista));
            Assert.DoesNotContain(a.AlimentoId!, lista.ItensLeves);
            Assert.NotNull(a.Macros);
        });
        // O total do dia foi somado de novo depois da troca.
        var terca = dieta.Dias.Single(d => d.DiaSemana == "Terça-feira");
        Assert.Equal(terca.Refeicoes.Sum(r => r.Macros.Calorias), terca.TotalDoDia.Calorias);
        Assert.Contains(logger.Mensagens, m => m.Contains("prefiro evitar") && m.Contains("12 trocadas"));
    }

    // Sem substituto (a lista só tem o arroz como carboidrato), as excedentes ficam como estão, e o log registra.
    [Fact]
    public async Task Leve_Sem_Substituto_Fica_E_O_Log_Registra()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaComArroz(14)), Envelope(DietaComArroz(14)));
        var logger = new LoggerQueGuarda<GeradorDietaGemini>();
        var completa = Lista(Leve("Arroz branco cozido"));
        var soArroz = completa with
        {
            Itens = completa.Itens.Where(i => i.Item.Grupo != GrupoDeTroca.Carboidratos || i.Item.Id == ArrozBranco).ToList()
        };
        var contexto = Contexto();
        contexto.Lista = soArroz;

        var dieta = await Gerador(gemini, logger).GerarDietaAsync(contexto);

        Assert.Equal(2, gemini.Chamadas);
        Assert.Equal(14, Vezes(dieta, ArrozBranco));
        Assert.Contains(logger.Mensagens, m => m.Contains("prefiro evitar") && m.Contains("0 trocadas") && m.Contains("12 sem substituto"));
    }

    // ---------- Limite por chip (opção A, aprovada em 2026-11-13) ----------

    // Os cortes de "carne vermelha" que estão na lista, na ordem da lista (o grupo do chip).
    private static List<string> CarneVermelha() =>
        Ids(Lista(Leve("carne vermelha"))).Where(Lista(Leve("carne vermelha")).GruposLeves.Single().Contains).ToList();

    // Um grupo por chip, só com os itens da lista; chips que casam igual viram um grupo só; o "nao_como" tira os
    // itens do grupo, como tira da lista.
    [Fact]
    public void Um_Grupo_Por_Chip_Com_Os_Itens_Da_Lista()
    {
        var lista = Lista(Leve("carne vermelha"), Leve("tilápia"), Leve("tilapia"));

        Assert.Equal(2, lista.GruposLeves.Count);
        var carne = lista.GruposLeves[0];
        Assert.True(carne.Count >= 3, "\"carne vermelha\" deveria casar com vários cortes.");
        Assert.All(carne, id => Assert.Contains(Base(id).Categoria, new[] { CategoriaAlimento.CarneBovina, CategoriaAlimento.CarneSuina }));
        Assert.Equal([Tilapia], lista.GruposLeves[1]);

        var comNaoComo = Lista(Leve("carne vermelha"), NaoComo("patinho"));
        Assert.DoesNotContain(comNaoComo.GruposLeves.Single(), id => id.StartsWith("patinho"));
    }

    // O chip amplo vai ao Gemini como o grupo dos nomes da lista, nunca como o texto digitado.
    [Fact]
    public async Task Chip_Amplo_Vai_Ao_Gemini_Como_Grupo_Sem_O_Texto_Digitado()
    {
        var gemini = new GeminiRoteirizado(Envelope(DietaVazia()));

        await Gerador(gemini).GerarComListaAsync(Contexto(Leve("Carne vermelha"), Leve("tilápia")));
        var prompt = Prompt(gemini);

        var nomes = Lista(Leve("carne vermelha")).Itens.Where(i => CarneVermelha().Contains(i.Item.Id)).Select(i => i.Nome);
        Assert.Contains($"somando todos os alimentos do grupo, e só quando ajudarem a chegar na meta:\n- {string.Join("; ", nomes)}\n- {Base(Tilapia).Nome}", prompt);
        Assert.DoesNotContain("carne vermelha", prompt, StringComparison.OrdinalIgnoreCase);
    }

    // 3 cortes diferentes, uma vez cada: nenhum item passa de 2, mas o chip passa (3). A 1ª resposta é recusada; a
    // contagem por item (antes da opção A) aceitaria.
    [Fact]
    public async Task Chip_Amplo_Soma_Os_Itens_E_Recusa_A_Primeira()
    {
        var cortes = CarneVermelha().Take(3).ToArray();
        var gemini = new GeminiRoteirizado(Envelope(DietaCom(cortes)), Envelope(DietaCom(cortes.Take(2).ToArray())));

        var dieta = await Gerador(gemini).GerarComListaAsync(Contexto(Leve("carne vermelha")));

        Assert.Equal(2, gemini.Chamadas);
        Assert.Equal(2, cortes.Sum(c => Vezes(dieta, c)));
    }

    // Acima do limite nas duas respostas: na 2ª, as vezes do grupo além das 2 primeiras da semana viram substituto
    // sem nenhum item leve, do mesmo grupo de troca. O log leva só os ids da base, nunca o texto do chip.
    [Fact]
    public async Task Chip_Amplo_Na_Segunda_Troca_O_Excesso_Do_Grupo()
    {
        var cortes = CarneVermelha();
        var sequencia = Enumerable.Range(0, 6).Select(i => cortes[i % cortes.Count]).ToArray();
        var gemini = new GeminiRoteirizado(Envelope(DietaCom(sequencia)), Envelope(DietaCom(sequencia)));
        var logger = new LoggerQueGuarda<GeradorDietaGemini>();

        var dieta = await Gerador(gemini, logger).GerarComListaAsync(Contexto(Leve("carne vermelha")));

        Assert.Equal(2, gemini.Chamadas);
        Assert.Equal(GeradorDietaGemini.MaximoPorSemanaEvitar, cortes.Distinct().Sum(c => Vezes(dieta, c)));
        // As 2 primeiras da semana (segunda, almoço e jantar) ficam.
        var segunda = dieta.Dias.Single(d => d.DiaSemana == "Segunda-feira");
        Assert.Equal(sequencia.Take(2), segunda.Refeicoes.Select(r => r.Alimentos[0].AlimentoId));
        var leves = Lista(Leve("carne vermelha")).ItensLeves;
        var trocados = dieta.Dias.Where(d => d.DiaSemana is "Terça-feira" or "Quarta-feira").SelectMany(d => d.Refeicoes).Select(r => r.Alimentos[0]).ToList();
        Assert.All(trocados, a =>
        {
            Assert.Equal(GrupoDeTroca.CarnesEPeixes, Base(a.AlimentoId!).Grupo);
            Assert.DoesNotContain(a.AlimentoId!, leves);
        });
        var log = string.Join("\n", logger.Mensagens);
        Assert.Contains(logger.Mensagens, m => m.Contains("prefiro evitar") && m.Contains("4 trocadas"));
        Assert.DoesNotContain("vermelha", log, StringComparison.OrdinalIgnoreCase);
    }

    // Um item em dois chips ("carne vermelha" e "patinho") conta nos dois: 2 patinhos e 1 lombo passam do limite do
    // chip amplo, mesmo com o chip "patinho" dentro dele.
    [Fact]
    public async Task Item_Em_Dois_Chips_Conta_Nos_Dois()
    {
        var patinho = Lista(Leve("patinho")).GruposLeves.Single().First();
        var outro = CarneVermelha().First(c => !Lista(Leve("patinho")).GruposLeves.Single().Contains(c));
        var gemini = new GeminiRoteirizado(Envelope(DietaCom(patinho, patinho, outro)), Envelope(DietaCom(patinho, patinho)));

        await Gerador(gemini).GerarComListaAsync(Contexto(Leve("carne vermelha"), Leve("patinho")));

        Assert.Equal(2, gemini.Chamadas);
    }

    // A vez excedente de um item que está em dois chips é trocada uma vez só (e contada uma vez no log).
    [Fact]
    public async Task Excesso_De_Item_Em_Dois_Chips_E_Trocado_Uma_Vez()
    {
        var patinho = Lista(Leve("patinho")).GruposLeves.Single().First();
        var gemini = new GeminiRoteirizado(Envelope(DietaCom(patinho, patinho, patinho)), Envelope(DietaCom(patinho, patinho, patinho)));
        var logger = new LoggerQueGuarda<GeradorDietaGemini>();

        var dieta = await Gerador(gemini, logger).GerarComListaAsync(Contexto(Leve("carne vermelha"), Leve("patinho")));

        Assert.Equal(GeradorDietaGemini.MaximoPorSemanaEvitar, Vezes(dieta, patinho));
        Assert.Contains(logger.Mensagens, m => m.Contains("prefiro evitar") && m.Contains("1 trocadas") && m.Contains("0 sem substituto"));
    }

    // ---------- Troca ----------

    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    private static AlimentoDto A(string id, decimal quantidade) =>
        new() { Nome = Base(id).Nome, AlimentoId = id, Quantidade = quantidade, Unidade = Base(id).Medida };

    // Um almoço com frango, num dia grande o bastante para os 5% não esconderem as opções.
    private static List<DiaDietaDto> AlmocoComFrango()
    {
        var dia = new DiaDietaDto
        {
            DiaSemana = "Segunda-feira",
            Refeicoes =
            [
                new RefeicaoDto { Nome = "Almoço", Alimentos = [A(ArrozBranco, 300), A("feijao-cozido", 150), A("peito-de-frango-sem-pele-grelhado", 200)], Macros = Zero() },
                new RefeicaoDto { Nome = "Jantar", Alimentos = [A("macarrao-cozido", 400), A("patinho-bovino-grelhado", 200)], Macros = Zero() }
            ],
            TotalDoDia = Zero(),
            MetaCalculada = Zero()
        };
        CalculoPelaBase.Calcular([dia]);
        dia.MetaCalculada = new MacrosDto { Calorias = dia.TotalDoDia.Calorias, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };
        return [dia];
    }

    private static readonly PosicaoItem Frango = new(0, 0, 2);

    // A troca oferece o item leve (ele não sai), mas depois dos outros; o "nao_como" tira e conta no ForaPeloEvitar.
    [Fact]
    public void Troca_Oferece_O_Leve_Por_Ultimo_E_Tira_O_Nao_Como()
    {
        var semNada = CalculadoraTroca.Calcular(AlmocoComFrango(), Frango, Lista()).Refeicao;
        var leve = CalculadoraTroca.Calcular(AlmocoComFrango(), Frango, Lista(Leve("tilápia")));
        var naoComo = CalculadoraTroca.Calcular(AlmocoComFrango(), Frango, Lista(NaoComo("tilápia")));

        var ordemSemNada = semNada.Parecidos.Concat(semNada.VerMais).Select(o => o.AlimentoId).ToList();
        var ordemLeve = leve.Refeicao.Parecidos.Concat(leve.Refeicao.VerMais).Select(o => o.AlimentoId).ToList();
        Assert.Contains(Tilapia, ordemSemNada);
        Assert.NotEqual(Tilapia, ordemSemNada[^1]);
        Assert.Equal(Tilapia, ordemLeve[^1]);
        Assert.Equal(ordemSemNada.Count, ordemLeve.Count);
        Assert.Equal(0, leve.ForaPeloEvitar);

        Assert.DoesNotContain(Tilapia, naoComo.Refeicao.Parecidos.Concat(naoComo.Refeicao.VerMais).Select(o => o.AlimentoId));
        Assert.Equal(1, naoComo.ForaPeloEvitar);
    }

    [Fact]
    public void Mensagem_Sem_Opcoes_Fala_Em_Nao_Como()
    {
        var mensagem = TrocasController.MensagemSemOpcoes(new Dictionary<MotivoForaDaTroca, int>(), 2);

        Assert.Equal("Nenhum substituto serve aqui: 2 estão no que você marcou em Não como.", mensagem);
    }

    // ---------- Apoio do gerador ----------

    private static ContextoDietaDto Contexto(params PreferenciaContextoDto[] prefs) => new()
    {
        Altura = 175, Sexo = "Masculino", Idade = 30, Peso = 80, Objetivo = "manter peso",
        Modalidades = new(), Preferencias = prefs.ToList(), HistoricoPeso = new()
    };

    // Ajuste, mínimo de refeições e gordura desligados, como nos testes de validação: aqui só importa o leve.
    private static GeradorDietaGemini Gerador(HttpMessageHandler gemini, LoggerQueGuarda<GeradorDietaGemini>? logger = null) => new(
        new HttpClient(gemini), new CalculadoraGastoCalorico(),
        new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
        new GeminiOptions { ApiKey = "teste" }, logger is null ? NullLogger<GeradorDietaGemini>.Instance : logger)
    {
        AjustarQuantidades = false,
        ExigirMinimoDeRefeicoes = false,
        ExigirFonteDeGordura = false
    };

    private static int Vezes(DietaSemanalDto dieta, string id) =>
        dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos).Count(a => a.AlimentoId == id);

    private static string DietaVazia() => JsonSerializer.Serialize(new
    {
        dias = GeradorDietaGemini.NomesDias.Select(n => new
        {
            diaSemana = n,
            refeicoes = Array.Empty<object>(),
            totalDoDia = new { calorias = 0, proteinasG = 0, carboidratosG = 0, gordurasG = 0 }
        })
    });

    // Almoço e jantar de cada dia com um item só: o arroz branco nas "vezes" primeiras refeições da semana (na ordem
    // segunda, almoço; segunda, jantar; terça, almoço...), e macarrão nas outras.
    private static string DietaComArroz(int vezes) => DietaCom(Enumerable.Repeat(ArrozBranco, vezes).ToArray());

    // Almoço e jantar de cada dia com um item só: os itens dados, na ordem, nas primeiras refeições da semana, e
    // macarrão nas outras.
    private static string DietaCom(params string[] primeiros)
    {
        var macarrao = Base("macarrao-cozido").Nome;
        var contador = 0;
        object Refeicao(string nome) => new
        {
            nome,
            alimentos = new[] { new { nome = contador < primeiros.Length ? Base(primeiros[contador++]).Nome : macarrao, quantidade = 150m, unidade = "g" } },
            macros = new { calorias = 200, proteinasG = 4, carboidratosG = 40, gordurasG = 1 },
            horario = "12:00"
        };

        var dias = GeradorDietaGemini.NomesDias.Select(n => new
        {
            diaSemana = n,
            refeicoes = new[] { Refeicao("Almoço"), Refeicao("Jantar") },
            totalDoDia = new { calorias = 400, proteinasG = 8, carboidratosG = 80, gordurasG = 2 }
        }).ToList();

        return JsonSerializer.Serialize(new { dias });
    }

    private static string Envelope(string texto) => JsonSerializer.Serialize(new
    {
        candidates = new[] { new { content = new { parts = new[] { new { text = texto } } }, finishReason = "STOP" } },
        usageMetadata = new { promptTokenCount = 1500, candidatesTokenCount = 4000, totalTokenCount = 5500 }
    });

    private static string Prompt(GeminiRoteirizado gemini) =>
        JsonDocument.Parse(gemini.UltimoCorpo!).RootElement
            .GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!.Replace("\r\n", "\n");

    // Os nomes do enum do schema (o "nome" de cada alimento), seja qual for o caminho até ele.
    private static List<string> EnumDoNome(GeminiRoteirizado gemini)
    {
        var nomes = new List<string>();
        void Andar(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name == "enum" && p.Value.ValueKind == JsonValueKind.Array)
                    {
                        nomes.AddRange(p.Value.EnumerateArray().Select(v => v.GetString()!));
                    }
                    else
                    {
                        Andar(p.Value);
                    }
                }
            }
            else if (e.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in e.EnumerateArray())
                {
                    Andar(v);
                }
            }
        }

        Andar(JsonDocument.Parse(gemini.UltimoCorpo!).RootElement.GetProperty("generationConfig").GetProperty("responseSchema"));
        Assert.NotEmpty(nomes);
        return nomes;
    }

    // Responde cada chamada com o próximo corpo da fila e guarda o último pedido.
    private sealed class GeminiRoteirizado(params string[] respostas) : HttpMessageHandler
    {
        private readonly Queue<string> _respostas = new(respostas);

        public int Chamadas { get; private set; }
        public string? UltimoCorpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            UltimoCorpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_respostas.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }
}
