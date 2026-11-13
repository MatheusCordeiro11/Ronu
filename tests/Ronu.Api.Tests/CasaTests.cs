using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services.Consentimentos;
using Ronu.Api.Services.Conta;
using Ronu.Api.Services.PlanoFamilia;

namespace Ronu.Api.Tests;

/// <summary>
/// Plano família, etapa 1: criar a casa, o convite (verificar e aceitar, na ordem das checagens), as permissões de dono
/// e de membro, sair, remover, transferir, a exclusão de conta com casa e o que a resposta mostra dos outros. Só perfis
/// fictícios, com e-mails @exemplo.test.
/// </summary>
public class CasaTests
{
    private static readonly int VersaoCasa = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoCasa);

    // ---------- Apoio ----------

    // Uma pessoa com o cadastro completo (o que o convite exige) e o aceite geral. O sobrenome e os dados corporais são
    // únicos o bastante para o teste de vazamento procurá-los na resposta.
    private static async Task<Usuario> Pessoa(
        ApplicationDbContext banco, string email, string nome = "Pessoa Sobrenomeficticio",
        DateOnly? nascimento = null, decimal? altura = 177.3m, string? sexo = "Feminino", bool comObjetivo = true)
    {
        var usuario = new Usuario
        {
            Nome = nome, Email = email, Estado = "SP", Altura = altura, Sexo = sexo,
            DataNascimento = nascimento ?? new DateOnly(1990, 3, 14), CriadoEm = DateTime.UtcNow
        };
        banco.Usuarios.Add(usuario);
        await banco.SaveChangesAsync();

        banco.Consentimentos.Add(new Consentimento { UsuarioId = usuario.Id, Tipo = TextosConsentimento.TipoGeral, Versao = 1, AceitoEm = DateTime.UtcNow });
        if (comObjetivo)
        {
            banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 83.7m, Objetivo = "perder peso", DataRegistro = DateTime.UtcNow });
        }
        await banco.SaveChangesAsync();
        return usuario;
    }

    private static CasaController Controller(ApplicationDbContext banco, int usuarioId) => new(banco, BancoEmMemoria.Configuracao())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"))
            }
        }
    };

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

    private static async Task<CasaResponse> CriarCasa(ApplicationDbContext banco, Usuario dono)
    {
        var resultado = await Controller(banco, dono.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa });
        return Assert.IsType<CasaResponse>(Assert.IsType<CreatedResult>(resultado).Value);
    }

    private static async Task<string> Convidar(ApplicationDbContext banco, Usuario dono)
    {
        var criado = Assert.IsType<ConviteCriadoResponse>(Assert.IsType<CreatedResult>(await Controller(banco, dono.Id).CriarConvite()).Value);
        return criado.Link.Split("#convite=")[1];
    }

    private static Task<IActionResult> Aceitar(ApplicationDbContext banco, Usuario pessoa, string token, int? versao = -1) =>
        Controller(banco, pessoa.Id).AceitarConvite(new AceitarConviteRequest { Token = token, VersaoConsentimento = versao == -1 ? VersaoCasa : versao });

    private static async Task Entrar(ApplicationDbContext banco, Usuario dono, Usuario pessoa) =>
        Assert.IsType<OkObjectResult>(await Aceitar(banco, pessoa, await Convidar(banco, dono)));

    private static Task<ConviteCasa> ConviteDoToken(ApplicationDbContext banco, string token)
    {
        var hash = OperacoesCasa.Hash(token);
        return banco.ConvitesCasa.SingleAsync(c => c.TokenHash == hash);
    }

    private static async Task<CasaResponse> Ver(ApplicationDbContext banco, Usuario pessoa) =>
        Assert.IsType<CasaResponse>(Assert.IsType<OkObjectResult>(await Controller(banco, pessoa.Id).Obter()).Value);

    private static Task<bool> AceiteCasaVigente(ApplicationDbContext banco, Usuario pessoa) =>
        RegrasConsentimento.TemAceiteVigenteAsync(banco, pessoa.Id, TextosConsentimento.TipoCasa);

    // ---------- Criar ----------

    [Fact]
    public async Task Criar_Casa_Vira_Dono_E_Grava_O_Aceite_Da_Casa()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test", "Ana Sobrenomeficticio");

        var resultado = await Controller(banco, ana.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa, Orcamento = "moderado" });

        var casa = Assert.IsType<CasaResponse>(Assert.IsType<CreatedResult>(resultado).Value);
        Assert.True(casa.SouDono);
        Assert.Equal("moderado", casa.Orcamento);
        var eu = Assert.Single(casa.Membros);
        Assert.Equal(("Ana", true, true), (eu.PrimeiroNome, eu.Dono, eu.SouEu));
        Assert.Empty(casa.ConvitesAbertos!);
        Assert.True(await AceiteCasaVigente(banco, ana));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task Criar_Sem_Aceite_Ou_Com_Versao_Errada_Da_409_E_Nao_Cria(int? deslocamento)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");

        var resultado = await Controller(banco, ana.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa + deslocamento });

        Assert.Equal(409, Status(resultado));
        Assert.Equal(RegrasConsentimento.MotivoSemAceiteCasa, Motivo(resultado));
        Assert.Equal(0, await banco.Casas.CountAsync());
        Assert.False(await AceiteCasaVigente(banco, ana));
    }

    [Fact]
    public async Task Criar_Ja_Estando_Numa_Casa_Da_409()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        await CriarCasa(banco, ana);

        var resultado = await Controller(banco, ana.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa });

        Assert.Equal((409, CasaController.MotivoJaEmCasa), (Status(resultado), Motivo(resultado)));
        Assert.Equal(1, await banco.Casas.CountAsync());
    }

    [Fact]
    public async Task Orcamento_Fora_Dos_Valores_Da_400_E_O_Dono_Muda_O_Orcamento()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");

        Assert.Equal(400, Status(await Controller(banco, ana.Id).Criar(new CriarCasaRequest { VersaoConsentimento = VersaoCasa, Orcamento = "caro" })));
        await CriarCasa(banco, ana);

        var resultado = await Controller(banco, ana.Id).AtualizarOrcamento(new OrcamentoCasaRequest { Orcamento = "economico" });

        Assert.Equal("economico", Assert.IsType<CasaResponse>(Assert.IsType<OkObjectResult>(resultado).Value).Orcamento);
        Assert.Equal(400, Status(await Controller(banco, ana.Id).AtualizarOrcamento(new OrcamentoCasaRequest { Orcamento = "caro" })));
    }

    [Fact]
    public async Task Sem_Casa_O_Get_Da_404()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");

        Assert.IsType<NotFoundObjectResult>(await Controller(banco, ana.Id).Obter());
    }

    // ---------- Convite ----------

    [Fact]
    public async Task Convite_Usa_A_Url_Da_Configuracao_Vale_7_Dias_E_Guarda_So_O_Hash()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        await CriarCasa(banco, ana);
        var antes = DateTime.UtcNow;

        var criado = Assert.IsType<ConviteCriadoResponse>(Assert.IsType<CreatedResult>(await Controller(banco, ana.Id).CriarConvite()).Value);

        Assert.StartsWith("http://127.0.0.1:5500/frontend/casa-convite.html#convite=", criado.Link);
        var token = criado.Link.Split("#convite=")[1];
        Assert.Equal(43, token.Length);
        var gravado = await banco.ConvitesCasa.SingleAsync();
        Assert.Equal(OperacoesCasa.Hash(token), gravado.TokenHash);
        Assert.DoesNotContain(token, gravado.TokenHash);
        Assert.Equal(ana.Id, gravado.UsuarioId);
        Assert.InRange(gravado.ExpiraEm, antes.AddDays(7), DateTime.UtcNow.AddDays(7));
    }

    [Fact]
    public async Task Gerar_Outro_Convite_Nao_Invalida_O_Anterior_E_O_Dono_Ve_Os_Abertos()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        var primeiro = await Convidar(banco, ana);
        await Convidar(banco, ana);

        Assert.Equal(2, (await Ver(banco, ana)).ConvitesAbertos!.Count);
        Assert.IsType<OkObjectResult>(await Aceitar(banco, bia, primeiro));

        // O usado sai dos abertos; o membro não vê convite nenhum.
        Assert.Single((await Ver(banco, ana)).ConvitesAbertos!);
        Assert.Null((await Ver(banco, bia)).ConvitesAbertos);
    }

    [Fact]
    public async Task Verificar_Convite_Valido_Mostra_So_O_Primeiro_Nome_Do_Dono_A_Contagem_E_O_Texto()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test", "Ana Sobrenomeficticio");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        var token = await Convidar(banco, ana);

        var resultado = await Controller(banco, bia.Id).VerificarConvite(new VerificarConviteRequest { Token = token });

        var verificado = Assert.IsType<ConviteVerificadoResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("Ana", verificado.PrimeiroNomeDono);
        Assert.Equal(1, verificado.Membros);
        Assert.Equal(VersaoCasa, verificado.Consentimento.Versao);
        Assert.Equal(TextosConsentimento.TextoVigente("casa").Corpo, verificado.Consentimento.Texto);
        var json = JsonSerializer.Serialize(verificado);
        Assert.DoesNotContain("Sobrenomeficticio", json);
        Assert.DoesNotContain("ana@exemplo.test", json);
        // Verificar não usa o convite.
        Assert.Null((await ConviteDoToken(banco, token)).UsadoEm);
    }

    [Theory]
    [InlineData("inexistente", 400, CasaController.MotivoInvalido)]
    [InlineData("vazio", 400, CasaController.MotivoInvalido)]
    [InlineData("revogado", 400, CasaController.MotivoInvalido)]
    [InlineData("usado", 400, CasaController.MotivoUsado)]
    [InlineData("expirado", 400, CasaController.MotivoExpirado)]
    public async Task Convite_Que_Nao_Serve_Da_O_Motivo_Na_Verificacao_E_No_Aceite(string situacao, int status, string motivo)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        var token = await Convidar(banco, ana);
        var convite = await ConviteDoToken(banco, token);
        switch (situacao)
        {
            case "inexistente": token = OperacoesCasa.NovoToken(); break;
            case "vazio": token = " "; break;
            case "revogado": Assert.IsType<NoContentResult>(await Controller(banco, ana.Id).RevogarConvite(convite.Id)); break;
            case "usado": convite.UsadoEm = DateTime.UtcNow.AddMinutes(-1); break;
            case "expirado": convite.ExpiraEm = DateTime.UtcNow.AddSeconds(-1); break;
        }
        await banco.SaveChangesAsync();

        var verificar = await Controller(banco, bia.Id).VerificarConvite(new VerificarConviteRequest { Token = token });
        var aceitar = await Aceitar(banco, bia, token);

        Assert.Equal((status, motivo), (Status(verificar), Motivo(verificar)));
        Assert.Equal((status, motivo), (Status(aceitar), Motivo(aceitar)));
        Assert.False(await banco.MembrosCasa.AnyAsync(m => m.UsuarioId == bia.Id));
        Assert.False(await AceiteCasaVigente(banco, bia));
    }

    [Fact]
    public async Task Quem_Ja_Esta_Numa_Casa_Recebe_409_Inclusive_Na_Mesma()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        await CriarCasa(banco, bia);
        var token = await Convidar(banco, ana);

        // Outra casa.
        var deBia = await Aceitar(banco, bia, token);
        // A mesma: o dono abrindo o próprio link.
        var deAna = await Controller(banco, ana.Id).VerificarConvite(new VerificarConviteRequest { Token = token });

        Assert.Equal((409, CasaController.MotivoJaEmCasa), (Status(deBia), Motivo(deBia)));
        Assert.Equal((409, CasaController.MotivoJaEmCasa), (Status(deAna), Motivo(deAna)));
        Assert.Null((await ConviteDoToken(banco, token)).UsadoEm);
    }

    [Theory]
    [InlineData("nascimento")]
    [InlineData("altura")]
    [InlineData("sexo")]
    [InlineData("objetivo")]
    public async Task Cadastro_Incompleto_Da_409(string falta)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test",
            altura: falta == "altura" ? null : 165m, sexo: falta == "sexo" ? null : "Feminino", comObjetivo: falta != "objetivo");
        if (falta == "nascimento")
        {
            bia.DataNascimento = null;
            await banco.SaveChangesAsync();
        }
        await CriarCasa(banco, ana);
        var token = await Convidar(banco, ana);

        var resultado = await Aceitar(banco, bia, token);

        Assert.Equal((409, CasaController.MotivoCadastroIncompleto), (Status(resultado), Motivo(resultado)));
        Assert.Equal(1, await banco.MembrosCasa.CountAsync());
    }

    [Fact]
    public async Task Menor_De_18_Da_403()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var jovem = await Pessoa(banco, "jovem@exemplo.test", nascimento: DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-17));
        await CriarCasa(banco, ana);
        var token = await Convidar(banco, ana);

        var verificar = await Controller(banco, jovem.Id).VerificarConvite(new VerificarConviteRequest { Token = token });
        var aceitar = await Aceitar(banco, jovem, token);

        Assert.Equal((403, CasaController.MotivoMenorDe18), (Status(verificar), Motivo(verificar)));
        Assert.Equal((403, CasaController.MotivoMenorDe18), (Status(aceitar), Motivo(aceitar)));
        Assert.Equal(1, await banco.MembrosCasa.CountAsync());
    }

    [Fact]
    public async Task Casa_Com_6_Pessoas_Esta_Cheia()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        await CriarCasa(banco, ana);
        var sobra = await Convidar(banco, ana);
        for (var i = 1; i <= 5; i++)
        {
            await Entrar(banco, ana, await Pessoa(banco, $"membro{i}@exemplo.test"));
        }
        var setima = await Pessoa(banco, "setima@exemplo.test");

        var resultado = await Aceitar(banco, setima, sobra);

        Assert.Equal((409, CasaController.MotivoCasaCheia), (Status(resultado), Motivo(resultado)));
        Assert.Equal(6, await banco.MembrosCasa.CountAsync());
        // Nem link novo: não teria quem pudesse usar.
        Assert.Equal(409, Status(await Controller(banco, ana.Id).CriarConvite()));
    }

    [Fact]
    public async Task Aceite_Grava_O_Aceite_O_Membro_E_O_Uso_Do_Convite()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test", "Ana Sobrenomeficticio");
        var bia = await Pessoa(banco, "bia@exemplo.test", "Bia Outrosobrenome");
        await CriarCasa(banco, ana);
        var token = await Convidar(banco, ana);

        var resultado = await Aceitar(banco, bia, token);

        var casa = Assert.IsType<CasaResponse>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.False(casa.SouDono);
        Assert.Equal(new[] { ("Ana", true, false), ("Bia", false, true) }, casa.Membros.Select(m => (m.PrimeiroNome, m.Dono, m.SouEu)));
        var membro = await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == bia.Id);
        Assert.Equal(MembroCasa.PapelMembro, membro.Papel);
        Assert.True(await AceiteCasaVigente(banco, bia));
        Assert.NotNull((await ConviteDoToken(banco, token)).UsadoEm);
    }

    [Fact]
    public async Task O_Mesmo_Convite_So_Serve_Uma_Vez()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        var caio = await Pessoa(banco, "caio@exemplo.test");
        await CriarCasa(banco, ana);
        var token = await Convidar(banco, ana);

        Assert.IsType<OkObjectResult>(await Aceitar(banco, bia, token));
        var segundo = await Aceitar(banco, caio, token);

        Assert.Equal((400, CasaController.MotivoUsado), (Status(segundo), Motivo(segundo)));
        Assert.False(await banco.MembrosCasa.AnyAsync(m => m.UsuarioId == caio.Id));
    }

    [Fact]
    public async Task O_Uso_Unico_Recusa_Quem_Chega_Depois_Mesmo_Depois_De_Conferir()
    {
        // O que o UPDATE condicional garante no PostgreSQL: se o convite foi usado entre a conferência e a gravação,
        // UsarConviteAsync diz que não foi este pedido que o usou.
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        await CriarCasa(banco, ana);
        var convite = await ConviteDoToken(banco, await Convidar(banco, ana));
        var agora = DateTime.UtcNow;

        Assert.True(await OperacoesCasa.UsarConviteAsync(banco, convite.Id, agora));
        await banco.SaveChangesAsync();
        Assert.False(await OperacoesCasa.UsarConviteAsync(banco, convite.Id, agora));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(99)]
    public async Task Aceitar_Sem_O_Aceite_Da_Casa_Da_409_E_Nao_Usa_O_Convite(int? versao)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        var token = await Convidar(banco, ana);

        var resultado = await Aceitar(banco, bia, token, versao);

        Assert.Equal((409, RegrasConsentimento.MotivoSemAceiteCasa), (Status(resultado), Motivo(resultado)));
        Assert.Null((await ConviteDoToken(banco, token)).UsadoEm);
        Assert.False(await banco.MembrosCasa.AnyAsync(m => m.UsuarioId == bia.Id));
    }

    // ---------- Permissões ----------

    [Fact]
    public async Task Membro_Em_Rota_De_Dono_Recebe_403()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        var caio = await Pessoa(banco, "caio@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        await Entrar(banco, ana, caio);
        var conviteId = (await banco.ConvitesCasa.FirstAsync()).Id;
        var membroCaio = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == caio.Id)).Id;
        var controller = Controller(banco, bia.Id);

        Assert.Equal(403, Status(await controller.AtualizarOrcamento(new OrcamentoCasaRequest { Orcamento = "moderado" })));
        Assert.Equal(403, Status(await controller.CriarConvite()));
        Assert.Equal(403, Status(await controller.RevogarConvite(conviteId)));
        Assert.Equal(403, Status(await controller.RemoverMembro(membroCaio)));
        Assert.Equal(403, Status(await controller.OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroCaio })));
        // Sem oferta para ela, recusar também é de dono.
        Assert.Equal(403, Status(await controller.CancelarTransferencia()));
        Assert.Equal(3, await banco.MembrosCasa.CountAsync());
    }

    [Fact]
    public async Task Quem_Nao_E_Da_Casa_Recebe_404_Em_Toda_Rota_De_Membro()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        var estranho = await Pessoa(banco, "estranho@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        var conviteId = (await banco.ConvitesCasa.FirstAsync()).Id;
        var membroBia = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == bia.Id)).Id;
        var controller = Controller(banco, estranho.Id);

        Assert.Equal(404, Status(await controller.Obter()));
        Assert.Equal(404, Status(await controller.AtualizarOrcamento(new OrcamentoCasaRequest())));
        Assert.Equal(404, Status(await controller.CriarConvite()));
        Assert.Equal(404, Status(await controller.RevogarConvite(conviteId)));
        Assert.Equal(404, Status(await controller.RemoverMembro(membroBia)));
        Assert.Equal(404, Status(await controller.Sair()));
        Assert.Equal(404, Status(await controller.OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroBia })));
        Assert.Equal(404, Status(await controller.AceitarTransferencia()));
        Assert.Equal(404, Status(await controller.CancelarTransferencia()));
        Assert.Equal(2, await banco.MembrosCasa.CountAsync());
    }

    [Fact]
    public async Task Dono_Nao_Mexe_Em_Membro_Nem_Convite_De_Outra_Casa()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        var caio = await Pessoa(banco, "caio@exemplo.test");
        await CriarCasa(banco, ana);
        await CriarCasa(banco, bia);
        await Entrar(banco, bia, caio);
        var conviteDeBia = (await banco.ConvitesCasa.SingleAsync()).Id;
        var membroCaio = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == caio.Id)).Id;
        var controller = Controller(banco, ana.Id);

        Assert.Equal(404, Status(await controller.RemoverMembro(membroCaio)));
        Assert.Equal(404, Status(await controller.RevogarConvite(conviteDeBia)));
        Assert.Equal(404, Status(await controller.OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroCaio })));
        Assert.True(await banco.MembrosCasa.AnyAsync(m => m.Id == membroCaio));
    }

    [Fact]
    public async Task Sem_O_Aceite_Da_Casa_Vigente_A_Casa_Nao_Aparece_Mas_Da_Para_Sair()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        foreach (var aceite in banco.Consentimentos.Where(c => c.UsuarioId == bia.Id && c.Tipo == "casa"))
        {
            aceite.RevogadoEm = DateTime.UtcNow;
        }
        await banco.SaveChangesAsync();

        var obter = await Controller(banco, bia.Id).Obter();

        Assert.Equal((409, RegrasConsentimento.MotivoSemAceiteCasa), (Status(obter), Motivo(obter)));
        Assert.IsType<NoContentResult>(await Controller(banco, bia.Id).Sair());
    }

    // ---------- Sair e remover ----------

    [Fact]
    public async Task Membro_Sai_E_O_Aceite_Da_Casa_E_Revogado()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);

        Assert.IsType<NoContentResult>(await Controller(banco, bia.Id).Sair());

        Assert.False(await banco.MembrosCasa.AnyAsync(m => m.UsuarioId == bia.Id));
        var aceite = await banco.Consentimentos.SingleAsync(c => c.UsuarioId == bia.Id && c.Tipo == "casa");
        Assert.NotNull(aceite.RevogadoEm);
        Assert.Single((await Ver(banco, ana)).Membros);
    }

    [Fact]
    public async Task Dono_Com_Membros_Nao_Sai_E_Dono_Sozinho_Leva_A_Casa_Junto()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        await Convidar(banco, ana);

        var comMembros = await Controller(banco, ana.Id).Sair();

        Assert.Equal((409, CasaController.MotivoTransferirAntes), (Status(comMembros), Motivo(comMembros)));
        Assert.Equal(2, await banco.MembrosCasa.CountAsync());

        Assert.IsType<NoContentResult>(await Controller(banco, bia.Id).Sair());
        Assert.IsType<NoContentResult>(await Controller(banco, ana.Id).Sair());

        Assert.Equal(0, await banco.Casas.CountAsync());
        Assert.Equal(0, await banco.MembrosCasa.CountAsync());
        Assert.Equal(0, await banco.ConvitesCasa.CountAsync());
        Assert.False(await AceiteCasaVigente(banco, ana));
    }

    [Fact]
    public async Task Dono_Remove_Membro_Mas_Nao_A_Si_Mesmo()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        var membroAna = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == ana.Id)).Id;
        var membroBia = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == bia.Id)).Id;

        Assert.Equal(400, Status(await Controller(banco, ana.Id).RemoverMembro(membroAna)));
        Assert.IsType<NoContentResult>(await Controller(banco, ana.Id).RemoverMembro(membroBia));

        Assert.False(await banco.MembrosCasa.AnyAsync(m => m.UsuarioId == bia.Id));
        Assert.False(await AceiteCasaVigente(banco, bia));
        Assert.True(await AceiteCasaVigente(banco, ana));
        Assert.Equal(404, Status(await Controller(banco, bia.Id).Obter()));
    }

    [Fact]
    public async Task Quem_Saiu_Entra_De_Novo_Com_Um_Aceite_Novo()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        Assert.IsType<NoContentResult>(await Controller(banco, bia.Id).Sair());

        await Entrar(banco, ana, bia);

        var aceites = await banco.Consentimentos.Where(c => c.UsuarioId == bia.Id && c.Tipo == "casa").ToListAsync();
        Assert.Equal(2, aceites.Count);
        Assert.Single(aceites, a => a.RevogadoEm is null);
    }

    // ---------- Transferência ----------

    [Fact]
    public async Task Transferencia_Oferecida_E_Aceita_Troca_Os_Papeis()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        var membroBia = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == bia.Id)).Id;

        Assert.IsType<NoContentResult>(await Controller(banco, ana.Id).OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroBia }));
        Assert.True((await Ver(banco, bia)).Membros.Single(m => m.SouEu).TransferenciaPendente);

        var aceitou = await Controller(banco, bia.Id).AceitarTransferencia();

        var casa = Assert.IsType<CasaResponse>(Assert.IsType<OkObjectResult>(aceitou).Value);
        Assert.True(casa.SouDono);
        Assert.All(casa.Membros, m => Assert.False(m.TransferenciaPendente));
        Assert.Equal(MembroCasa.PapelMembro, (await banco.MembrosCasa.AsNoTracking().SingleAsync(m => m.UsuarioId == ana.Id)).Papel);
        Assert.Equal(1, await banco.MembrosCasa.CountAsync(m => m.Papel == MembroCasa.PapelDono));
        // Agora a antiga dona pode sair.
        Assert.IsType<NoContentResult>(await Controller(banco, ana.Id).Sair());
    }

    [Fact]
    public async Task Transferencia_Recusada_Ou_Cancelada_Some_E_Nao_Se_Aceita_Sem_Oferta()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        var caio = await Pessoa(banco, "caio@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        await Entrar(banco, ana, caio);
        var membroBia = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == bia.Id)).Id;
        var membroCaio = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == caio.Id)).Id;
        var dono = Controller(banco, ana.Id);

        // Bia recusa.
        await dono.OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroBia });
        Assert.IsType<NoContentResult>(await Controller(banco, bia.Id).CancelarTransferencia());
        Assert.Equal(409, Status(await Controller(banco, bia.Id).AceitarTransferencia()));

        // Oferecer ao Caio e depois à Bia deixa só a da Bia; o dono cancela.
        await dono.OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroCaio });
        await dono.OferecerTransferencia(new TransferenciaCasaRequest { MembroId = membroBia });
        Assert.Equal(409, Status(await Controller(banco, caio.Id).AceitarTransferencia()));
        Assert.IsType<NoContentResult>(await dono.CancelarTransferencia());
        Assert.Equal(409, Status(await Controller(banco, bia.Id).AceitarTransferencia()));

        Assert.Equal(400, Status(await dono.OferecerTransferencia(new TransferenciaCasaRequest
        {
            MembroId = (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == ana.Id)).Id
        })));
        Assert.Equal(ana.Id, (await banco.MembrosCasa.SingleAsync(m => m.Papel == MembroCasa.PapelDono)).UsuarioId);
        Assert.False(await banco.MembrosCasa.AnyAsync(m => m.TransferenciaOferecidaEm != null));
    }

    // ---------- Exclusão de conta, exportação e limpeza ----------

    [Fact]
    public async Task Membro_Que_Apaga_A_Conta_Sai_E_A_Casa_Fica()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);

        Assert.True(await ExclusaoConta.ApagarAsync(banco, bia.Id));

        Assert.Equal(1, await banco.Casas.CountAsync());
        Assert.Equal(ana.Id, (await banco.MembrosCasa.SingleAsync()).UsuarioId);
    }

    [Fact]
    public async Task Ultimo_Da_Casa_Que_Apaga_A_Conta_Leva_A_Casa()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        await CriarCasa(banco, ana);
        await Convidar(banco, ana);

        Assert.True(await ExclusaoConta.ApagarAsync(banco, ana.Id));

        Assert.Equal(0, await banco.Casas.CountAsync());
        Assert.Equal(0, await banco.MembrosCasa.CountAsync());
        Assert.Equal(0, await banco.ConvitesCasa.CountAsync());
    }

    [Fact]
    public async Task Dono_Com_Membros_Que_Apaga_A_Conta_Passa_A_Casa_Ao_Mais_Antigo()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        var bia = await Pessoa(banco, "bia@exemplo.test");
        var caio = await Pessoa(banco, "caio@exemplo.test");
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, caio);
        await Entrar(banco, ana, bia);
        // Caio entrou antes, mas a data decide (e não a ordem das linhas): Bia fica como a mais antiga.
        (await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == bia.Id)).EntrouEm = DateTime.UtcNow.AddDays(-3);
        var membroCaio = await banco.MembrosCasa.SingleAsync(m => m.UsuarioId == caio.Id);
        membroCaio.EntrouEm = DateTime.UtcNow.AddDays(-1);
        membroCaio.TransferenciaOferecidaEm = DateTime.UtcNow;
        await banco.SaveChangesAsync();

        Assert.True(await ExclusaoConta.ApagarAsync(banco, ana.Id));

        var membros = await banco.MembrosCasa.AsNoTracking().ToListAsync();
        Assert.Equal(2, membros.Count);
        Assert.Equal(MembroCasa.PapelDono, membros.Single(m => m.UsuarioId == bia.Id).Papel);
        Assert.Equal(MembroCasa.PapelMembro, membros.Single(m => m.UsuarioId == caio.Id).Papel);
        Assert.All(membros, m => Assert.Null(m.TransferenciaOferecidaEm));
        Assert.Equal(1, await banco.Casas.CountAsync());
        // Os convites eram linhas da Ana: saem com a conta dela.
        Assert.Equal(0, await banco.ConvitesCasa.CountAsync());
        Assert.True((await Ver(banco, bia)).SouDono);
    }

    [Fact]
    public async Task Exportacao_Leva_O_Papel_Na_Casa_E_Os_Convites_Sem_Hash_Nem_Os_Outros()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test", "Ana Sobrenomeficticio");
        var bia = await Pessoa(banco, "bia@exemplo.test", "Bia Outrosobrenome");
        await CriarCasa(banco, ana);
        var token = await Convidar(banco, ana);
        Assert.IsType<OkObjectResult>(await Aceitar(banco, bia, token));
        await Convidar(banco, ana);

        var deAna = System.Text.Encoding.UTF8.GetString((await ExportacaoConta.GerarAsync(banco, ana.Id, DateTime.UtcNow))!);
        var deBia = System.Text.Encoding.UTF8.GetString((await ExportacaoConta.GerarAsync(banco, bia.Id, DateTime.UtcNow))!);

        using var jsonAna = JsonDocument.Parse(deAna);
        Assert.Equal("dono", jsonAna.RootElement.GetProperty("casa").GetProperty("papel").GetString());
        Assert.Equal(2, jsonAna.RootElement.GetProperty("convitesCasa").GetArrayLength());
        foreach (var convite in await banco.ConvitesCasa.ToListAsync())
        {
            Assert.DoesNotContain(convite.TokenHash, deAna, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain(token, deAna);
        Assert.DoesNotContain("Bia", deAna);

        using var jsonBia = JsonDocument.Parse(deBia);
        Assert.Equal("membro", jsonBia.RootElement.GetProperty("casa").GetProperty("papel").GetString());
        Assert.Equal(0, jsonBia.RootElement.GetProperty("convitesCasa").GetArrayLength());
        Assert.DoesNotContain("Ana", deBia);
    }

    [Fact]
    public async Task Limpeza_Apaga_Convites_Usados_Vencidos_Ou_Revogados_Ha_Mais_De_30_Dias()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test");
        await CriarCasa(banco, ana);
        var casaId = (await banco.Casas.SingleAsync()).Id;
        var agora = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        ConviteCasa Convite(string marca, DateTime expira, DateTime? usado = null, DateTime? revogado = null) => new()
        {
            CasaId = casaId, UsuarioId = ana.Id, TokenHash = OperacoesCasa.Hash(marca),
            CriadoEm = expira.AddDays(-7), ExpiraEm = expira, UsadoEm = usado, RevogadoEm = revogado
        };
        banco.ConvitesCasa.AddRange(
            Convite("usado-antigo", agora.AddDays(-30), usado: agora.AddDays(-31)),
            Convite("vencido-antigo", agora.AddDays(-31)),
            Convite("revogado-antigo", agora.AddDays(-30), revogado: agora.AddDays(-31)),
            Convite("usado-recente", agora.AddDays(2), usado: agora.AddDays(-5)),
            Convite("vencido-recente", agora.AddDays(-29)),
            Convite("aberto", agora.AddDays(6)));
        await banco.SaveChangesAsync();

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, agora);

        Assert.Equal(3, resultado.ConvitesCasaApagados);
        Assert.Equal(
            new[] { "aberto", "usado-recente", "vencido-recente" }.Select(OperacoesCasa.Hash).Order(),
            (await banco.ConvitesCasa.Select(c => c.TokenHash).ToListAsync()).Order());
    }

    // ---------- O que a resposta mostra dos outros ----------

    [Fact]
    public async Task Resposta_Da_Casa_Nao_Leva_Dado_De_Saude_Nem_Email_De_Ninguem()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var ana = await Pessoa(banco, "ana@exemplo.test", "Ana Sobrenomeficticio", altura: 181.4m, sexo: "Feminino");
        var bia = await Pessoa(banco, "bia@exemplo.test", "Bia Outrosobrenome", altura: 158.2m, sexo: "Masculino");
        var judo = banco.Modalidades.Add(new Modalidade { Nome = "Judô", MetReferencia = 7.8m, Categoria = "Lutas" }).Entity;
        await banco.SaveChangesAsync();
        banco.UsuarioModalidades.Add(new UsuarioModalidade { UsuarioId = ana.Id, ModalidadeId = judo.Id, DiasSemana = [1], DuracaoMediaHoras = 1.5m });
        await banco.SaveChangesAsync();
        await CriarCasa(banco, ana);
        await Entrar(banco, ana, bia);
        await Convidar(banco, ana);

        foreach (var (pessoa, outro) in new[] { (ana, bia), (bia, ana) })
        {
            var json = JsonSerializer.Serialize(await Ver(banco, pessoa), JsonSerializerOptions.Web);

            foreach (var proibida in new[] { "email", "@", "peso", "altura", "sexo", "idade", "nascimento", "objetivo", "meta",
                "treino", "modalidade", "Judô", "Sobrenomeficticio", "Outrosobrenome", "83.7", "181.4", "158.2", "1990",
                "Masculino", "Feminino", "perder", "token", "hash" })
            {
                Assert.DoesNotContain(proibida, json, StringComparison.OrdinalIgnoreCase);
            }
            Assert.Contains(OperacoesCasa.PrimeiroNome(outro.Nome), json);
        }
    }
}
