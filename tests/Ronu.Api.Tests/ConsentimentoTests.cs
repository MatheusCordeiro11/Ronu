using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;

namespace Ronu.Api.Tests;

/// <summary>
/// LGPD base, etapa 1: o consentimento geral. Sem aceite não segue (filtro 403 e cadastro), o aceite grava tipo,
/// versão e data, o usuário vem sempre do token, e as datas novas do Usuario (CriadoEm, UltimoLoginEm). Só contas
/// fictícias.
/// </summary>
public class ConsentimentoTests
{
    private static readonly int Vigente = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoGeral);

    // ---------- Apoio ----------

    private static async Task<Usuario> NovoUsuario(ApplicationDbContext banco, string email, string? senha = null)
    {
        var usuario = new Usuario
        {
            Nome = "Fulano Fictício", Email = email, Estado = "SP",
            SenhaHash = senha is null ? null : BCrypt.Net.BCrypt.HashPassword(senha)
        };
        banco.Usuarios.Add(usuario);
        await banco.SaveChangesAsync();
        return usuario;
    }

    private static async Task Aceitar(ApplicationDbContext banco, int usuarioId, int versao, DateTime? revogadoEm = null)
    {
        banco.Consentimentos.Add(new Consentimento
        {
            UsuarioId = usuarioId, Tipo = TextosConsentimento.TipoGeral, Versao = versao,
            AceitoEm = DateTime.UtcNow.AddDays(-1), RevogadoEm = revogadoEm
        });
        await banco.SaveChangesAsync();
    }

    private static ClaimsPrincipal Logado(int usuarioId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"));

    private static ConsentimentosController Controller(ApplicationDbContext banco, int usuarioId) => new(banco)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Logado(usuarioId) } }
    };

    private static IConfiguration Config(bool? exigir) =>
        BancoEmMemoria.Configuracao(exigir is null ? null : new() { [RegrasConsentimento.ChaveExigir] = exigir.Value.ToString() });

    // Roda o filtro como o MVC roda, com os metadados que a rota teria. Devolve o resultado que ele deixou (nulo = passou).
    private static async Task<IActionResult?> Filtrar(ApplicationDbContext banco, int? usuarioId, bool? exigir, params object[] metadados)
    {
        var http = new DefaultHttpContext();
        if (usuarioId is not null)
        {
            http.User = Logado(usuarioId.Value);
        }

        var acao = new ActionContext(http, new RouteData(), new ActionDescriptor { EndpointMetadata = metadados.ToList() });
        var contexto = new AuthorizationFilterContext(acao, new List<IFilterMetadata>());
        await new FiltroConsentimento(banco, Config(exigir)).OnAuthorizationAsync(contexto);
        return contexto.Result;
    }

    private static void AssertBarrado(IActionResult? resultado)
    {
        var objeto = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(403, objeto.StatusCode);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(objeto.Value));
        Assert.Equal(RegrasConsentimento.MotivoSemAceite, json.RootElement.GetProperty("motivo").GetString());
        Assert.Equal(RegrasConsentimento.MensagemSemAceite, json.RootElement.GetProperty("mensagem").GetString());
    }

    // ---------- Filtro: sem aceite não segue ----------

    [Fact]
    public async Task Rota_Com_Login_Sem_Aceite_Da_403_Com_O_Motivo()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "sem-aceite@exemplo.test");

        AssertBarrado(await Filtrar(banco, usuario.Id, exigir: null, new AuthorizeAttribute()));
    }

    [Fact]
    public async Task Com_O_Aceite_Vigente_Passa()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "aceitou@exemplo.test");
        await Aceitar(banco, usuario.Id, Vigente);

        Assert.Null(await Filtrar(banco, usuario.Id, exigir: null, new AuthorizeAttribute()));
    }

    [Fact]
    public async Task Aceite_De_Versao_Antiga_Nao_Vale()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "versao-velha@exemplo.test");
        await Aceitar(banco, usuario.Id, Vigente - 1);

        AssertBarrado(await Filtrar(banco, usuario.Id, exigir: null, new AuthorizeAttribute()));
    }

    [Fact]
    public async Task Aceite_Revogado_Nao_Vale()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "revogou@exemplo.test");
        await Aceitar(banco, usuario.Id, Vigente, revogadoEm: DateTime.UtcNow);

        AssertBarrado(await Filtrar(banco, usuario.Id, exigir: null, new AuthorizeAttribute()));
    }

    [Fact]
    public async Task Aceite_De_Outra_Conta_Nao_Vale()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var aceitou = await NovoUsuario(banco, "aceitou@exemplo.test");
        var outra = await NovoUsuario(banco, "outra@exemplo.test");
        await Aceitar(banco, aceitou.Id, Vigente);

        AssertBarrado(await Filtrar(banco, outra.Id, exigir: null, new AuthorizeAttribute()));
    }

    [Fact]
    public async Task Interruptor_Desligado_Deixa_Passar()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "sem-aceite@exemplo.test");

        Assert.Null(await Filtrar(banco, usuario.Id, exigir: false, new AuthorizeAttribute()));
    }

    [Fact]
    public async Task Interruptor_Ligado_Explicito_Barra()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "sem-aceite@exemplo.test");

        AssertBarrado(await Filtrar(banco, usuario.Id, exigir: true, new AuthorizeAttribute()));
    }

    [Fact]
    public void Sem_A_Chave_Na_Configuracao_O_Consentimento_E_Exigido()
    {
        Assert.True(RegrasConsentimento.Exigir(Config(null)));
    }

    [Fact]
    public async Task Rota_Publica_E_Rota_Dispensada_Passam()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "sem-aceite@exemplo.test");

        Assert.Null(await Filtrar(banco, usuario.Id, exigir: null));
        Assert.Null(await Filtrar(banco, usuario.Id, exigir: null, new AuthorizeAttribute(), new AllowAnonymousAttribute()));
        Assert.Null(await Filtrar(banco, usuario.Id, exigir: null, new AuthorizeAttribute(), new DispensaConsentimentoAttribute()));
    }

    // Uma rota nova com [DispensaConsentimento] precisa entrar nesta lista de propósito, não por engano. Apagar a rotina
    // antiga entrou em 2026-11-13: apagar o próprio dado é direito (art. 18, VI).
    [Fact]
    public void So_O_Aceite_E_A_Conta_Dispensam_O_Consentimento()
    {
        var dispensados = typeof(ConsentimentosController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => new MemberInfo[] { t }.Concat(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
            .Where(m => m.GetCustomAttribute<DispensaConsentimentoAttribute>() is not null)
            .Select(m => m is Type t ? t.Name : $"{m.DeclaringType!.Name}.{m.Name}")
            .Order()
            .ToList();

        Assert.Equal(new[] { nameof(ConsentimentosController), nameof(ContaController), $"{nameof(PerfilController)}.{nameof(PerfilController.ApagarRotina)}" }, dispensados);
    }

    // Com os metadados reais da rota (o [Authorize] da classe e os atributos do método), o filtro deixa passar uma
    // conta sem aceite, e a rota apaga a rotina dela, só dela.
    [Fact]
    public async Task Apagar_Rotina_Funciona_Em_Conta_Sem_Aceite()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "rotina-sem-aceite@exemplo.test");
        var outro = await NovoUsuario(banco, "rotina-outro@exemplo.test");
        usuario.RotinaDiaria = "Acordo às 6h";
        outro.RotinaDiaria = "Durmo às 23h";
        await banco.SaveChangesAsync();

        var metadados = typeof(PerfilController).GetCustomAttributes(true)
            .Concat(typeof(PerfilController).GetMethod(nameof(PerfilController.ApagarRotina))!.GetCustomAttributes(true))
            .ToArray();
        Assert.Contains(metadados, m => m is AuthorizeAttribute);

        Assert.Null(await Filtrar(banco, usuario.Id, exigir: true, metadados));

        var controller = new PerfilController(banco)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Logado(usuario.Id) } }
        };
        Assert.IsType<NoContentResult>(await controller.ApagarRotina());
        Assert.Null(banco.Usuarios.AsNoTracking().Single(u => u.Id == usuario.Id).RotinaDiaria);
        Assert.Equal("Durmo às 23h", banco.Usuarios.AsNoTracking().Single(u => u.Id == outro.Id).RotinaDiaria);
    }

    // ---------- GET e POST /api/consentimentos ----------

    [Fact]
    public async Task Consulta_Sem_Aceite_Traz_O_Texto_E_Pede_Aceite()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "novo@exemplo.test");

        var resposta = Assert.IsType<ConsentimentoResponse>(Assert.IsType<OkObjectResult>(
            await Controller(banco, usuario.Id).Consultar(TextosConsentimento.TipoGeral)).Value);

        Assert.Equal(Vigente, resposta.VersaoVigente);
        Assert.False(string.IsNullOrWhiteSpace(resposta.Titulo));
        // As 4 linhas da caixa: para que usamos; o que vai e o que não vai para a IA; plano gratuito; onde fica e
        // como apagar ou baixar.
        var linhas = resposta.Texto.Split('\n');
        Assert.Equal(4, linhas.Length);
        Assert.StartsWith("O Ronu usa seu peso, altura, idade, sexo, objetivo e treinos", linhas[0]);
        Assert.Contains("Seu nome, e-mail, peso, altura, idade, sexo e o nome das modalidades que você treina não vão para a IA.", linhas[1]);
        Assert.Contains("O Ronu não envia nada que identifique você.", linhas[2]);
        Assert.DoesNotContain("sem ligá-lo", resposta.Texto);
        Assert.StartsWith("Seus dados ficam guardados no Chile", linhas[3]);
        Assert.Contains("Antes de apagar, você pode baixar seus dados no Perfil.", resposta.Texto);
        Assert.Null(resposta.Aceito);
        Assert.True(resposta.PrecisaAceitar);
    }

    // ---------- GET /api/consentimentos/vigente/{tipo}, sem login ----------

    [Fact]
    public void Texto_Publico_Traz_O_Mesmo_Texto_Da_Consulta_Logada_Sem_Login()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var anonimo = new ConsentimentosController(banco)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var resposta = Assert.IsType<ConsentimentoTextoResponse>(Assert.IsType<OkObjectResult>(
            anonimo.TextoVigente(TextosConsentimento.TipoGeral)).Value);

        var vigente = TextosConsentimento.TextoVigente(TextosConsentimento.TipoGeral);
        Assert.Equal(Vigente, resposta.Versao);
        Assert.Equal(vigente.Titulo, resposta.Titulo);
        Assert.Equal(vigente.Corpo, resposta.Texto);
    }

    [Theory]
    [InlineData("alergias")]
    [InlineData("")]
    public void Texto_Publico_De_Tipo_Desconhecido_Da_400(string tipo)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        Assert.IsType<BadRequestObjectResult>(new ConsentimentosController(banco).TextoVigente(tipo));
    }

    // A rota pública não pode herdar o [Authorize] da classe: o cadastro a chama antes de a conta existir.
    [Fact]
    public void Texto_Publico_E_A_Unica_Rota_Anonima_Do_Consentimento_E_Tem_Limite()
    {
        var anonimas = typeof(ConsentimentosController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .ToList();

        var metodo = Assert.Single(anonimas);
        Assert.Equal(nameof(ConsentimentosController.TextoVigente), metodo.Name);
        Assert.Equal(LimitesConsentimento.PoliticaTextoPublico,
            metodo.GetCustomAttribute<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public async Task Aceite_Grava_Tipo_Versao_E_Data_Em_UTC()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "aceita@exemplo.test");
        var antes = DateTime.UtcNow;

        var resultado = await Controller(banco, usuario.Id).Aceitar(new ConsentimentoRequest { Tipo = "geral", Versao = Vigente });

        Assert.IsType<NoContentResult>(resultado);
        var gravado = await banco.Consentimentos.SingleAsync();
        Assert.Equal(usuario.Id, gravado.UsuarioId);
        Assert.Equal("geral", gravado.Tipo);
        Assert.Equal(Vigente, gravado.Versao);
        Assert.Equal(DateTimeKind.Utc, gravado.AceitoEm.Kind);
        Assert.InRange(gravado.AceitoEm, antes, DateTime.UtcNow);
        Assert.Null(gravado.RevogadoEm);

        var consulta = Assert.IsType<ConsentimentoResponse>(Assert.IsType<OkObjectResult>(
            await Controller(banco, usuario.Id).Consultar("geral")).Value);
        Assert.False(consulta.PrecisaAceitar);
        Assert.Equal(Vigente, consulta.Aceito!.Versao);
    }

    [Fact]
    public async Task Aceitar_Duas_Vezes_Nao_Duplica()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "duas-vezes@exemplo.test");
        var controller = Controller(banco, usuario.Id);

        await controller.Aceitar(new ConsentimentoRequest { Tipo = "geral", Versao = Vigente });
        var segunda = await controller.Aceitar(new ConsentimentoRequest { Tipo = "geral", Versao = Vigente });

        Assert.IsType<NoContentResult>(segunda);
        Assert.Equal(1, await banco.Consentimentos.CountAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Versao_Diferente_Da_Vigente_Da_409_E_Nao_Grava(int deslocamento)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "versao@exemplo.test");

        var resultado = await Controller(banco, usuario.Id).Aceitar(new ConsentimentoRequest { Tipo = "geral", Versao = Vigente + deslocamento });

        Assert.IsType<ConflictObjectResult>(resultado);
        Assert.Equal(0, await banco.Consentimentos.CountAsync());
    }

    [Theory]
    [InlineData("alergias")]
    [InlineData("Geral")]
    [InlineData("")]
    public async Task Tipo_Desconhecido_Da_400(string tipo)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "tipo@exemplo.test");
        var controller = Controller(banco, usuario.Id);

        Assert.IsType<BadRequestObjectResult>(await controller.Aceitar(new ConsentimentoRequest { Tipo = tipo, Versao = Vigente }));
        Assert.IsType<BadRequestObjectResult>(await controller.Consultar(tipo));
        Assert.Equal(0, await banco.Consentimentos.CountAsync());
    }

    [Fact]
    public async Task Aceite_E_Sempre_Do_Usuario_Do_Token()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var a = await NovoUsuario(banco, "a@exemplo.test");
        var b = await NovoUsuario(banco, "b@exemplo.test");

        await Controller(banco, a.Id).Aceitar(new ConsentimentoRequest { Tipo = "geral", Versao = Vigente });

        Assert.Equal(a.Id, (await banco.Consentimentos.SingleAsync()).UsuarioId);
        var deB = Assert.IsType<ConsentimentoResponse>(Assert.IsType<OkObjectResult>(await Controller(banco, b.Id).Consultar("geral")).Value);
        Assert.True(deB.PrecisaAceitar);
        Assert.Null(deB.Aceito);
    }

    // ---------- Cadastro e login ----------

    private static CadastroRequest Cadastro(string email, int? versao) => new()
    {
        Nome = "Fulano Fictício", Email = email, Senha = "senha-de-teste", Estado = "SP",
        ConsentimentoGeral = versao is null ? null : new ConsentimentoCadastroRequest { Versao = versao }
    };

    [Fact]
    public async Task Cadastro_Sem_Aceite_Da_400_E_Nao_Cria_A_Conta()
    {
        using var banco = BancoEmMemoria.NovoContexto();

        var resultado = await new AuthController(banco, Config(null)).Cadastro(Cadastro("novo@exemplo.test", versao: null));

        var erro = Assert.IsType<BadRequestObjectResult>(resultado);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(erro.Value));
        Assert.Equal(RegrasConsentimento.MensagemCadastroSemAceite, json.RootElement.GetProperty("mensagem").GetString());
        Assert.Equal(0, await banco.Usuarios.CountAsync());
        Assert.Equal(0, await banco.Consentimentos.CountAsync());
    }

    [Fact]
    public async Task Cadastro_Com_Aceite_Grava_A_Conta_O_Aceite_E_A_Data_De_Criacao()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var antes = DateTime.UtcNow;

        Assert.IsType<OkObjectResult>(await new AuthController(banco, Config(null)).Cadastro(Cadastro("novo@exemplo.test", Vigente)));

        var usuario = await banco.Usuarios.SingleAsync();
        var aceite = await banco.Consentimentos.SingleAsync();
        Assert.Equal(usuario.Id, aceite.UsuarioId);
        Assert.Equal(("geral", Vigente), (aceite.Tipo, aceite.Versao));
        Assert.InRange(usuario.CriadoEm!.Value, antes, DateTime.UtcNow);
        Assert.Equal(usuario.CriadoEm, aceite.AceitoEm);
    }

    [Fact]
    public async Task Cadastro_Com_Versao_Velha_Da_409_E_Nao_Cria_A_Conta()
    {
        using var banco = BancoEmMemoria.NovoContexto();

        var resultado = await new AuthController(banco, Config(null)).Cadastro(Cadastro("novo@exemplo.test", Vigente - 1));

        Assert.IsType<ConflictObjectResult>(resultado);
        Assert.Equal(0, await banco.Usuarios.CountAsync());
    }

    // Só durante a publicação: o frontend antigo ainda cadastra sem mandar o aceite.
    [Fact]
    public async Task Cadastro_Sem_Aceite_Com_Interruptor_Desligado_Cria_A_Conta_Sem_Aceite()
    {
        using var banco = BancoEmMemoria.NovoContexto();

        Assert.IsType<OkObjectResult>(await new AuthController(banco, Config(false)).Cadastro(Cadastro("novo@exemplo.test", versao: null)));

        Assert.Equal(1, await banco.Usuarios.CountAsync());
        Assert.Equal(0, await banco.Consentimentos.CountAsync());
    }

    [Fact]
    public async Task Login_De_Conta_Antiga_Pede_Consentimento_E_Grava_O_Ultimo_Login()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "antiga@exemplo.test", senha: "senha-de-teste");
        var antes = DateTime.UtcNow;

        var login = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(
            await new AuthController(banco, Config(null)).Login(new LoginRequest { Email = "antiga@exemplo.test", Senha = "senha-de-teste" })).Value);

        Assert.True(login.PrecisaConsentimento);
        var gravado = await banco.Usuarios.SingleAsync(u => u.Id == usuario.Id);
        Assert.InRange(gravado.UltimoLoginEm!.Value, antes, DateTime.UtcNow);
        Assert.Null(gravado.CriadoEm);
    }

    [Fact]
    public async Task Login_Depois_Do_Aceite_Nao_Pede_Consentimento()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(banco, "aceitou@exemplo.test", senha: "senha-de-teste");
        await Aceitar(banco, usuario.Id, Vigente);

        var login = Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(
            await new AuthController(banco, Config(null)).Login(new LoginRequest { Email = "aceitou@exemplo.test", Senha = "senha-de-teste" })).Value);

        Assert.False(login.PrecisaConsentimento);
    }

    [Fact]
    public async Task Login_Errado_Nao_Grava_O_Ultimo_Login()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        await NovoUsuario(banco, "antiga@exemplo.test", senha: "senha-de-teste");

        await new AuthController(banco, Config(null)).Login(new LoginRequest { Email = "antiga@exemplo.test", Senha = "senha-errada" });

        Assert.Null((await banco.Usuarios.SingleAsync()).UltimoLoginEm);
    }
}
