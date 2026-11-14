using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;
using Ronu.Api.Services.PlanoFamilia;

namespace Ronu.Api.Tests;

/// <summary>
/// Plano família, complementos da etapa 7: as restrições da casa ("Nesta casa, não entra" e "Evitamos quando dá"), o
/// dia em que o convite vence na verificação e o aceite de uma versão nova do texto da casa por quem já está nela. Só
/// perfis fictícios, com e-mails @exemplo.test.
/// </summary>
public class CasaComplementosTests
{
    private static readonly int VersaoCasa = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoCasa);
    private static readonly StringComparer OrdemPtBr = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);

    // ---------- Apoio ----------

    private static async Task<Usuario> Pessoa(ApplicationDbContext banco, string email, string nome)
    {
        var usuario = new Usuario
        {
            Nome = nome, Email = email, Estado = "SP", Altura = 170m, Sexo = "Feminino",
            DataNascimento = new DateOnly(1990, 3, 14), CriadoEm = DateTime.UtcNow
        };
        banco.Usuarios.Add(usuario);
        await banco.SaveChangesAsync();
        banco.Consentimentos.Add(new Consentimento { UsuarioId = usuario.Id, Tipo = TextosConsentimento.TipoGeral, Versao = 1, AceitoEm = DateTime.UtcNow });
        banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 70m, Objetivo = "manter peso", DataRegistro = DateTime.UtcNow });
        await banco.SaveChangesAsync();
        return usuario;
    }

    private static ClaimsPrincipal Token(int usuarioId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"));

    private static CasaController Casa(ApplicationDbContext banco, int usuarioId) => new(banco, BancoEmMemoria.Configuracao())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Token(usuarioId) } }
    };

    private static ConsentimentosController Consentimentos(ApplicationDbContext banco, int usuarioId) => new(banco)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Token(usuarioId) } }
    };

    private static async Task<string> Convidar(ApplicationDbContext banco, Usuario dono)
    {
        var criado = Assert.IsType<ConviteCriadoResponse>(Assert.IsType<CreatedResult>(await Casa(banco, dono.Id).CriarConvite()).Value);
        return criado.Link.Split("#convite=")[1];
    }

    private static async Task MontarCasa(ApplicationDbContext banco, Usuario dono, params Usuario[] membros)
    {
        Assert.IsType<CreatedResult>(await Casa(banco, dono.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa }));
        foreach (var membro in membros)
        {
            var token = await Convidar(banco, dono);
            Assert.IsType<OkObjectResult>(await Casa(banco, membro.Id).AceitarConvite(new AceitarConviteRequest { Token = token, VersaoConsentimento = VersaoCasa }));
        }
    }

    private static void Preferencia(ApplicationDbContext banco, Usuario pessoa, string alimento, string tipo = TiposPreferencia.NaoComo)
    {
        banco.PreferenciasAlimentares.Add(new PreferenciaAlimentar { UsuarioId = pessoa.Id, Alimento = alimento, Tipo = tipo });
        banco.SaveChanges();
    }

    private static void Alergia(ApplicationDbContext banco, Usuario pessoa, string codigo)
    {
        banco.AlergiasUsuario.Add(new AlergiaUsuario { UsuarioId = pessoa.Id, Codigo = codigo, MarcadoEm = DateTime.UtcNow });
        banco.SaveChanges();
    }

    private static async Task<CasaResponse> Ver(ApplicationDbContext banco, Usuario pessoa) =>
        Assert.IsType<CasaResponse>(Assert.IsType<OkObjectResult>(await Casa(banco, pessoa.Id).Obter()).Value);

    private static int Status(IActionResult resultado) => resultado switch
    {
        ObjectResult objeto => objeto.StatusCode ?? 200,
        StatusCodeResult codigo => codigo.StatusCode,
        _ => throw new InvalidOperationException(resultado.GetType().Name)
    };

    private static string? Motivo(IActionResult resultado)
    {
        var valor = Assert.IsAssignableFrom<ObjectResult>(resultado).Value;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(valor));
        return json.RootElement.TryGetProperty("motivo", out var motivo) ? motivo.GetString() : null;
    }

    private static string Item(string id) => BaseNutricional.Itens.Single(i => i.Id == id).Nome;

    private static void EmOrdemAlfabetica(List<string> nomes) =>
        Assert.Equal(nomes.Order(OrdemPtBr).ToList(), nomes);

    // ---------- Restrições da casa ----------

    [Fact]
    public async Task Restricoes_Juntam_Nao_Como_E_Alergias_Sem_Autor_Sem_Texto_Livre_E_Iguais_Para_Todos()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana-restricoes@exemplo.test", "Ana Sobrenomeficticio");
        var bia = await Pessoa(banco, "bia-restricoes@exemplo.test", "Bia Sobrenomeficticio");
        var caio = await Pessoa(banco, "caio-restricoes@exemplo.test", "Caio Sobrenomeficticio");
        await MontarCasa(banco, ana, bia, caio);
        // A dona marca o chip; a Bia escreve a alergia no "não como", com um trecho que não casa com a base; o Caio só
        // evita (e evita também o peixe, que a Bia não come).
        Alergia(banco, ana, "amendoim");
        Preferencia(banco, bia, "tenho alergia a peixe");
        Preferencia(banco, bia, "comida da vovozinha xyzw");
        Preferencia(banco, caio, "carne vermelha", TiposPreferencia.Evitar);
        Preferencia(banco, caio, "peixe", TiposPreferencia.Evitar);

        var daDona = (await Ver(banco, ana)).Restricoes;

        // O peixe sai inteiro (o nome da categoria); o amendoim e as castanhas, item a item (a linhaça fica).
        Assert.Contains("Peixes", daDona.NaoEntra);
        Assert.Contains(Item("amendoim-torrado"), daDona.NaoEntra);
        Assert.Contains(Item("castanha-do-para"), daDona.NaoEntra);
        Assert.DoesNotContain(Item("linhaca"), daDona.NaoEntra);
        // "Carne vermelha" é a bovina e a suína; o peixe que o Caio evita já não entra, e não aparece de novo.
        Assert.Equal(new[] { "Carne bovina", "Carne suína" }, daDona.EvitamosQuandoDa);
        EmOrdemAlfabetica(daDona.NaoEntra);
        EmOrdemAlfabetica(daDona.EvitamosQuandoDa);

        // Nada do texto que alguém escreveu, nada do chip, nada de quem.
        var json = JsonSerializer.Serialize(daDona, JsonSerializerOptions.Web);
        foreach (var proibida in new[] { "alergia", "tenho", "vovozinha", "xyzw", "Leite e derivados", "Ana", "Bia", "Caio", "carne vermelha" })
        {
            Assert.DoesNotContain(proibida, json, StringComparison.Ordinal);
        }

        // Dono e membros veem exatamente o mesmo.
        foreach (var pessoa in new[] { bia, caio })
        {
            Assert.Equal(json, JsonSerializer.Serialize((await Ver(banco, pessoa)).Restricoes, JsonSerializerOptions.Web));
        }
    }

    // A tabela do casamento tira, para estes alimentos, os mesmos itens que o chip: a lista não diz se veio de alergia.
    [Theory]
    [InlineData("leite", "leite")]
    [InlineData("ovo", "ovo")]
    [InlineData("soja", "soja")]
    [InlineData("gluten", "glúten")]
    [InlineData("amendoim", "amendoim")]
    [InlineData("castanhas", "castanha")]
    public async Task Alergia_E_Nao_Como_Do_Mesmo_Alimento_Dao_A_Mesma_Restricao(string chip, string texto)
    {
        async Task<RestricoesCasaResponse> Restricoes(Action<ApplicationDbContext, Usuario> marcar)
        {
            using var banco = BancoEmMemoria.NovoContexto();
            var ana = await Pessoa(banco, "ana-igual@exemplo.test", "Ana Sobrenomeficticio");
            var bia = await Pessoa(banco, "bia-igual@exemplo.test", "Bia Sobrenomeficticio");
            await MontarCasa(banco, ana, bia);
            marcar(banco, bia);
            return (await Ver(banco, ana)).Restricoes;
        }

        var pelaAlergia = await Restricoes((banco, bia) => Alergia(banco, bia, chip));
        var peloTexto = await Restricoes((banco, bia) => Preferencia(banco, bia, texto));

        Assert.NotEmpty(pelaAlergia.NaoEntra);
        Assert.Equal(pelaAlergia.NaoEntra, peloTexto.NaoEntra);
    }

    [Fact]
    public async Task Ordem_Das_Restricoes_Nao_Depende_Da_Ordem_Dos_Membros()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana-ordem@exemplo.test", "Ana Sobrenomeficticio");
        var bia = await Pessoa(banco, "bia-ordem@exemplo.test", "Bia Sobrenomeficticio");
        // A dona não come algo que vem depois no alfabeto; a Bia, algo que vem antes.
        Preferencia(banco, ana, "tomate");
        Preferencia(banco, bia, "abacate");
        Preferencia(banco, bia, "banana", TiposPreferencia.Evitar);
        Preferencia(banco, ana, "maçã", TiposPreferencia.Evitar);

        var umaOrdem = await RestricoesDaCasa.MontarAsync(banco, [ana.Id, bia.Id], ana.Id);
        var outraOrdem = await RestricoesDaCasa.MontarAsync(banco, [bia.Id, ana.Id], ana.Id);

        Assert.Equal(JsonSerializer.Serialize(umaOrdem), JsonSerializer.Serialize(outraOrdem));
        EmOrdemAlfabetica(umaOrdem.NaoEntra);
        EmOrdemAlfabetica(umaOrdem.EvitamosQuandoDa);
        Assert.StartsWith("Abacate", umaOrdem.NaoEntra[0]);
    }

    [Fact]
    public async Task Casa_Sem_Restricao_Manda_As_Duas_Listas_Vazias()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana-vazia@exemplo.test", "Ana Sobrenomeficticio");
        await MontarCasa(banco, ana);
        // Quem gosta de algo não restringe nada; café e bebida alcoólica nunca entram na casa, então "não como" deles
        // também não vira restrição.
        Preferencia(banco, ana, "frango", TiposPreferencia.Preferido);
        Preferencia(banco, ana, "café");
        Preferencia(banco, ana, "bebida alcoólica");

        var restricoes = (await Ver(banco, ana)).Restricoes;

        Assert.Empty(restricoes.NaoEntra);
        Assert.Empty(restricoes.EvitamosQuandoDa);
    }

    // ---------- Validade do convite ----------

    [Fact]
    public async Task Verificar_Devolve_So_O_Dia_Em_Que_O_Convite_Vence()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana-validade@exemplo.test", "Ana Sobrenomeficticio");
        var bia = await Pessoa(banco, "bia-validade@exemplo.test", "Bia Sobrenomeficticio");
        await MontarCasa(banco, ana);
        var token = await Convidar(banco, ana);
        var convite = await banco.ConvitesCasa.SingleAsync();

        var resultado = await Casa(banco, bia.Id).VerificarConvite(new VerificarConviteRequest { Token = token });

        var verificado = Assert.IsType<ConviteVerificadoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(FusoHorarioEstado.Hoje("SP", convite.ExpiraEm), verificado.ExpiraEm);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(verificado, JsonSerializerOptions.Web));
        // Só estes campos, e a data sem hora (a hora diria o minuto em que o link foi gerado).
        Assert.Equal(new[] { "consentimento", "expiraEm", "membros", "primeiroNomeDono" },
            json.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2}$"), json.RootElement.GetProperty("expiraEm").GetString());
    }

    // ---------- Versão nova do texto da casa ----------

    // Uma versão nova do texto invalida o aceite anterior. Sem uma v2 escrita, o teste deixa o aceite da Bia numa versão
    // anterior à vigente, que é o que ela teria quando o texto mudasse.
    private static async Task VersaoAntiga(ApplicationDbContext banco, Usuario pessoa)
    {
        foreach (var aceite in banco.Consentimentos.Where(c => c.UsuarioId == pessoa.Id && c.Tipo == TextosConsentimento.TipoCasa))
        {
            aceite.Versao = VersaoCasa - 1;
        }
        await banco.SaveChangesAsync();
    }

    [Fact]
    public async Task Membro_Com_Aceite_Antigo_Aceita_A_Versao_Nova_E_A_Casa_Volta_A_Abrir()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana-versao@exemplo.test", "Ana Sobrenomeficticio");
        var bia = await Pessoa(banco, "bia-versao@exemplo.test", "Bia Sobrenomeficticio");
        await MontarCasa(banco, ana, bia);
        await VersaoAntiga(banco, bia);

        var fechada = await Casa(banco, bia.Id).Obter();
        Assert.Equal((409, RegrasConsentimento.MotivoSemAceiteCasa), (Status(fechada), Motivo(fechada)));
        var consulta = Assert.IsType<ConsentimentoResponse>(Assert.IsType<OkObjectResult>(await Consentimentos(banco, bia.Id).Consultar("casa")).Value);
        Assert.True(consulta.PrecisaAceitar);

        var aceite = await Consentimentos(banco, bia.Id).Aceitar(new ConsentimentoRequest { Tipo = "casa", Versao = VersaoCasa });

        Assert.IsType<NoContentResult>(aceite);
        Assert.IsType<OkObjectResult>(await Casa(banco, bia.Id).Obter());
        // O aceite antigo fica para o histórico; o novo vale.
        Assert.Equal(new[] { VersaoCasa - 1, VersaoCasa },
            banco.Consentimentos.Where(c => c.UsuarioId == bia.Id && c.Tipo == "casa").Select(c => c.Versao).Order().ToArray());

        // Aceitar de novo não duplica; sair revoga os dois.
        Assert.IsType<NoContentResult>(await Consentimentos(banco, bia.Id).Aceitar(new ConsentimentoRequest { Tipo = "casa", Versao = VersaoCasa }));
        Assert.Equal(2, banco.Consentimentos.Count(c => c.UsuarioId == bia.Id && c.Tipo == "casa"));
        Assert.IsType<NoContentResult>(await Casa(banco, bia.Id).Sair());
        Assert.All(banco.Consentimentos.Where(c => c.UsuarioId == bia.Id && c.Tipo == "casa"), c => Assert.NotNull(c.RevogadoEm));
    }

    [Fact]
    public async Task Sem_Casa_O_Aceite_Da_Casa_Pela_Rota_De_Consentimentos_Continua_Recusado()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var carla = await Pessoa(banco, "carla-versao@exemplo.test", "Carla Sobrenomeficticio");

        var resultado = await Consentimentos(banco, carla.Id).Aceitar(new ConsentimentoRequest { Tipo = "casa", Versao = VersaoCasa });

        Assert.Equal(400, Status(resultado));
        Assert.False(await banco.Consentimentos.AnyAsync(c => c.UsuarioId == carla.Id && c.Tipo == "casa"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Versao_Diferente_Da_Vigente_Da_409_Com_O_Motivo_Da_Casa(int deslocamento)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana-errada@exemplo.test", "Ana Sobrenomeficticio");
        var bia = await Pessoa(banco, "bia-errada@exemplo.test", "Bia Sobrenomeficticio");
        await MontarCasa(banco, ana, bia);
        await VersaoAntiga(banco, bia);

        var resultado = await Consentimentos(banco, bia.Id).Aceitar(new ConsentimentoRequest { Tipo = "casa", Versao = VersaoCasa + deslocamento });

        Assert.Equal((409, RegrasConsentimento.MotivoSemAceiteCasa), (Status(resultado), Motivo(resultado)));
        Assert.False(await RegrasConsentimento.TemAceiteVigenteAsync(banco, bia.Id, TextosConsentimento.TipoCasa));
    }
}
