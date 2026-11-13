using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services.Conta;

namespace Ronu.Api.Tests;

/// <summary>
/// LGPD base, etapa 2: apagar a conta não deixa nada da pessoa em nenhuma tabela (e não toca nas outras), a
/// confirmação forte, a exportação sem hash e só com os dados de quem pediu, e os prazos de guarda. Só contas
/// fictícias.
/// </summary>
public class ContaTests
{
    private const string Senha = "senha-de-teste";

    // ---------- Apoio ----------

    // Uma conta com uma linha em cada tabela que tem UsuarioId. Devolve também o hash do token do pedido de senha,
    // para conferir que ele não aparece na exportação.
    private static async Task<(Usuario Usuario, string TokenHash)> ContaCompleta(ApplicationDbContext banco, string email, string alimento, decimal peso, bool comSenha = true)
    {
        var modalidade = await banco.Modalidades.FirstOrDefaultAsync() ?? banco.Modalidades.Add(new Modalidade { Nome = "Judô", MetReferencia = 7.8m, Categoria = "Lutas" }).Entity;
        var usuario = new Usuario
        {
            Nome = $"Pessoa {email}", Email = email, Estado = "SP", Altura = 175, Sexo = "Masculino",
            DataNascimento = new DateOnly(1995, 5, 10), RotinaDiaria = "Acordo às 7h", NivelAtividade = "sentado",
            SenhaHash = comSenha ? BCrypt.Net.BCrypt.HashPassword(Senha) : null,
            GoogleId = comSenha ? null : $"google-{email}", CriadoEm = DateTime.UtcNow.AddDays(-60)
        };
        banco.Usuarios.Add(usuario);
        await banco.SaveChangesAsync();

        var dieta = new DietaIA { UsuarioId = usuario.Id, DataGeracao = DateTime.UtcNow, ConteudoJson = $$"""{"dias":[{"alimento":"{{alimento}}"}]}""" };
        banco.DietasIA.Add(dieta);
        await banco.SaveChangesAsync();

        var tokenHash = Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());
        banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = peso, Objetivo = "perder peso", DataRegistro = DateTime.UtcNow });
        banco.UsuarioModalidades.Add(new UsuarioModalidade { UsuarioId = usuario.Id, ModalidadeId = modalidade.Id, DiasSemana = [1, 3], DuracaoMediaHoras = 1.5m });
        banco.PreferenciasAlimentares.Add(new PreferenciaAlimentar { UsuarioId = usuario.Id, Alimento = alimento, Tipo = "preferido" });
        banco.MetasDieta.Add(new MetaDieta { UsuarioId = usuario.Id, DietaIAId = dieta.Id, DataGeracao = dieta.DataGeracao, VersaoFormula = 3, MetasPorDia = [2000, 2000, 2000, 2000, 2000, 2000, 2000] });
        banco.ListaComprasMarcacoes.Add(new ListaComprasMarcacao { UsuarioId = usuario.Id, DietaIAId = dieta.Id, Chave = "arroz-branco", MarcadoEm = DateTime.UtcNow });
        banco.Consentimentos.Add(new Consentimento { UsuarioId = usuario.Id, Tipo = "geral", Versao = 1, AceitoEm = DateTime.UtcNow });
        banco.Consentimentos.Add(new Consentimento { UsuarioId = usuario.Id, Tipo = "alergias", Versao = 1, AceitoEm = DateTime.UtcNow });
        banco.AlergiasUsuario.Add(new AlergiaUsuario { UsuarioId = usuario.Id, Codigo = "soja", MarcadoEm = DateTime.UtcNow });
        banco.PedidosRedefinicaoSenha.Add(new PedidoRedefinicaoSenha { UsuarioId = usuario.Id, Tipo = PedidoRedefinicaoSenha.TipoLink, TokenHash = tokenHash, CriadoEm = DateTime.UtcNow, ExpiraEm = DateTime.UtcNow.AddMinutes(30) });
        await banco.SaveChangesAsync();

        return (usuario, tokenHash);
    }

    // Quantas linhas da pessoa existem em cada tabela do modelo que tem UsuarioId (mais a própria conta). Por
    // reflexão: uma tabela nova (2c, casa) entra sozinha na conferência.
    private static Dictionary<string, int> LinhasDe(ApplicationDbContext banco, int usuarioId)
    {
        var contagem = new Dictionary<string, int>
        {
            ["Usuarios"] = banco.Usuarios.Count(u => u.Id == usuarioId)
        };

        foreach (var tipo in banco.Model.GetEntityTypes().Where(t => t.FindProperty("UsuarioId") is not null))
        {
            var set = (IQueryable<object>)typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(tipo.ClrType).Invoke(banco, null)!;
            var propriedade = tipo.ClrType.GetProperty("UsuarioId")!;
            contagem[tipo.ClrType.Name] = set.AsNoTracking().AsEnumerable().Count(e => (int)propriedade.GetValue(e)! == usuarioId);
        }

        return contagem;
    }

    private static ContaController Controller(ApplicationDbContext banco, int usuarioId) => new(banco, NullLogger<ContaController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"))
            }
        }
    };

    // ---------- Consultar como a conta confirma a exclusão ----------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Consulta_Diz_So_Se_A_Conta_Tem_Senha(bool comSenha)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var (conta, _) = await ContaCompleta(banco, "consulta@exemplo.test", "banana", 80, comSenha);

        var resposta = Assert.IsType<ContaResponse>(Assert.IsType<OkObjectResult>(await Controller(banco, conta.Id).Consultar()).Value);

        Assert.Equal(comSenha, resposta.TemSenha);
        // Nada além disso: nenhum dado da conta sai por aqui.
        Assert.Equal(new[] { nameof(ContaResponse.TemSenha) }, typeof(ContaResponse).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task Consulta_De_Conta_Que_Nao_Existe_Da_404()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        Assert.IsType<NotFoundResult>(await Controller(banco, 999).Consultar());
    }

    // ---------- Apagar conta ----------

    [Fact]
    public async Task Apagar_Nao_Deixa_Nada_Do_Usuario_Em_Nenhuma_Tabela_E_Nao_Toca_No_Outro()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var (apagar, _) = await ContaCompleta(banco, "apagar@exemplo.test", "banana", 80);
        var (outro, _) = await ContaCompleta(banco, "outro@exemplo.test", "mamão", 70);
        var antes = LinhasDe(banco, apagar.Id);
        var outroAntes = LinhasDe(banco, outro.Id);

        var resultado = await Controller(banco, apagar.Id).Apagar(new ExclusaoContaRequest { Senha = Senha });

        Assert.IsType<NoContentResult>(resultado);
        // A conferência viu as 10 tabelas (9 com UsuarioId, AlergiasUsuario incluída desde a 2c, e a conta), e a conta
        // tinha linha em todas.
        Assert.Equal(10, antes.Count);
        Assert.All(antes, t => Assert.True(t.Value > 0, $"a conta de teste deveria ter linha em {t.Key}"));
        Assert.All(LinhasDe(banco, apagar.Id), t => Assert.True(t.Value == 0, $"sobrou {t.Value} linha(s) em {t.Key}"));
        Assert.Equal(outroAntes, LinhasDe(banco, outro.Id));
    }

    [Fact]
    public async Task Senha_Errada_Ou_Vazia_Da_400_E_Nao_Apaga()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var (usuario, _) = await ContaCompleta(banco, "conta@exemplo.test", "banana", 80);
        var antes = LinhasDe(banco, usuario.Id);
        var controller = Controller(banco, usuario.Id);

        Assert.IsType<BadRequestObjectResult>(await controller.Apagar(new ExclusaoContaRequest { Senha = "outra-senha" }));
        Assert.IsType<BadRequestObjectResult>(await controller.Apagar(new ExclusaoContaRequest()));
        // A frase não serve para conta com senha.
        Assert.IsType<BadRequestObjectResult>(await controller.Apagar(new ExclusaoContaRequest { Confirmacao = ExclusaoContaRequest.FraseConfirmacao }));

        Assert.Equal(antes, LinhasDe(banco, usuario.Id));
    }

    [Theory]
    [InlineData("apagar minha conta")]
    [InlineData("  Apagar Minha Conta ")]
    public async Task Conta_Google_Apaga_Com_A_Frase(string frase)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var (usuario, _) = await ContaCompleta(banco, "google@exemplo.test", "banana", 80, comSenha: false);

        Assert.IsType<NoContentResult>(await Controller(banco, usuario.Id).Apagar(new ExclusaoContaRequest { Confirmacao = frase }));

        Assert.All(LinhasDe(banco, usuario.Id), t => Assert.Equal(0, t.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("apagar")]
    [InlineData("apagar minha conta agora")]
    public async Task Conta_Google_Com_Frase_Errada_Da_400_E_Nao_Apaga(string? frase)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var (usuario, _) = await ContaCompleta(banco, "google@exemplo.test", "banana", 80, comSenha: false);
        var antes = LinhasDe(banco, usuario.Id);

        Assert.IsType<BadRequestObjectResult>(await Controller(banco, usuario.Id).Apagar(new ExclusaoContaRequest { Confirmacao = frase, Senha = "qualquer" }));

        Assert.Equal(antes, LinhasDe(banco, usuario.Id));
    }

    [Fact]
    public async Task Apagar_Conta_Que_Nao_Existe_Mais_Da_204()
    {
        using var banco = BancoEmMemoria.NovoContexto();

        Assert.IsType<NoContentResult>(await Controller(banco, 999).Apagar(new ExclusaoContaRequest { Senha = Senha }));
    }

    // ---------- Exportação ----------

    private static IEnumerable<string> NomesDePropriedades(JsonElement elemento) => elemento.ValueKind switch
    {
        JsonValueKind.Object => elemento.EnumerateObject().SelectMany(p => NomesDePropriedades(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => elemento.EnumerateArray().SelectMany(NomesDePropriedades),
        _ => []
    };

    [Fact]
    public async Task Exportacao_Traz_Tudo_Da_Conta_Sem_Hash_Nem_Token_E_Nada_Do_Outro()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var (pessoa, tokenHash) = await ContaCompleta(banco, "pessoa@exemplo.test", "banana", 81.5m);
        var (outro, tokenHashOutro) = await ContaCompleta(banco, "outro@exemplo.test", "jabuticaba", 66.6m);
        var hashSenha = (await banco.Usuarios.SingleAsync(u => u.Id == pessoa.Id)).SenhaHash!;

        var resultado = Assert.IsType<FileContentResult>(await Controller(banco, pessoa.Id).Exportar());
        var texto = Encoding.UTF8.GetString(resultado.FileContents);
        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        // Tudo da pessoa.
        Assert.Equal(ExportacaoConta.Formato, raiz.GetProperty("formato").GetString());
        Assert.Equal("pessoa@exemplo.test", raiz.GetProperty("conta").GetProperty("email").GetString());
        Assert.True(raiz.GetProperty("conta").GetProperty("temSenha").GetBoolean());
        Assert.Equal(175, raiz.GetProperty("perfil").GetProperty("altura").GetDecimal());
        Assert.Equal(81.5m, raiz.GetProperty("pesagens")[0].GetProperty("peso").GetDecimal());
        Assert.Equal("Judô", raiz.GetProperty("modalidades")[0].GetProperty("modalidade").GetString());
        Assert.Equal("banana", raiz.GetProperty("preferenciasAlimentares")[0].GetProperty("alimento").GetString());
        Assert.Equal(JsonValueKind.Object, raiz.GetProperty("dietas")[0].GetProperty("cardapio").ValueKind);
        Assert.Equal(7, raiz.GetProperty("metasDieta")[0].GetProperty("metasPorDia").GetArrayLength());
        Assert.Equal("arroz-branco", raiz.GetProperty("listaComprasMarcacoes")[0].GetProperty("chave").GetString());
        Assert.Equal("geral", raiz.GetProperty("consentimentos")[0].GetProperty("tipo").GetString());
        // TACO 2c: os chips e o aceite do tipo "alergias".
        Assert.Equal("soja", raiz.GetProperty("alergias")[0].GetProperty("codigo").GetString());
        Assert.Contains(raiz.GetProperty("consentimentos").EnumerateArray(), c => c.GetProperty("tipo").GetString() == "alergias");
        Assert.Equal("link", raiz.GetProperty("pedidosRedefinicaoSenha")[0].GetProperty("tipo").GetString());

        // Sem hash nem token: nem o campo, nem o valor.
        Assert.DoesNotContain(NomesDePropriedades(raiz), nome => nome.Contains("hash", StringComparison.OrdinalIgnoreCase)
            || nome.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hashSenha, texto);
        Assert.DoesNotContain(tokenHash, texto, StringComparison.OrdinalIgnoreCase);

        // Nada do outro.
        Assert.DoesNotContain("outro@exemplo.test", texto);
        Assert.DoesNotContain("jabuticaba", texto);
        Assert.DoesNotContain("66.6", texto);
        Assert.DoesNotContain(tokenHashOutro, texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exportacao_Vem_Como_Anexo_Sem_Cache()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var (pessoa, _) = await ContaCompleta(banco, "pessoa@exemplo.test", "banana", 80);
        var controller = Controller(banco, pessoa.Id);

        var resultado = Assert.IsType<FileContentResult>(await controller.Exportar());

        Assert.Equal("application/json", resultado.ContentType);
        Assert.Matches(@"^ronu-dados-\d{4}-\d{2}-\d{2}\.json$", resultado.FileDownloadName);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    // ---------- Prazos de guarda ----------

    private static async Task<Usuario> ContaSoNomeEEmail(ApplicationDbContext banco, string email, DateTime? criadoEm)
    {
        var usuario = new Usuario { Nome = "Pessoa Fictícia", Email = email, GoogleId = $"google-{email}", CriadoEm = criadoEm };
        banco.Usuarios.Add(usuario);
        await banco.SaveChangesAsync();
        return usuario;
    }

    private static readonly DateTime Agora = new(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Conta_Antiga_Sem_Aceite_E_Com_Dados_Nao_E_Apagada()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var (antiga, _) = await ContaCompleta(banco, "antiga@exemplo.test", "banana", 80);
        banco.Consentimentos.RemoveRange(banco.Consentimentos);
        antiga.CriadoEm = null;
        await banco.SaveChangesAsync();
        var antes = LinhasDe(banco, antiga.Id);

        // O relógio de agora, não o Agora fixo: o pedido de senha da conta é de hoje e não pode sair pelo prazo dele.
        var resultado = await LimpezaRetencao.ExecutarAsync(banco, DateTime.UtcNow);

        Assert.Equal(0, resultado.ContasApagadas);
        Assert.Equal(antes, LinhasDe(banco, antiga.Id));
    }

    [Fact]
    public async Task Conta_Antiga_Sem_Aceite_E_Sem_Dados_Tambem_Nao_E_Apagada()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var antiga = await ContaSoNomeEEmail(banco, "antiga@exemplo.test", criadoEm: null);

        await LimpezaRetencao.ExecutarAsync(banco, Agora);

        Assert.True(await banco.Usuarios.AnyAsync(u => u.Id == antiga.Id));
    }

    [Fact]
    public async Task Conta_Nova_So_Com_Nome_E_Email_Sem_Aceite_Ha_Mais_De_30_Dias_E_Apagada()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var abandonada = await ContaSoNomeEEmail(banco, "abandonada@exemplo.test", Agora.AddDays(-31));
        var recente = await ContaSoNomeEEmail(banco, "recente@exemplo.test", Agora.AddDays(-29));

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, Agora);

        Assert.Equal(1, resultado.ContasApagadas);
        Assert.False(await banco.Usuarios.AnyAsync(u => u.Id == abandonada.Id));
        Assert.True(await banco.Usuarios.AnyAsync(u => u.Id == recente.Id));
    }

    // Cada condição sozinha segura a conta: basta um dado além de nome e email, ou qualquer aceite (mesmo revogado
    // ou de versão antiga), para ela ficar.
    [Theory]
    [InlineData("aceite")]
    [InlineData("aceite-revogado")]
    [InlineData("aceite-versao-antiga")]
    [InlineData("estado")]
    [InlineData("altura")]
    [InlineData("rotina")]
    [InlineData("nivel")]
    [InlineData("pesagem")]
    [InlineData("modalidade")]
    [InlineData("preferencia")]
    [InlineData("dieta")]
    [InlineData("meta")]
    [InlineData("alergia")]
    public async Task Conta_Nova_Com_Qualquer_Dado_Ou_Aceite_Nao_E_Apagada(string dado)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var conta = await ContaSoNomeEEmail(banco, "conta@exemplo.test", Agora.AddDays(-90));
        switch (dado)
        {
            case "aceite": banco.Consentimentos.Add(new Consentimento { UsuarioId = conta.Id, Tipo = "geral", Versao = 1, AceitoEm = Agora.AddDays(-89) }); break;
            case "aceite-revogado": banco.Consentimentos.Add(new Consentimento { UsuarioId = conta.Id, Tipo = "geral", Versao = 1, AceitoEm = Agora.AddDays(-89), RevogadoEm = Agora.AddDays(-80) }); break;
            case "aceite-versao-antiga": banco.Consentimentos.Add(new Consentimento { UsuarioId = conta.Id, Tipo = "geral", Versao = 0, AceitoEm = Agora.AddDays(-89) }); break;
            case "estado": conta.Estado = "RJ"; break;
            case "altura": conta.Altura = 170; break;
            case "rotina": conta.RotinaDiaria = "Trabalho de noite"; break;
            case "nivel": conta.NivelAtividade = "em_pe"; break;
            case "pesagem": banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = conta.Id, Peso = 70, Objetivo = "manter peso", DataRegistro = Agora.AddDays(-89) }); break;
            case "modalidade":
                var judo = banco.Modalidades.Add(new Modalidade { Nome = "Judô", MetReferencia = 7.8m, Categoria = "Lutas" }).Entity;
                await banco.SaveChangesAsync();
                banco.UsuarioModalidades.Add(new UsuarioModalidade { UsuarioId = conta.Id, ModalidadeId = judo.Id, DiasSemana = [2], DuracaoMediaHoras = 1 });
                break;
            case "preferencia": banco.PreferenciasAlimentares.Add(new PreferenciaAlimentar { UsuarioId = conta.Id, Alimento = "banana", Tipo = "preferido" }); break;
            case "dieta": banco.DietasIA.Add(new DietaIA { UsuarioId = conta.Id, DataGeracao = Agora.AddDays(-89), ConteudoJson = "{}" }); break;
            case "meta": banco.MetasDieta.Add(new MetaDieta { UsuarioId = conta.Id, DataGeracao = Agora.AddDays(-89), VersaoFormula = 3 }); break;
            case "alergia": banco.AlergiasUsuario.Add(new AlergiaUsuario { UsuarioId = conta.Id, Codigo = "ovo", MarcadoEm = Agora.AddDays(-89) }); break;
        }
        await banco.SaveChangesAsync();

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, Agora);

        Assert.Equal(0, resultado.ContasApagadas);
        Assert.True(await banco.Usuarios.AnyAsync(u => u.Id == conta.Id));
    }

    [Fact]
    public async Task Pedidos_De_Redefinicao_Com_Mais_De_30_Dias_Saem()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var conta = await ContaSoNomeEEmail(banco, "conta@exemplo.test", criadoEm: null);
        banco.PedidosRedefinicaoSenha.AddRange(
            new PedidoRedefinicaoSenha { UsuarioId = conta.Id, Tipo = PedidoRedefinicaoSenha.TipoAvisoGoogle, CriadoEm = Agora.AddDays(-31) },
            new PedidoRedefinicaoSenha { UsuarioId = conta.Id, Tipo = PedidoRedefinicaoSenha.TipoAvisoGoogle, CriadoEm = Agora.AddDays(-29) });
        await banco.SaveChangesAsync();

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, Agora);

        Assert.Equal(1, resultado.PedidosApagados);
        Assert.Equal(Agora.AddDays(-29), (await banco.PedidosRedefinicaoSenha.SingleAsync()).CriadoEm);
        Assert.True(await banco.Usuarios.AnyAsync(u => u.Id == conta.Id));
    }
}
