using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;
using Ronu.Api.Services.ListaCompras;
using Ronu.Api.Services.Troca;

namespace Ronu.Api.Tests;

/// <summary>
/// Trocar alimento, etapa 2: gravação na dieta (Original, UltimaTroca, revisão), as rotas de opções, troca,
/// desfazer e voltar ao original, a concorrência entre abas e as marcações "já tenho" (D5). Só contas fictícias.
/// </summary>
public class TrocasControllerTests
{
    // ---------- Apoio ----------

    private static AlimentoDto A(string id, decimal quantidade)
    {
        var b = BaseNutricional.Itens.Single(i => i.Id == id);
        return new AlimentoDto { Nome = b.Nome, AlimentoId = id, Quantidade = quantidade, Unidade = b.Medida };
    }

    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    // Os 7 dias iguais, calculados pela base, cada um exatamente na meta. Almoço: refeição 1; frango: item 2.
    private static DietaSemanalDto Dieta(decimal frangoNaTerca = 200)
    {
        var dias = GeradorDietaGemini.NomesDias.Select((nome, d) => new DiaDietaDto
        {
            DiaSemana = nome,
            Refeicoes =
            [
                new RefeicaoDto { Nome = "Café da manhã", Macros = Zero(), Alimentos = [A("pao-frances", 100), A("ovo-de-galinha-cozido", 90), A("cafe-coado", 200)] },
                new RefeicaoDto { Nome = "Almoço", Macros = Zero(), Alimentos = [A("arroz-branco-cozido", 300), A("feijao-cozido", 150), A("peito-de-frango-sem-pele-grelhado", d == 1 ? frangoNaTerca : 200), A("alface-crua", 50), A("azeite-de-oliva-extra-virgem", 10)] },
                new RefeicaoDto { Nome = "Jantar", Macros = Zero(), Alimentos = [A("macarrao-cozido", 400), A("patinho-bovino-grelhado", 200), A("banana-prata", 225), A("leite-integral", 500)] }
            ],
            TotalDoDia = Zero(),
            MetaCalculada = Zero()
        }).ToList();
        CalculoPelaBase.Calcular(dias);
        foreach (var dia in dias)
        {
            dia.MetaCalculada = new MacrosDto { Calorias = dia.TotalDoDia.Calorias, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };
        }

        return new DietaSemanalDto { Dias = dias, VersaoBase = BaseNutricional.VersaoBase };
    }

    private static TrocasController Controller(ApplicationDbContext banco, int usuarioId) => new(banco, NullLogger<TrocasController>.Instance)
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

    private static DietaIA NovaDieta(ApplicationDbContext banco, Usuario usuario, DietaSemanalDto? dieta = null, DateTime? quando = null)
    {
        var dietaIA = new DietaIA
        {
            UsuarioId = usuario.Id, DataGeracao = quando ?? new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc),
            ConteudoJson = JsonSerializer.Serialize(dieta ?? Dieta())
        };
        banco.DietasIA.Add(dietaIA);
        banco.SaveChanges();
        return dietaIA;
    }

    private static TrocaRequest Pedido(DietaIA d, string novo, string escopo = "refeicao", int dia = 0, int refeicao = 1, int item = 2,
        string atual = "peito-de-frango-sem-pele-grelhado", int? revisao = null) => new()
    {
        DietaId = d.Id, Revisao = revisao ?? d.Revisao, Dia = dia, Refeicao = refeicao, Item = item,
        AlimentoIdAtual = atual, AlimentoIdNovo = novo, Escopo = escopo
    };

    private static DietaSemanalDto Salva(ApplicationDbContext banco, int dietaId) =>
        JsonSerializer.Deserialize<DietaSemanalDto>(banco.DietasIA.AsNoTracking().Single(d => d.Id == dietaId).ConteudoJson)!;

    private static AlimentoDto Item(DietaSemanalDto dieta, int dia = 0, int refeicao = 1, int item = 2) =>
        dieta.Dias[dia].Refeicoes[refeicao].Alimentos[item];

    private static int? Status(IActionResult r) => r switch
    {
        ObjectResult o => o.StatusCode,
        StatusCodeResult s => s.StatusCode,
        _ => null
    };

    private static string? Mensagem(IActionResult resultado)
    {
        var valor = ((ObjectResult)resultado).Value!;
        return valor.GetType().GetProperty("mensagem")?.GetValue(valor) as string;
    }

    private static T Valor<T>(IActionResult r) => (T)((OkObjectResult)r).Value!;

    // ---------- Opções ----------

    [Fact]
    public async Task Opcoes_Trazem_Dieta_Revisao_Original_E_Os_Dois_Escopos()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca1@exemplo.com", ("preferido", "tilápia"));
        var dieta = NovaDieta(banco, usuario);

        var r = Valor<OpcoesTrocaResponse>(await Controller(banco, usuario.Id).Opcoes(0, 1, 2));

        Assert.Equal((dieta.Id, 0, "proteina", 7), (r.DietaId, r.Revisao, r.Nutriente, r.OcorrenciasNaSemana));
        Assert.Equal((200m, 318m, 64m), (r.Original.Quantidade, r.Original.Kcal, r.Original.NutrienteG));
        Assert.Equal(["file-de-tilapia-grelhado"], r.Refeicao.DasPreferencias.Select(o => o.AlimentoId));
        var tilapia = r.Semana.DasPreferencias.Single();
        Assert.Equal((245m, 7, 7), (tilapia.Quantidade, tilapia.Ocorrencias, tilapia.Dias.Count));
        Assert.Null(r.Refeicao.MensagemSemOpcoes);
    }

    // No lugar do pão do café, macarrão e arroz são de outro uso (prato) e vêm marcados; os de café, não.
    [Fact]
    public async Task Opcao_De_Outro_Uso_Vem_Marcada()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca25@exemplo.com");
        NovaDieta(banco, usuario);

        var r = Valor<OpcoesTrocaResponse>(await Controller(banco, usuario.Id).Opcoes(0, 0, 0)).Refeicao;
        var todas = r.DasPreferencias.Concat(r.Parecidos).Concat(r.VerMais).ToDictionary(o => o.AlimentoId);

        Assert.True(todas["macarrao-cozido"].OutroUso);
        Assert.True(todas["arroz-branco-cozido"].OutroUso);
        Assert.False(todas["pao-de-forma-integral"].OutroUso);
        Assert.False(todas["tapioca"].OutroUso);
        Assert.All(r.Parecidos, o => Assert.False(o.OutroUso, o.AlimentoId));
    }

    // Macarrão no lugar do pão: +0,2 kcal num dia de 2.954,8 = 0,00677%, que a resposta manda como 0,01.
    [Fact]
    public async Task Desvio_Do_Dia_Vem_Com_Duas_Casas()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca28@exemplo.com");
        NovaDieta(banco, usuario);

        var r = Valor<OpcoesTrocaResponse>(await Controller(banco, usuario.Id).Opcoes(0, 0, 0)).Refeicao;

        Assert.Equal(0.01m, r.VerMais.Single(o => o.AlimentoId == "macarrao-cozido").Dias.Single().DesvioPct);
        Assert.All(r.DasPreferencias.Concat(r.Parecidos).Concat(r.VerMais).SelectMany(o => o.Dias),
            d => Assert.Equal(Math.Round(d.DesvioPct, 2), d.DesvioPct));
    }

    [Fact]
    public async Task Sem_Opcao_A_Resposta_Diz_O_Porque()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca2@exemplo.com");
        NovaDieta(banco, usuario);

        // Azeite: o óleo de soja é o único do mesmo uso; com ele já no almoço, não sobra nada.
        var dieta = Dieta();
        foreach (var d in dieta.Dias) d.Refeicoes[1].Alimentos.Add(A("oleo-de-soja", 5));
        CalculoPelaBase.Calcular(dieta.Dias);
        banco.DietasIA.RemoveRange(banco.DietasIA);
        banco.SaveChanges();
        NovaDieta(banco, usuario, dieta);

        var r = Valor<OpcoesTrocaResponse>(await Controller(banco, usuario.Id).Opcoes(0, 1, 4));

        Assert.Equal("Nenhum substituto serve aqui: 1 já está nesta refeição e 2 não combinam com esta refeição.", r.Refeicao.MensagemSemOpcoes);
    }

    [Fact]
    public void Mensagem_Sem_Opcoes_Junta_Os_Motivos()
    {
        var fora = new Dictionary<MotivoForaDaTroca, int> { [MotivoForaDaTroca.PorcaoMaxima] = 3, [MotivoForaDaTroca.ForaDaMeta] = 1 };

        Assert.Equal("Nenhum substituto serve aqui: 3 passariam da porção máxima, 1 deixaria o dia mais de 5% fora da meta e 2 estão no que você marcou para evitar.",
            TrocasController.MensagemSemOpcoes(fora, 2));
        Assert.Equal("Este alimento não tem substituto entre os alimentos do Ronu.", TrocasController.MensagemSemOpcoes(new Dictionary<MotivoForaDaTroca, int>(), 0));
    }

    [Fact]
    public async Task Dieta_Anterior_A_Lista_Nao_Tem_Troca_E_Diz_Por_Que()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca3@exemplo.com");
        var antiga = Dieta();
        antiga.VersaoBase = null;
        foreach (var a in antiga.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos)) a.AlimentoId = null;
        NovaDieta(banco, usuario, antiga);

        var r = await Controller(banco, usuario.Id).Opcoes(0, 1, 2);

        Assert.Equal(422, Status(r));
        Assert.Contains("Gere uma dieta nova para trocar alimentos", Mensagem(r));
    }

    [Fact]
    public async Task Dieta_De_Outra_Versao_Da_Base_Nao_Tem_Troca_E_Diz_Por_Que()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca4@exemplo.com");
        var dieta = Dieta();
        dieta.VersaoBase = BaseNutricional.VersaoBase - 1;
        var dietaIA = NovaDieta(banco, usuario, dieta);

        var opcoes = await Controller(banco, usuario.Id).Opcoes(0, 1, 2);
        var troca = await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado"));

        Assert.Equal((422, 422), (Status(opcoes), Status(troca)));
        Assert.Equal("A base de alimentos do Ronu mudou desde que esta dieta foi gerada. Gere uma dieta nova para trocar alimentos.", Mensagem(opcoes));
    }

    [Fact]
    public async Task Posicao_Fora_Da_Dieta_Da_400()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca5@exemplo.com");
        NovaDieta(banco, usuario);

        Assert.Equal(400, Status(await Controller(banco, usuario.Id).Opcoes(7, 0, 0)));
        Assert.Equal(400, Status(await Controller(banco, usuario.Id).Opcoes(0, 1, 9)));
    }

    [Fact]
    public async Task Sem_Dieta_Da_404()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca6@exemplo.com");

        Assert.Equal(404, Status(await Controller(banco, usuario.Id).Opcoes(0, 0, 0)));
    }

    // ---------- Trocar ----------

    // Só o frango da segunda muda: 245 g de tilápia, com o original guardado e o almoço e o dia somados de novo.
    [Fact]
    public async Task Troca_Na_Refeicao_Grava_O_Substituto_O_Original_E_Os_Totais()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca7@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var antes = Dieta();

        var r = Valor<TrocaResponse>(await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado")));

        var salva = Salva(banco, dietaIA.Id);
        var novo = Item(salva);
        Assert.Equal(("file-de-tilapia-grelhado", 245m, "Filé de tilápia grelhado", 313.6m), (novo.AlimentoId, novo.Quantidade, novo.Nome, novo.Macros!.Calorias));
        Assert.Equal(("peito-de-frango-sem-pele-grelhado", 200m), (novo.Original!.AlimentoId, novo.Original.Quantidade));
        Assert.Equal(antes.Dias[0].TotalDoDia.Calorias - 4.4m, salva.Dias[0].TotalDoDia.Calorias);
        Assert.Equal(antes.Dias[0].Refeicoes[1].Macros.Calorias - 4.4m, salva.Dias[0].Refeicoes[1].Macros.Calorias);
        Assert.Equal(antes.Dias[0].MetaCalculada.Calorias, salva.Dias[0].MetaCalculada.Calorias);
        Assert.Equal("peito-de-frango-sem-pele-grelhado", Item(salva, dia: 1).AlimentoId);
        Assert.Null(Item(salva, dia: 1).Original);
        Assert.Equal((1, 1), (r.Revisao, banco.DietasIA.Single().Revisao));
        Assert.Equal(1, salva.UltimaTroca!.Revisao);
        Assert.Equal(0, r.MarcacoesRemovidas);
    }

    // Na semana, cada vez é recalculada pela sua quantidade: 200 g → 245 g; na terça, 100 g → 120 g.
    [Fact]
    public async Task Troca_Na_Semana_Muda_Todas_As_Vezes()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca8@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario, Dieta(frangoNaTerca: 100));

        Assert.IsType<OkObjectResult>(await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "semana")));

        var salva = Salva(banco, dietaIA.Id);
        Assert.All(Enumerable.Range(0, 7), d => Assert.Equal("file-de-tilapia-grelhado", Item(salva, d).AlimentoId));
        Assert.Equal(120m, Item(salva, 1).Quantidade);
        Assert.Equal(100m, Item(salva, 1).Original!.Quantidade);
        Assert.Equal(7, salva.UltimaTroca!.Itens.Count);
    }

    // Frango → tilápia → merluza: o original continua sendo o frango.
    [Fact]
    public async Task Trocar_De_Novo_Guarda_O_Primeiro_Original()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca9@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var controller = Controller(banco, usuario.Id);

        await controller.Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado"));
        await controller.Trocar(Pedido(dietaIA, "merluza-assada", atual: "file-de-tilapia-grelhado", revisao: 1));

        var novo = Item(Salva(banco, dietaIA.Id));
        Assert.Equal("merluza-assada", novo.AlimentoId);
        Assert.Equal(("peito-de-frango-sem-pele-grelhado", 200m), (novo.Original!.AlimentoId, novo.Original.Quantidade));
        Assert.Null(novo.Original.Original);
    }

    // Bisteca: +298 kcal, mais de 5% num dia na meta. O cliente não consegue forçar o que a tela não oferece.
    [Fact]
    public async Task Substituto_Fora_Das_Regras_Da_400_E_Nao_Grava()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca10@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var json = dietaIA.ConteudoJson;

        var bisteca = await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "bisteca-suina-grelhada"));
        var outroGrupo = await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "arroz-branco-cozido"));
        var inexistente = await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "whey-protein"));

        Assert.Equal((400, 400, 400), (Status(bisteca), Status(outroGrupo), Status(inexistente)));
        Assert.Equal((json, 0), (banco.DietasIA.AsNoTracking().Single().ConteudoJson, banco.DietasIA.AsNoTracking().Single().Revisao));
    }

    // As preferências de agora valem: a tilápia marcada para evitar depois da geração não é aceita.
    [Fact]
    public async Task Evitar_Atual_Vale_Na_Troca()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca11@exemplo.com", ("evitar", "peixe"));
        var dietaIA = NovaDieta(banco, usuario);

        var r = await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado"));

        Assert.Equal(400, Status(r));
    }

    [Fact]
    public async Task Escopo_Invalido_Da_400()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca12@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);

        Assert.Equal(400, Status(await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "mes"))));
    }

    // ---------- Concorrência e dono da dieta ----------

    // Outra aba gravou (revisão 1): a tela com a revisão 0 recebe 409 e não grava por cima.
    [Fact]
    public async Task Revisao_Antiga_Da_409()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca13@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado"));

        var r = await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "merluza-assada", revisao: 0));

        Assert.Equal(409, Status(r));
        Assert.Equal("Sua dieta mudou desde que a tela foi aberta. Atualize e tente de novo.", Mensagem(r));
        Assert.Equal("file-de-tilapia-grelhado", Item(Salva(banco, dietaIA.Id)).AlimentoId);
    }

    [Fact]
    public async Task Alimento_Diferente_Na_Posicao_Da_409()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca14@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);

        var r = await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", atual: "patinho-bovino-grelhado"));

        Assert.Equal(409, Status(r));
    }

    // O dietaId de outra pessoa só é comparado com a dieta de quem chama: 409, sem tocar na dieta dela.
    [Fact]
    public async Task Dieta_De_Outra_Pessoa_Nao_E_Tocada()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var dona = NovoUsuario(banco, "troca15a@exemplo.com");
        var outra = NovoUsuario(banco, "troca15b@exemplo.com");
        var dietaDaDona = NovaDieta(banco, dona);
        NovaDieta(banco, outra);
        var json = dietaDaDona.ConteudoJson;

        var troca = await Controller(banco, outra.Id).Trocar(Pedido(dietaDaDona, "file-de-tilapia-grelhado"));
        var desfazer = await Controller(banco, outra.Id).Desfazer(dietaDaDona.Id, 0);
        var voltar = await Controller(banco, outra.Id).VoltarAoOriginal(dietaDaDona.Id, 0, 0, 1, 2, "refeicao");

        Assert.Equal((409, 409, 409), (Status(troca), Status(desfazer), Status(voltar)));
        Assert.Equal(json, banco.DietasIA.AsNoTracking().Single(d => d.Id == dietaDaDona.Id).ConteudoJson);
    }

    // Duas gravações lidas na mesma revisão: a segunda não passa pelo token de concorrência do banco.
    [Fact]
    public async Task Revisao_E_Token_De_Concorrencia()
    {
        var nome = Guid.NewGuid().ToString();
        using (var banco = BancoEmMemoria.NovoContexto(nome))
        {
            NovaDieta(banco, NovoUsuario(banco, "troca16@exemplo.com"));
        }

        using var aba1 = BancoEmMemoria.NovoContexto(nome);
        using var aba2 = BancoEmMemoria.NovoContexto(nome);
        var d1 = aba1.DietasIA.Single();
        var d2 = aba2.DietasIA.Single();
        d1.Revisao++;
        d2.Revisao++;
        await aba1.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => aba2.SaveChangesAsync());
    }

    // ---------- Desfazer e voltar ao original ----------

    [Fact]
    public async Task Desfazer_Volta_Exatamente_Ao_Que_Estava()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca17@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var json = dietaIA.ConteudoJson;
        var controller = Controller(banco, usuario.Id);
        await controller.Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "semana"));

        var r = await controller.Desfazer(dietaIA.Id, 1);

        Assert.IsType<OkObjectResult>(r);
        Assert.Equal(json, JsonSerializer.Serialize(Salva(banco, dietaIA.Id)));
        Assert.Equal(2, banco.DietasIA.Single().Revisao);
        Assert.Equal(409, Status(await controller.Desfazer(dietaIA.Id, 2)));
    }

    // Frango → tilápia → merluza: desfazer volta à tilápia (a última troca), não ao frango.
    [Fact]
    public async Task Desfazer_Volta_A_Ultima_Troca_E_Nao_Ao_Original()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca18@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var controller = Controller(banco, usuario.Id);
        await controller.Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado"));
        await controller.Trocar(Pedido(dietaIA, "merluza-assada", atual: "file-de-tilapia-grelhado", revisao: 1));

        await controller.Desfazer(dietaIA.Id, 2);

        var item = Item(Salva(banco, dietaIA.Id));
        Assert.Equal(("file-de-tilapia-grelhado", 245m), (item.AlimentoId, item.Quantidade));
        Assert.Equal("peito-de-frango-sem-pele-grelhado", item.Original!.AlimentoId);
    }

    [Fact]
    public async Task Voltar_Ao_Original_So_Nesta_Refeicao()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca19@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var antes = Dieta();
        var controller = Controller(banco, usuario.Id);
        await controller.Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "semana"));
        await controller.Trocar(Pedido(dietaIA, "merluza-assada", atual: "file-de-tilapia-grelhado", revisao: 1));

        Assert.IsType<OkObjectResult>(await controller.VoltarAoOriginal(dietaIA.Id, 2, 0, 1, 2, "refeicao"));

        var salva = Salva(banco, dietaIA.Id);
        var item = Item(salva);
        Assert.Equal(("peito-de-frango-sem-pele-grelhado", 200m, 318m), (item.AlimentoId, item.Quantidade, item.Macros!.Calorias));
        Assert.Null(item.Original);
        Assert.Equal(antes.Dias[0].TotalDoDia.Calorias, salva.Dias[0].TotalDoDia.Calorias);
        Assert.Equal("file-de-tilapia-grelhado", Item(salva, 1).AlimentoId);
    }

    [Fact]
    public async Task Voltar_Ao_Original_Na_Semana_Volta_Todos_Os_Trocados_Do_Mesmo_Jeito()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca20@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var json = dietaIA.ConteudoJson;
        var controller = Controller(banco, usuario.Id);
        await controller.Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "semana"));

        await controller.VoltarAoOriginal(dietaIA.Id, 1, 3, 1, 2, "semana");

        var salva = Salva(banco, dietaIA.Id);
        salva.UltimaTroca = null;
        Assert.Equal(json, JsonSerializer.Serialize(salva));
    }

    [Fact]
    public async Task Voltar_Ao_Original_De_Item_Nao_Trocado_Da_400()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca21@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);

        var r = await Controller(banco, usuario.Id).VoltarAoOriginal(dietaIA.Id, 0, 0, 1, 2, "refeicao");

        Assert.Equal(400, Status(r));
        Assert.Equal("Este alimento não foi trocado.", Mensagem(r));
    }

    // ---------- Lista de compras (D5) ----------

    private static string Chave(DietaSemanalDto dieta, string alimentoId) =>
        MontadorListaCompras.Montar(dieta, "SP").Secoes.SelectMany(s => s.Itens)
            .Single(i => i.Detalhe.Origens.Any(o => o.AlimentoId == alimentoId)).Chave;

    [Fact]
    public void Marca_Cai_Quando_O_Item_Sai_Ou_Sobe_E_Fica_Quando_Desce()
    {
        var antes = Dieta();
        var chaveFrango = Chave(antes, "peito-de-frango-sem-pele-grelhado");
        var chaveArroz = Chave(antes, "arroz-branco-cozido");

        var menos = Dieta();
        foreach (var d in menos.Dias) d.Refeicoes[1].Alimentos[2].Quantidade = 150;
        var mais = Dieta();
        foreach (var d in mais.Dias) d.Refeicoes[1].Alimentos[2].Quantidade = 260;
        var semArroz = Dieta();
        foreach (var d in semArroz.Dias) d.Refeicoes[1].Alimentos[0] = A("macarrao-cozido", 275);

        Assert.DoesNotContain(chaveFrango, MarcacoesAposTroca.QueCaem(antes, menos, "SP"));
        Assert.Contains(chaveFrango, MarcacoesAposTroca.QueCaem(antes, mais, "SP"));
        var caem = MarcacoesAposTroca.QueCaem(antes, semArroz, "SP");
        Assert.Contains(chaveArroz, caem);
        // O macarrão do jantar passou a pedir mais: a marca dele também cai.
        Assert.Contains(Chave(antes, "macarrao-cozido"), caem);
    }

    [Fact]
    public async Task Troca_Tira_A_Marca_Do_Item_Que_Saiu_E_Deixa_As_Outras()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca22@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var chaveFrango = Chave(Dieta(), "peito-de-frango-sem-pele-grelhado");
        var chaveOvo = Chave(Dieta(), "ovo-de-galinha-cozido");
        foreach (var chave in new[] { chaveFrango, chaveOvo })
        {
            banco.ListaComprasMarcacoes.Add(new ListaComprasMarcacao { UsuarioId = usuario.Id, DietaIAId = dietaIA.Id, Chave = chave, MarcadoEm = DateTime.UtcNow });
        }

        banco.SaveChanges();

        var r = Valor<TrocaResponse>(await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "semana")));

        Assert.Equal([chaveOvo], banco.ListaComprasMarcacoes.Select(m => m.Chave));
        Assert.Equal(1, r.MarcacoesRemovidas);
    }

    // A contagem é das marcações que existiam: a chave do frango deixou de valer, mas ninguém a tinha marcado.
    [Fact]
    public async Task Marcacoes_Removidas_Conta_So_As_Que_Existiam()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca26@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        banco.ListaComprasMarcacoes.Add(new ListaComprasMarcacao
        {
            UsuarioId = usuario.Id, DietaIAId = dietaIA.Id, Chave = Chave(Dieta(), "ovo-de-galinha-cozido"), MarcadoEm = DateTime.UtcNow
        });
        banco.SaveChanges();

        var r = Valor<TrocaResponse>(await Controller(banco, usuario.Id).Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "semana")));

        Assert.Equal(0, r.MarcacoesRemovidas);
        Assert.Single(banco.ListaComprasMarcacoes);
    }

    // Desfazer e voltar ao original também contam: a tilápia marcada sai da lista quando o frango volta.
    [Fact]
    public async Task Desfazer_E_Voltar_Ao_Original_Contam_As_Marcacoes_Que_Caem()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca27@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var controller = Controller(banco, usuario.Id);
        await controller.Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "semana"));
        void MarcarTilapia() => banco.ListaComprasMarcacoes.Add(new ListaComprasMarcacao
        {
            UsuarioId = usuario.Id, DietaIAId = dietaIA.Id, Chave = Chave(Salva(banco, dietaIA.Id), "file-de-tilapia-grelhado"),
            MarcadoEm = DateTime.UtcNow
        });

        MarcarTilapia();
        banco.SaveChanges();
        var desfeita = Valor<TrocaResponse>(await controller.Desfazer(dietaIA.Id, 1));

        await controller.Trocar(Pedido(dietaIA, "file-de-tilapia-grelhado", "semana", revisao: 2));
        MarcarTilapia();
        banco.SaveChanges();
        var voltou = Valor<TrocaResponse>(await controller.VoltarAoOriginal(dietaIA.Id, 3, 0, 1, 2, "semana"));

        Assert.Equal((1, 1), (desfeita.MarcacoesRemovidas, voltou.MarcacoesRemovidas));
        Assert.Empty(banco.ListaComprasMarcacoes);
    }

    // ---------- Formato e limites ----------

    // Dietas sem troca continuam com o mesmo JSON: os campos novos só aparecem quando há o que guardar.
    [Fact]
    public void Dieta_Sem_Troca_Nao_Ganha_Campos_Novos_No_Json()
    {
        var json = JsonSerializer.Serialize(Dieta());

        Assert.DoesNotContain("\"Original\"", json);
        Assert.DoesNotContain("\"UltimaTroca\"", json);
    }

    [Fact]
    public async Task Get_Atual_Traz_Id_Revisao_E_Se_Tem_Troca()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca23@exemplo.com");
        var dietaIA = NovaDieta(banco, usuario);
        var dietas = new DietasController(banco, null!, null!, null!, NullLogger<DietasController>.Instance)
        {
            ControllerContext = Controller(banco, usuario.Id).ControllerContext
        };

        var r = Valor<DietaResponse>(await dietas.ObterAtual());

        Assert.Equal((dietaIA.Id, 0, (string?)null), (r.DietaId, r.Revisao, r.SemTroca));
    }

    [Theory]
    [InlineData(nameof(TrocasController.Opcoes), LimitesTroca.PoliticaLeitura)]
    [InlineData(nameof(TrocasController.Trocar), LimitesTroca.PoliticaEscrita)]
    [InlineData(nameof(TrocasController.Desfazer), LimitesTroca.PoliticaEscrita)]
    [InlineData(nameof(TrocasController.VoltarAoOriginal), LimitesTroca.PoliticaEscrita)]
    public void Toda_Rota_Tem_Limite_Por_Usuario(string metodo, string politica)
    {
        var atributo = typeof(TrocasController).GetMethod(metodo)!.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.Equal(politica, atributo?.PolicyName);
    }
}
