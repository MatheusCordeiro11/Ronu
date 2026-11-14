using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
/// TACO 2c, etapa 2: os chips de alergia (rotas, consentimento do tipo "alergias", revogação ao tirar o último), o
/// filtro num ponto só (ListaDaPessoa), a sugestão de chip na conferência das preferências e a minimização (as
/// alergias nunca vão para o prompt nem para o log). Só contas fictícias, com e-mail @exemplo.test.
/// </summary>
public class AlergiasUsuarioTests
{
    private static readonly int Vigente = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoAlergias);

    // ---------- Apoio ----------

    private static Usuario NovoUsuario(ApplicationDbContext banco, string email, params (string Tipo, string Alimento)[] prefs)
    {
        var usuario = new Usuario
        {
            Nome = "Pessoa Fictícia", Email = email, Estado = "SP", Altura = 175, Sexo = "Masculino",
            DataNascimento = new DateOnly(1995, 5, 10)
        };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        foreach (var (tipo, alimento) in prefs)
        {
            banco.PreferenciasAlimentares.Add(new PreferenciaAlimentar { UsuarioId = usuario.Id, Tipo = tipo, Alimento = alimento });
        }

        banco.SaveChanges();
        return usuario;
    }

    // Grava os chips direto no banco, com o aceite vigente, como o PUT deixaria.
    private static void Marcar(ApplicationDbContext banco, int usuarioId, params string[] codigos)
    {
        banco.Consentimentos.Add(new Consentimento { UsuarioId = usuarioId, Tipo = "alergias", Versao = Vigente, AceitoEm = DateTime.UtcNow });
        foreach (var codigo in codigos)
        {
            banco.AlergiasUsuario.Add(new AlergiaUsuario { UsuarioId = usuarioId, Codigo = codigo, MarcadoEm = DateTime.UtcNow });
        }

        banco.SaveChanges();
    }

    private static ClaimsPrincipal Logado(int usuarioId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"));

    private static PerfilController Perfil(ApplicationDbContext banco, int usuarioId) => new(banco)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Logado(usuarioId) } }
    };

    private static PreferenciasAlimentaresController Preferencias(ApplicationDbContext banco, int usuarioId) => new(banco)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Logado(usuarioId) } }
    };

    private static AlergiasResponse Estado(IActionResult resultado) => Assert.IsType<AlergiasResponse>(Assert.IsType<OkObjectResult>(resultado).Value);

    private static JsonElement Corpo(IActionResult resultado) =>
        JsonDocument.Parse(JsonSerializer.Serialize(((ObjectResult)resultado).Value)).RootElement;

    private static AlergiaRequest ComVersao(int versao) => new() { VersaoConsentimento = versao };

    // ---------- Texto do consentimento ----------

    [Fact]
    public void Texto_Vigente_Das_Alergias_Sai_Pela_Rota_Publica_Com_Os_Dois_Paragrafos()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var anonimo = new ConsentimentosController(banco)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var resposta = Assert.IsType<ConsentimentoTextoResponse>(Assert.IsType<OkObjectResult>(anonimo.TextoVigente("alergias")).Value);

        Assert.Equal(1, resposta.Versao);
        Assert.Equal("Autorizo o Ronu a usar minhas alergias para montar o cardápio.", resposta.Titulo);
        Assert.Equal(
        [
            "As alergias marcadas tiram do cardápio os alimentos que as contêm. Elas ficam guardadas na sua conta e não vão para a IA: ela recebe a lista de alimentos já sem esses itens.",
            "Desmarque a qualquer momento: sem nenhuma marcada, o Ronu apaga suas alergias."
        ], resposta.Texto.Split('\n'));
    }

    // ---------- PUT /api/perfil/alergias/{codigo} ----------

    [Fact]
    public async Task Sem_Aceite_E_Sem_Versao_Da_409_Com_O_Motivo_E_Nao_Grava()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "sem-aceite-alergia@exemplo.test");

        var resultado = await Perfil(banco, usuario.Id).MarcarAlergia("soja", null);

        Assert.IsType<ConflictObjectResult>(resultado);
        Assert.Equal("consentimento_alergias", Corpo(resultado).GetProperty("motivo").GetString());
        Assert.Equal(Vigente, Corpo(resultado).GetProperty("versaoVigente").GetInt32());
        Assert.Empty(banco.AlergiasUsuario);
        Assert.Empty(banco.Consentimentos);
    }

    [Fact]
    public async Task Com_A_Versao_Vigente_Grava_O_Aceite_E_O_Chip_Juntos()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "aceita-alergia@exemplo.test");
        var antes = DateTime.UtcNow;

        var estado = Estado(await Perfil(banco, usuario.Id).MarcarAlergia("soja", ComVersao(Vigente)));

        Assert.Equal(["soja"], estado.Alergias);
        Assert.True(estado.AceiteVigente);
        var aceite = await banco.Consentimentos.SingleAsync();
        Assert.Equal((usuario.Id, "alergias", Vigente), (aceite.UsuarioId, aceite.Tipo, aceite.Versao));
        Assert.InRange(aceite.AceitoEm, antes, DateTime.UtcNow);
        Assert.Null(aceite.RevogadoEm);
        var chip = await banco.AlergiasUsuario.SingleAsync();
        Assert.Equal((usuario.Id, "soja"), (chip.UsuarioId, chip.Codigo));
        Assert.Equal(DateTimeKind.Utc, chip.MarcadoEm.Kind);
    }

    [Fact]
    public async Task Com_Aceite_Vigente_Grava_Sem_Mandar_A_Versao()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "ja-aceitou@exemplo.test");
        Marcar(banco, usuario.Id, "leite");

        var estado = Estado(await Perfil(banco, usuario.Id).MarcarAlergia("ovo", null));

        // Na ordem dos chips da tela, não na ordem em que foram marcados.
        Assert.Equal(["leite", "ovo"], estado.Alergias);
        Assert.Single(banco.Consentimentos);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Versao_Diferente_Da_Vigente_Da_409_E_Nao_Grava(int deslocamento)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "versao-alergia@exemplo.test");

        var resultado = await Perfil(banco, usuario.Id).MarcarAlergia("soja", ComVersao(Vigente + deslocamento));

        Assert.IsType<ConflictObjectResult>(resultado);
        Assert.Equal(RegrasConsentimento.MensagemVersaoMudou, Corpo(resultado).GetProperty("mensagem").GetString());
        Assert.Empty(banco.AlergiasUsuario);
        Assert.Empty(banco.Consentimentos);
    }

    [Theory]
    [InlineData("Soja")]
    [InlineData(" soja")]
    [InlineData("gergelim")]
    [InlineData("latex")]
    [InlineData("lactose")]
    [InlineData("")]
    public async Task Codigo_Fora_Dos_8_Da_400_No_Put_E_No_Delete(string codigo)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "codigo@exemplo.test");
        var perfil = Perfil(banco, usuario.Id);

        Assert.IsType<BadRequestObjectResult>(await perfil.MarcarAlergia(codigo, ComVersao(Vigente)));
        Assert.IsType<BadRequestObjectResult>(await perfil.DesmarcarAlergia(codigo));
        Assert.Empty(banco.AlergiasUsuario);
        Assert.Empty(banco.Consentimentos);
    }

    [Fact]
    public void Os_8_Codigos_E_As_Marcas()
    {
        Assert.Equal(["leite", "gluten", "ovo", "amendoim", "castanhas", "soja", "peixe", "crustaceos"], CodigosAlergia.Chips.Select(c => c.Codigo));
        Assert.Equal(
            [Alergenos.Lactose, Alergenos.Gluten, Alergenos.Ovo, Alergenos.Amendoim, Alergenos.Castanhas, Alergenos.Soja, Alergenos.Peixe, Alergenos.Crustaceo],
            CodigosAlergia.Chips.Select(c => c.Marca));
    }

    [Fact]
    public async Task Marcar_Duas_Vezes_Nao_Duplica()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "duas-vezes-alergia@exemplo.test");
        var perfil = Perfil(banco, usuario.Id);

        await perfil.MarcarAlergia("peixe", ComVersao(Vigente));
        var estado = Estado(await perfil.MarcarAlergia("peixe", ComVersao(Vigente)));

        Assert.Equal(["peixe"], estado.Alergias);
        Assert.Single(banco.AlergiasUsuario);
        Assert.Single(banco.Consentimentos);
    }

    // ---------- DELETE /api/perfil/alergias/{codigo} ----------

    [Fact]
    public async Task Tirar_Um_Chip_Com_Outro_Marcado_Mantem_O_Aceite()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "tira-um@exemplo.test");
        Marcar(banco, usuario.Id, "soja", "leite");

        var estado = Estado(await Perfil(banco, usuario.Id).DesmarcarAlergia("soja"));

        Assert.Equal(["leite"], estado.Alergias);
        Assert.True(estado.AceiteVigente);
        Assert.Null((await banco.Consentimentos.SingleAsync()).RevogadoEm);
    }

    // Decisão aprovada: tirar o último apaga as alergias e marca RevogadoEm, mantendo a linha do aceite; marcar de novo
    // pede a caixa outra vez.
    [Fact]
    public async Task Tirar_O_Ultimo_Apaga_E_Revoga_O_Aceite_Mantendo_A_Linha()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "tira-ultimo@exemplo.test");
        Marcar(banco, usuario.Id, "soja");
        var perfil = Perfil(banco, usuario.Id);

        var estado = Estado(await perfil.DesmarcarAlergia("soja"));

        Assert.Empty(estado.Alergias);
        Assert.False(estado.AceiteVigente);
        Assert.Null(estado.AceitoEm);
        Assert.Empty(banco.AlergiasUsuario);
        var aceite = await banco.Consentimentos.AsNoTracking().SingleAsync();
        Assert.NotNull(aceite.RevogadoEm);

        var denovo = await perfil.MarcarAlergia("soja", null);
        Assert.IsType<ConflictObjectResult>(denovo);
        Assert.Equal("consentimento_alergias", Corpo(denovo).GetProperty("motivo").GetString());
        Assert.Empty(banco.AlergiasUsuario);

        // Com a caixa marcada de novo, um aceite novo (o revogado fica no histórico).
        Estado(await perfil.MarcarAlergia("soja", ComVersao(Vigente)));
        Assert.Equal(2, await banco.Consentimentos.CountAsync());
        Assert.Single(banco.AlergiasUsuario);
    }

    [Fact]
    public async Task Desmarcar_O_Que_Nao_Esta_Marcado_Responde_Igual()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "nada-marcado@exemplo.test");

        var estado = Estado(await Perfil(banco, usuario.Id).DesmarcarAlergia("ovo"));

        Assert.Empty(estado.Alergias);
    }

    [Fact]
    public async Task Cada_Um_So_Le_E_So_Apaga_As_Proprias_Alergias()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = NovoUsuario(banco, "ana-alergia@exemplo.test");
        var bia = NovoUsuario(banco, "bia-alergia@exemplo.test");
        Marcar(banco, ana.Id, "amendoim");

        Assert.Empty(Estado(await Perfil(banco, bia.Id).ObterAlergias()).Alergias);
        Assert.False(Estado(await Perfil(banco, bia.Id).ObterAlergias()).AceiteVigente);
        await Perfil(banco, bia.Id).DesmarcarAlergia("amendoim");

        Assert.Equal(["amendoim"], Estado(await Perfil(banco, ana.Id).ObterAlergias()).Alergias);
        Assert.Null((await banco.Consentimentos.SingleAsync()).RevogadoEm);
    }

    // Como no DELETE /api/perfil/rotina: apagar o próprio dado não depende do consentimento geral. Marcar e listar
    // dependem (não têm [DispensaConsentimento]).
    [Fact]
    public async Task Desmarcar_Funciona_Sem_O_Consentimento_Geral_E_Marcar_Nao()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "sem-geral@exemplo.test");
        Marcar(banco, usuario.Id, "ovo");

        async Task<IActionResult?> Filtrar(string metodo)
        {
            var metadados = typeof(PerfilController).GetCustomAttributes(true)
                .Concat(typeof(PerfilController).GetMethod(metodo)!.GetCustomAttributes(true))
                .ToList();
            var http = new DefaultHttpContext { User = Logado(usuario.Id) };
            var contexto = new AuthorizationFilterContext(
                new ActionContext(http, new RouteData(), new ActionDescriptor { EndpointMetadata = metadados }), new List<IFilterMetadata>());
            await new FiltroConsentimento(banco, BancoEmMemoria.Configuracao()).OnAuthorizationAsync(contexto);
            return contexto.Result;
        }

        Assert.Null(await Filtrar(nameof(PerfilController.DesmarcarAlergia)));
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Filtrar(nameof(PerfilController.MarcarAlergia))).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await Filtrar(nameof(PerfilController.ObterAlergias))).StatusCode);

        Assert.Empty(Estado(await Perfil(banco, usuario.Id).DesmarcarAlergia("ovo")).Alergias);
    }

    [Fact]
    public void Rotas_Das_Alergias_Sao_Proprias_E_Exigem_Login()
    {
        Assert.NotNull(typeof(PerfilController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("alergias", typeof(PerfilController).GetMethod(nameof(PerfilController.ObterAlergias))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("alergias/{codigo}", typeof(PerfilController).GetMethod(nameof(PerfilController.MarcarAlergia))!.GetCustomAttribute<HttpPutAttribute>()!.Template);
        Assert.Equal("alergias/{codigo}", typeof(PerfilController).GetMethod(nameof(PerfilController.DesmarcarAlergia))!.GetCustomAttribute<HttpDeleteAttribute>()!.Template);
        // O PUT do perfil inteiro não conhece alergias (duas abas).
        Assert.DoesNotContain(typeof(PerfilRequest).GetProperties(), p => p.Name.Contains("Alerg", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PerfilResponse).GetProperties(), p => p.Name.Contains("Alerg", StringComparison.OrdinalIgnoreCase));
    }

    // ---------- Filtro num ponto só ----------

    private static string RaizDoRepositorio()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "src", "Ronu.Api")))
        {
            pasta = pasta.Parent;
        }

        return pasta?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }

    // Toda lista da pessoa (geração, conferência, troca) sai do ListaDaPessoa, que lê as alergias. Um Montar chamado
    // de outro lugar seria uma lista que pode esquecer as alergias.
    [Fact]
    public void Montar_So_E_Chamado_Pelo_Ponto_Unico()
    {
        var api = Path.Combine(RaizDoRepositorio(), "src", "Ronu.Api");
        var chamada = new Regex(@"ListaAlimentosPessoa\s*\.\s*Montar\s*\(");

        var arquivos = Directory.EnumerateFiles(api, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();
        var chamam = arquivos.Where(f => chamada.IsMatch(File.ReadAllText(f))).Select(Path.GetFileName).ToList();

        Assert.True(arquivos.Count > 50, "a varredura não achou o código da API");
        Assert.Equal(["ListaDaPessoa.cs"], chamam);
    }

    [Fact]
    public void Montar_Exige_As_Alergias_Sem_Valor_Padrao()
    {
        var parametro = typeof(ListaAlimentosPessoa).GetMethod(nameof(ListaAlimentosPessoa.Montar))!.GetParameters().Single(p => p.Name == "alergias");

        Assert.False(parametro.HasDefaultValue);
    }

    [Theory]
    [InlineData("soja", Alergenos.Soja)]
    [InlineData("leite", Alergenos.Lactose)]
    [InlineData("gluten", Alergenos.Gluten)]
    [InlineData("ovo", Alergenos.Ovo)]
    [InlineData("peixe", Alergenos.Peixe)]
    public async Task Lista_Da_Pessoa_Tira_O_Que_Cada_Chip_Marca(string codigo, Alergenos marca)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, $"chip-{codigo}@exemplo.test");
        var semChip = (await ListaDaPessoa.MontarAsync(banco, usuario.Id)).Lista;
        Marcar(banco, usuario.Id, codigo);

        var daPessoa = await ListaDaPessoa.MontarAsync(banco, usuario.Id);

        Assert.Equal([codigo], daPessoa.Alergias);
        Assert.Contains(semChip.Itens, i => i.Item.Alergenos.HasFlag(marca));
        Assert.DoesNotContain(daPessoa.Lista.Itens, i => i.Item.Alergenos.HasFlag(marca));
        Assert.Equal(marca, daPessoa.Lista.Alergias);
    }

    // Regra cruzada: cada um dos dois chips tira as duas famílias.
    [Theory]
    [InlineData("amendoim")]
    [InlineData("castanhas")]
    public async Task Amendoim_E_Castanhas_Tiram_As_Duas_Familias(string codigo)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, $"cruzado-{codigo}@exemplo.test");
        Marcar(banco, usuario.Id, codigo);

        var ids = (await ListaDaPessoa.MontarAsync(banco, usuario.Id)).Lista.Itens.Select(i => i.Item.Id).ToList();

        Assert.DoesNotContain("pasta-de-amendoim", ids);
        Assert.DoesNotContain(ids, id => BaseNutricional.Itens.Single(i => i.Id == id).Alergenos.HasFlag(Alergenos.Castanhas));
        Assert.DoesNotContain(ids, id => BaseNutricional.Itens.Single(i => i.Id == id).Alergenos.HasFlag(Alergenos.Amendoim));
    }

    // A lista da geração sai do mesmo ponto: o contexto do Gemini já chega sem os alérgenos.
    [Fact]
    public async Task Contexto_Da_Geracao_Leva_A_Lista_Sem_Os_Alergenos()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "contexto-alergia@exemplo.test", ("nao_como", "fígado"));
        banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 80, Objetivo = "manter peso", DataRegistro = DateTime.UtcNow });
        banco.SaveChanges();
        Marcar(banco, usuario.Id, "soja", "gluten");

        var contexto = await new ContextoDietaBuilder(banco, new CalculadoraPesoTendencia()).ConstruirAsync(usuario.Id);

        Assert.NotNull(contexto.Lista);
        Assert.DoesNotContain(contexto.Lista.Itens, i => (i.Item.Alergenos & (Alergenos.Soja | Alergenos.Gluten)) != Alergenos.Nenhum);
        Assert.Equal(["fígado"], contexto.Preferencias.Select(p => p.Alimento));
    }

    // Sem lista no contexto, o gerador não monta uma por conta própria (seria sem as alergias).
    [Fact]
    public async Task Gerador_Recusa_Contexto_Sem_Lista()
    {
        var contexto = new ContextoDietaDto
        {
            Altura = 175, Sexo = "Masculino", Idade = 30, Peso = 80, Objetivo = "manter peso",
            Modalidades = new(), Preferencias = new(), HistoricoPeso = new()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Gerador(new GeminiFalso(), new LoggerQueGuarda<GeradorDietaGemini>()).GerarDietaAsync(contexto));
    }

    // Nunca relaxa: com os 8 chips e uma lista pobre, os alérgenos continuam fora (vale o aviso de lista pobre).
    [Fact]
    public async Task Lista_Pobre_Nao_Devolve_Alergeno()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "pobre@exemplo.test", ("nao_como", "carne"));
        Marcar(banco, usuario.Id, CodigosAlergia.Chips.Select(c => c.Codigo).ToArray());

        var lista = (await ListaDaPessoa.MontarAsync(banco, usuario.Id)).Lista;

        Assert.False(lista.Minimos.Atende);
        Assert.DoesNotContain(lista.Itens, i => i.Item.Alergenos != Alergenos.Nenhum);
    }

    // ---------- Conferência das preferências ----------

    [Theory]
    [InlineData("camarão", "crustaceos")]
    [InlineData("frutos do mar", "crustaceos")]
    [InlineData("glúten", "gluten")]
    [InlineData("sou celíaco", "gluten")]
    [InlineData("ovo", "ovo")]
    [InlineData("ovos", "ovo")]
    [InlineData("amendoim", "amendoim")]
    [InlineData("amendoin", "amendoim")]
    [InlineData("castanha de caju", "castanhas")]
    [InlineData("nozes", "castanhas")]
    [InlineData("tenho alergia a soja", "soja")]
    [InlineData("peixe", "peixe")]
    [InlineData("leite", "leite")]
    [InlineData("alergia a leite", "leite")]
    public void Evitar_De_Alergeno_Sugere_O_Chip(string texto, string chip)
    {
        Assert.Equal([chip], CasamentoPreferencias.SugerirAlergias(texto));
    }

    // Lactose e intolerância não são alergia: Leite não é sugerido. O que não é linha de alérgeno também não.
    [Theory]
    [InlineData("lactose")]
    [InlineData("lactoze")]
    [InlineData("intolerância à lactose")]
    [InlineData("leite (sou intolerante)")]
    [InlineData("frango")]
    [InlineData("queijo")]
    [InlineData("vegano")]
    [InlineData("gergelim")]
    public void Lactose_Intolerancia_E_O_Resto_Nao_Sugerem(string texto)
    {
        Assert.Empty(CasamentoPreferencias.SugerirAlergias(texto));
    }

    [Fact]
    public void Texto_Com_Dois_Alergenos_Sugere_Os_Dois_Na_Ordem_Dos_Chips()
    {
        Assert.Equal(["ovo", "soja"], CasamentoPreferencias.SugerirAlergias("soja e ovo"));
    }

    [Fact]
    public async Task Conferencia_Sugere_So_No_Evitar_E_Nunca_Um_Chip_Ja_Marcado()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "sugere@exemplo.test",
            ("nao_como", "camarão"), ("nao_como", "soja"), ("preferido", "peixe"), ("nao_como", "intolerância à lactose"), ("nao_como", "jiló"),
            ("nao_como", "ovo e soja"), ("nao_como", "glúten e ovo"));
        Marcar(banco, usuario.Id, "soja");

        var conferencia = Assert.IsType<ConferenciaPreferenciasResponse>(
            Assert.IsType<OkObjectResult>(await Preferencias(banco, usuario.Id).Conferir()).Value);
        var porTexto = conferencia.Preferencias.ToDictionary(p => p.Alimento, p => p.SugereAlergias);

        Assert.Equal(["crustaceos"], porTexto["camarão"]);
        Assert.Empty(porTexto["soja"]);
        Assert.Empty(porTexto["peixe"]);
        Assert.Empty(porTexto["intolerância à lactose"]);
        Assert.Empty(porTexto["jiló"]);
        // Dois alérgenos no mesmo texto: sugere os dois, menos o já marcado.
        Assert.Equal(["ovo"], porTexto["ovo e soja"]);
        Assert.Equal(["gluten", "ovo"], porTexto["glúten e ovo"]);
    }

    // A conferência conta os chips: o Leite tira proteína dos mínimos.
    [Fact]
    public async Task Conferencia_Conta_Os_Chips_Nos_Minimos()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "minimos-chip@exemplo.test");
        async Task<int> Proteinas() => Assert.IsType<ConferenciaPreferenciasResponse>(
            Assert.IsType<OkObjectResult>(await Preferencias(banco, usuario.Id).Conferir()).Value).Lista.ItensPorPapel["Proteina"];

        var antes = await Proteinas();
        Marcar(banco, usuario.Id, "leite");

        Assert.True(await Proteinas() < antes);
    }

    // ---------- Minimização: nem prompt, nem log ----------

    private static GeradorDietaGemini Gerador(GeminiFalso gemini, ILogger<GeradorDietaGemini> logger) => new(
        new HttpClient(gemini), new CalculadoraGastoCalorico(),
        new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia()),
        new GeminiOptions { ApiKey = "teste" }, logger)
    {
        AjustarQuantidades = false,
        ExigirMinimoDeRefeicoes = false,
        ExigirFonteDeGordura = false
    };

    private static async Task<ContextoDietaDto> ContextoComChips(ApplicationDbContext banco, string email, params string[] codigos)
    {
        var usuario = NovoUsuario(banco, email);
        banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 80, Objetivo = "manter peso", DataRegistro = DateTime.UtcNow });
        banco.SaveChanges();
        if (codigos.Length > 0)
        {
            Marcar(banco, usuario.Id, codigos);
        }

        return await new ContextoDietaBuilder(banco, new CalculadoraPesoTendencia()).ConstruirAsync(usuario.Id);
    }

    // O texto inteiro que vai ao Gemini (prompt e schema), já sem o escape do JSON.
    private static string Enviado(GeminiFalso gemini)
    {
        using var corpo = JsonDocument.Parse(gemini.UltimoCorpo!);
        return corpo.RootElement.ToString() + "\n" + corpo.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
    }

    // As marcas que somem com os chips de soja, amendoim, castanhas e crustáceos (nomes que não aparecem nas regras do
    // prompt): sem chip, o prompt tem óleo de soja e pasta de amendoim; com eles, nenhuma palavra das alergias.
    [Fact]
    public async Task Prompt_Nao_Leva_Nenhum_Nome_De_Alergia()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var semChips = new GeminiFalso();
        await Gerador(semChips, new LoggerQueGuarda<GeradorDietaGemini>()).GerarDietaAsync(await ContextoComChips(banco, "sem-chips@exemplo.test"));
        var comChips = new GeminiFalso();
        await Gerador(comChips, new LoggerQueGuarda<GeradorDietaGemini>()).GerarDietaAsync(
            await ContextoComChips(banco, "com-chips@exemplo.test", "soja", "amendoim", "castanhas", "crustaceos"));

        Assert.Contains("soja", Enviado(semChips), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("amendoim", Enviado(semChips), StringComparison.OrdinalIgnoreCase);
        var texto = Enviado(comChips);
        foreach (var palavra in new[] { "soja", "amendoim", "castanha", "crustác", "crustac", "alergi", "alérg", "alerg" })
        {
            Assert.DoesNotContain(palavra, texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static readonly Regex PalavraDeAlergia = new(
        @"\b(leite|gluten|glúten|ovo|amendoim|castanhas?|soja|peixe|crustaceos|crustáceos|alergias?)\b", RegexOptions.IgnoreCase);

    // O log da geração não tem os códigos nem os nomes dos chips (nem a contagem de alergias da pessoa).
    [Fact]
    public async Task Log_Da_Geracao_Nao_Leva_As_Alergias()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var logger = new LoggerQueGuarda<GeradorDietaGemini>();

        await Gerador(new GeminiFalso(), logger).GerarDietaAsync(
            await ContextoComChips(banco, "log@exemplo.test", CodigosAlergia.Chips.Select(c => c.Codigo).ToArray()));

        Assert.NotEmpty(logger.Mensagens);
        Assert.All(logger.Mensagens, m => Assert.DoesNotMatch(PalavraDeAlergia, m));
    }

    // ---------- Apoio do Gemini e do log ----------

    // Responde sempre uma semana válida: arroz no almoço e no jantar (sem nenhum alérgeno).
    private sealed class GeminiFalso : HttpMessageHandler
    {
        public string? UltimoCorpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimoCorpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            object Refeicao(string nome) => new
            {
                nome,
                alimentos = new[] { new { nome = "Arroz branco cozido", quantidade = 100, unidade = "g" } },
                macros = new { calorias = 128, proteinasG = 3, carboidratosG = 28, gordurasG = 0 },
                horario = "12:00"
            };
            var dias = GeradorDietaGemini.NomesDias.Select(n => new
            {
                diaSemana = n,
                refeicoes = new[] { Refeicao("Almoço"), Refeicao("Jantar") },
                totalDoDia = new { calorias = 256, proteinasG = 6, carboidratosG = 56, gordurasG = 0 }
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

/// <summary>Guarda cada mensagem de log já formatada (com a exceção, se houver), para os testes de minimização.</summary>
internal sealed class LoggerQueGuarda<T> : ILogger<T>
{
    public List<string> Mensagens { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Mensagens.Add(formatter(state, exception) + (exception is null ? "" : " " + exception));
}
