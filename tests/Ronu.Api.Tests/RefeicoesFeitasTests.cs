using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Refeição feita, etapa 2: GET e PUT /api/dietas/atual/refeicoes-feitas, as datas marcáveis (hoje e ontem, só na dieta
/// atual) e o "hoje" no fuso da UF. Só contas fictícias.
/// </summary>
public class RefeicoesFeitasTests
{
    // ---------- Apoio ----------

    private static MacrosDto Macros(decimal kcal) => new() { Calorias = kcal, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    // Os 7 dias, cada um com 5 refeições de 300, 400, 500, 600 e 700,25 kcal. Fora de ordem quando pedido (a lista da IA
    // pode vir em outra ordem): o dia é casado pelo nome.
    private static DietaSemanalDto Dieta(bool foraDeOrdem = false, bool daCasa = false)
    {
        var nomes = foraDeOrdem ? GeradorDietaGemini.NomesDias.Reverse() : GeradorDietaGemini.NomesDias;
        return new DietaSemanalDto
        {
            Dias = nomes.Select(nome => new DiaDietaDto
            {
                DiaSemana = nome,
                Refeicoes = new[] { 300m, 400m, 500m, 600m, 700.25m }.Select((kcal, i) => new RefeicaoDto
                {
                    Nome = $"Refeição {i}",
                    Alimentos = [],
                    Macros = Macros(kcal),
                    DaCasa = daCasa && i == 2
                }).ToList(),
                TotalDoDia = Macros(2500.25m),
                MetaCalculada = Macros(2500)
            }).ToList()
        };
    }

    // Sem UF (contas antigas ou via Google): Estado vazio, que cai no fuso de Brasília.
    private static Usuario NovoUsuario(ApplicationDbContext banco, string email, string? uf = "SP")
    {
        var usuario = new Usuario { Nome = "Teste", Email = email, Estado = uf ?? string.Empty };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        return usuario;
    }

    private static DietaIA NovaDieta(ApplicationDbContext banco, Usuario usuario, DateTime quando, DietaSemanalDto? dieta = null, string origem = DietaIA.OrigemPessoa)
    {
        var dietaIA = new DietaIA { UsuarioId = usuario.Id, DataGeracao = quando, ConteudoJson = JsonSerializer.Serialize(dieta ?? Dieta()), Origem = origem };
        banco.DietasIA.Add(dietaIA);
        banco.SaveChanges();
        return dietaIA;
    }

    private static RefeicoesFeitasController Controller(ApplicationDbContext banco, int usuarioId, DateTime agora) => new(banco)
    {
        Relogio = () => agora,
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"))
            }
        }
    };

    private static MarcacaoRefeicaoRequest Pedido(int dietaId, bool feita) => new() { DietaId = dietaId, Feita = feita };

    private static async Task<RefeicoesFeitasResponse> Ler(ApplicationDbContext banco, int usuarioId, DateTime agora) =>
        Assert.IsType<RefeicoesFeitasResponse>(Assert.IsType<OkObjectResult>(await Controller(banco, usuarioId, agora).Obter()).Value);

    private static string? Mensagem(IActionResult resultado)
    {
        var valor = ((ObjectResult)resultado).Value!;
        return valor.GetType().GetProperty("mensagem")?.GetValue(valor) as string;
    }

    // Segunda-feira, 16/11/2026, 15h UTC (12h em Brasília). A dieta foi gerada na terça anterior.
    private static readonly DateTime Agora = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SemanaPassada = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoje = new(2026, 11, 16);
    private static readonly DateOnly Ontem = new(2026, 11, 15);

    // ---------- GET ----------

    [Fact]
    public async Task Sem_Dieta_O_Get_E_O_Put_Dao_404()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "semdieta@exemplo.test");
        var controller = Controller(banco, usuario.Id, Agora);

        Assert.IsType<NotFoundObjectResult>(await controller.Obter());
        Assert.IsType<NotFoundObjectResult>(await controller.Marcar("2026-11-16", 0, Pedido(1, true)));
    }

    [Fact]
    public async Task Get_Traz_Hoje_E_Ontem_Com_O_Indice_Do_Dia_Pelo_Nome_Mesmo_Com_A_Dieta_Fora_De_Ordem()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "get@exemplo.test");
        // Fora de ordem: Domingo é o índice 0 e Segunda-feira o 6.
        var dieta = NovaDieta(banco, usuario, SemanaPassada, Dieta(foraDeOrdem: true));

        var resposta = await Ler(banco, usuario.Id, Agora);

        Assert.Equal(dieta.Id, resposta.DietaId);
        Assert.Equal(Hoje, resposta.Hoje);
        Assert.Collection(resposta.Dias,
            d => Assert.Equal((Ontem, (int?)0, 0), (d.Data, d.IndiceDia, d.Marcadas.Count)),
            d => Assert.Equal((Hoje, (int?)6, 0), (d.Data, d.IndiceDia, d.Marcadas.Count)));
    }

    [Fact]
    public async Task Get_Em_Json_Tem_As_Datas_No_Formato_Do_Calendario()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "json@exemplo.test");
        NovaDieta(banco, usuario, SemanaPassada);

        var json = JsonSerializer.Serialize(await Ler(banco, usuario.Id, Agora), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"hoje\":\"2026-11-16\"", json);
        Assert.Contains("\"data\":\"2026-11-15\",\"indiceDia\":6,\"marcadas\":[]", json);
    }

    [Fact]
    public async Task Dieta_Gerada_Hoje_So_Tem_Hoje()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "geradahoje@exemplo.test");
        var dieta = NovaDieta(banco, usuario, Agora.AddHours(-2));

        var resposta = await Ler(banco, usuario.Id, Agora);

        Assert.Equal(Hoje, Assert.Single(resposta.Dias).Data);
        var resultado = await Controller(banco, usuario.Id, Agora).Marcar("2026-11-15", 0, Pedido(dieta.Id, true));
        Assert.IsType<UnprocessableEntityObjectResult>(resultado);
    }

    // AC às 22h30 locais = 03h30 UTC do dia seguinte. Em SP, no mesmo instante, já é o dia seguinte.
    [Theory]
    [InlineData("AC", "2026-11-16")]
    [InlineData("AM", "2026-11-16")]
    [InlineData("SP", "2026-11-17")]
    [InlineData(null, "2026-11-17")]
    public async Task Hoje_E_O_Dia_Do_Fuso_Da_Uf(string? uf, string hojeEsperado)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "fuso@exemplo.test", uf);
        NovaDieta(banco, usuario, SemanaPassada);
        var agora = new DateTime(2026, 11, 17, 3, 30, 0, DateTimeKind.Utc);

        var resposta = await Ler(banco, usuario.Id, agora);

        Assert.Equal(DateOnly.Parse(hojeEsperado), resposta.Hoje);
        Assert.Equal(DateOnly.Parse(hojeEsperado), resposta.Dias[^1].Data);
    }

    [Fact]
    public async Task Dieta_Gerada_No_Fim_Da_Noite_Do_Acre_Vale_Do_Dia_Local()
    {
        // Gerada às 22h de domingo no AC (03h UTC de segunda): na segunda, ontem (domingo) já é marcável nela.
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "acre@exemplo.test", "AC");
        NovaDieta(banco, usuario, new DateTime(2026, 11, 16, 3, 0, 0, DateTimeKind.Utc));

        var resposta = await Ler(banco, usuario.Id, Agora);

        Assert.Equal(new[] { Ontem, Hoje }, resposta.Dias.Select(d => d.Data));
    }

    // ---------- PUT ----------

    [Fact]
    public async Task Marcar_E_Desmarcar_E_Idempotente_E_Grava_As_Kcal_Da_Dieta()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marcar@exemplo.test");
        var dieta = NovaDieta(banco, usuario, SemanaPassada);
        var controller = Controller(banco, usuario.Id, Agora);

        Assert.IsType<NoContentResult>(await controller.Marcar("2026-11-16", 4, Pedido(dieta.Id, true)));
        Assert.IsType<NoContentResult>(await controller.Marcar("2026-11-16", 4, Pedido(dieta.Id, true)));

        var marcacao = Assert.Single(banco.RefeicoesFeitas);
        Assert.Equal((usuario.Id, dieta.Id, Hoje, 4), (marcacao.UsuarioId, marcacao.DietaIAId, marcacao.Data, marcacao.RefeicaoIndice));
        // Do servidor: 700,25 arredondado a uma casa, e as 5 refeições do dia.
        Assert.Equal(700.3m, marcacao.Kcal);
        Assert.Equal(5, marcacao.RefeicoesNoDia);
        Assert.Equal(new[] { 4 }, (await Ler(banco, usuario.Id, Agora)).Dias[^1].Marcadas);

        Assert.IsType<NoContentResult>(await controller.Marcar("2026-11-16", 4, Pedido(dieta.Id, false)));
        Assert.IsType<NoContentResult>(await controller.Marcar("2026-11-16", 4, Pedido(dieta.Id, false)));
        Assert.Empty(banco.RefeicoesFeitas);
    }

    [Fact]
    public async Task Ontem_E_Hoje_Ficam_Separados_E_Em_Ordem()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "ontem@exemplo.test");
        var dieta = NovaDieta(banco, usuario, SemanaPassada);
        var controller = Controller(banco, usuario.Id, Agora);

        foreach (var (data, refeicao) in new[] { ("2026-11-15", 3), ("2026-11-15", 0), ("2026-11-16", 1) })
        {
            Assert.IsType<NoContentResult>(await controller.Marcar(data, refeicao, Pedido(dieta.Id, true)));
        }

        var resposta = await Ler(banco, usuario.Id, Agora);
        Assert.Equal(new[] { 0, 3 }, resposta.Dias[0].Marcadas);
        Assert.Equal(new[] { 1 }, resposta.Dias[1].Marcadas);
    }

    [Theory]
    [InlineData("2026-11-17")] // amanhã
    [InlineData("2026-11-14")] // anteontem
    [InlineData("2026-11-09")] // antes de a dieta valer
    public async Task Data_Fora_De_Hoje_E_Ontem_Da_422(string data)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "fora@exemplo.test");
        var dieta = NovaDieta(banco, usuario, SemanaPassada);

        var resultado = await Controller(banco, usuario.Id, Agora).Marcar(data, 0, Pedido(dieta.Id, true));

        Assert.IsType<UnprocessableEntityObjectResult>(resultado);
        Assert.Empty(banco.RefeicoesFeitas);
    }

    [Theory]
    [InlineData("16-11-2026")]
    [InlineData("2026-11-31")]
    [InlineData("hoje")]
    [InlineData("2026-11-16T00:00:00")]
    public async Task Data_Mal_Formada_Da_400(string data)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "formato@exemplo.test");
        var dieta = NovaDieta(banco, usuario, SemanaPassada);

        Assert.IsType<BadRequestObjectResult>(await Controller(banco, usuario.Id, Agora).Marcar(data, 0, Pedido(dieta.Id, true)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(99)]
    public async Task Refeicao_Fora_Do_Dia_Da_400(int refeicao)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "refeicao@exemplo.test");
        var dieta = NovaDieta(banco, usuario, SemanaPassada);

        var resultado = await Controller(banco, usuario.Id, Agora).Marcar("2026-11-16", refeicao, Pedido(dieta.Id, true));

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(banco.RefeicoesFeitas);
    }

    [Fact]
    public async Task Dieta_Antiga_Ou_De_Outra_Pessoa_Da_409()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "conflito@exemplo.test");
        var outro = NovoUsuario(banco, "outro@exemplo.test");
        var antiga = NovaDieta(banco, usuario, SemanaPassada.AddDays(-7));
        NovaDieta(banco, usuario, SemanaPassada);
        var doOutro = NovaDieta(banco, outro, SemanaPassada.AddDays(1));
        var controller = Controller(banco, usuario.Id, Agora);

        var comAntiga = await controller.Marcar("2026-11-16", 0, Pedido(antiga.Id, true));
        var comDoOutro = await controller.Marcar("2026-11-16", 0, Pedido(doOutro.Id, true));

        Assert.IsType<ConflictObjectResult>(comAntiga);
        Assert.IsType<ConflictObjectResult>(comDoOutro);
        Assert.Contains("mudou", Mensagem(comAntiga));
        Assert.Empty(banco.RefeicoesFeitas);
    }

    [Fact]
    public async Task Marcar_Nao_Mexe_Na_Dieta_Nem_Na_Revisao()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "revisao@exemplo.test");
        var dieta = NovaDieta(banco, usuario, SemanaPassada);
        dieta.Revisao = 3;
        banco.SaveChanges();
        var conteudo = dieta.ConteudoJson;

        Assert.IsType<NoContentResult>(await Controller(banco, usuario.Id, Agora).Marcar("2026-11-16", 0, Pedido(dieta.Id, true)));

        var depois = await banco.DietasIA.AsNoTracking().SingleAsync();
        Assert.Equal((3, conteudo), (depois.Revisao, depois.ConteudoJson));
    }

    [Fact]
    public async Task Troca_De_Alimento_Depois_De_Marcar_Mantem_A_Marcacao_E_As_Kcal_Da_Hora_Do_Toque()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "troca@exemplo.test");
        var dieta = NovaDieta(banco, usuario, SemanaPassada);
        Assert.IsType<NoContentResult>(await Controller(banco, usuario.Id, Agora).Marcar("2026-11-16", 1, Pedido(dieta.Id, true)));

        // A troca regrava o JSON e sobe a revisão; a refeição fica no mesmo lugar, com outras kcal.
        var trocada = Dieta();
        trocada.Dias[0].Refeicoes[1].Macros = Macros(999);
        dieta.ConteudoJson = JsonSerializer.Serialize(trocada);
        dieta.Revisao++;
        banco.SaveChanges();

        var marcacao = Assert.Single(banco.RefeicoesFeitas);
        Assert.Equal(400m, marcacao.Kcal);
        Assert.Equal(new[] { 1 }, (await Ler(banco, usuario.Id, Agora)).Dias[^1].Marcadas);
    }

    [Fact]
    public async Task Dieta_Nova_Comeca_Sem_Marcacoes_E_As_Da_Antiga_Ficam_No_Banco()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "nova@exemplo.test");
        var antiga = NovaDieta(banco, usuario, SemanaPassada);
        Assert.IsType<NoContentResult>(await Controller(banco, usuario.Id, Agora).Marcar("2026-11-16", 0, Pedido(antiga.Id, true)));

        var nova = NovaDieta(banco, usuario, Agora.AddMinutes(30));
        var resposta = await Ler(banco, usuario.Id, Agora.AddHours(1));

        Assert.Equal(nova.Id, resposta.DietaId);
        Assert.Empty(Assert.Single(resposta.Dias).Marcadas);
        Assert.Equal(antiga.Id, Assert.Single(banco.RefeicoesFeitas).DietaIAId);
    }

    // ---------- Plano família ----------

    [Fact]
    public async Task Na_Casa_A_Marcacao_E_Pessoal()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var membro = NovoUsuario(banco, "membro@exemplo.test");
        var dono = NovoUsuario(banco, "dono@exemplo.test");
        // A semana da casa criou uma dieta para cada um, na mesma hora, com o almoço da casa (posição 2).
        var dietaMembro = NovaDieta(banco, membro, SemanaPassada, Dieta(daCasa: true), DietaIA.OrigemCasa);
        NovaDieta(banco, dono, SemanaPassada, Dieta(daCasa: true), DietaIA.OrigemCasa);

        Assert.IsType<NoContentResult>(await Controller(banco, membro.Id, Agora).Marcar("2026-11-16", 2, Pedido(dietaMembro.Id, true)));

        Assert.Equal(new[] { 2 }, (await Ler(banco, membro.Id, Agora)).Dias[^1].Marcadas);
        Assert.All((await Ler(banco, dono.Id, Agora)).Dias, d => Assert.Empty(d.Marcadas));
        Assert.Equal(membro.Id, Assert.Single(banco.RefeicoesFeitas).UsuarioId);
    }

    // ---------- Limite e consentimento ----------

    [Fact]
    public void As_Rotas_Tem_Limite_E_Exigem_O_Consentimento()
    {
        var tipo = typeof(RefeicoesFeitasController);
        var obter = tipo.GetMethod(nameof(RefeicoesFeitasController.Obter))!;
        var marcar = tipo.GetMethod(nameof(RefeicoesFeitasController.Marcar))!;

        Assert.Equal(LimitesRefeicoesFeitas.PoliticaLeitura, obter.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(LimitesRefeicoesFeitas.PoliticaEscrita, marcar.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Null(tipo.GetCustomAttribute<DispensaConsentimentoAttribute>());
        Assert.Null(obter.GetCustomAttribute<DispensaConsentimentoAttribute>());
        Assert.Null(marcar.GetCustomAttribute<DispensaConsentimentoAttribute>());
    }

    // ---------- Regras puras ----------

    [Theory]
    [InlineData(2026, 11, 16, "Segunda-feira")]
    [InlineData(2026, 11, 21, "Sábado")]
    [InlineData(2026, 11, 22, "Domingo")]
    public void Indice_Do_Dia_Casa_Pelo_Nome(int ano, int mes, int dia, string nome)
    {
        var dieta = Dieta(foraDeOrdem: true);

        var indice = RegrasRefeicoesFeitas.IndiceDoDia(dieta, new DateOnly(ano, mes, dia));

        Assert.Equal(nome, dieta.Dias[indice!.Value].DiaSemana);
    }

    [Fact]
    public void Indice_Do_Dia_E_Nulo_Se_A_Dieta_Nao_Tem_O_Dia()
    {
        var dieta = Dieta();
        dieta.Dias.RemoveAt(0); // sem segunda

        Assert.Null(RegrasRefeicoesFeitas.IndiceDoDia(dieta, Hoje));
    }
}
