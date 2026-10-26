using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// GET /api/preferencias-alimentares/conferencia (fase 2b-3): exige login, devolve
/// só as preferências de quem está logado e traz o casamento de cada uma e os
/// mínimos da lista.
/// </summary>
public class ConferenciaPreferenciasTests
{
    private static (ApplicationDbContext Banco, Usuario Ana, Usuario Bia) Montar()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var ana = new Usuario { Nome = "Ana", Email = "ana@exemplo.com", Estado = "RJ" };
        var bia = new Usuario { Nome = "Bia", Email = "bia@exemplo.com", Estado = "SP" };
        banco.Usuarios.AddRange(ana, bia);
        banco.SaveChanges();
        banco.PreferenciasAlimentares.AddRange(
            new PreferenciaAlimentar { UsuarioId = ana.Id, Alimento = "leite", Tipo = "evitar" },
            new PreferenciaAlimentar { UsuarioId = ana.Id, Alimento = "whey", Tipo = "preferido" },
            new PreferenciaAlimentar { UsuarioId = ana.Id, Alimento = "lactoze", Tipo = "evitar" },
            new PreferenciaAlimentar { UsuarioId = ana.Id, Alimento = "cordeiro", Tipo = "evitar" },
            new PreferenciaAlimentar { UsuarioId = bia.Id, Alimento = "peixe", Tipo = "evitar" });
        banco.SaveChanges();
        return (banco, ana, bia);
    }

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

    private static async Task<ConferenciaPreferenciasResponse> Conferir(ApplicationDbContext banco, int usuarioId) =>
        Assert.IsType<ConferenciaPreferenciasResponse>(Assert.IsType<OkObjectResult>(await Controller(banco, usuarioId).Conferir()).Value);

    // Sem token, a rota nem chega a rodar: [Authorize] no controller e nenhum [AllowAnonymous] nela.
    [Fact]
    public void Rota_Exige_Login()
    {
        Assert.NotNull(typeof(PreferenciasAlimentaresController).GetCustomAttribute<AuthorizeAttribute>());
        var metodo = typeof(PreferenciasAlimentaresController).GetMethod(nameof(PreferenciasAlimentaresController.Conferir))!;
        Assert.Null(metodo.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("conferencia", metodo.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact]
    public async Task Devolve_So_As_Preferencias_De_Quem_Esta_Logado()
    {
        var (banco, ana, bia) = Montar();

        var daAna = await Conferir(banco, ana.Id);
        var daBia = await Conferir(banco, bia.Id);

        Assert.Equal(["leite", "whey", "lactoze", "cordeiro"], daAna.Preferencias.Select(p => p.Alimento));
        Assert.Equal(["peixe"], daBia.Preferencias.Select(p => p.Alimento));
        var idsDaAna = banco.PreferenciasAlimentares.Where(p => p.UsuarioId == ana.Id).Select(p => p.Id).ToHashSet();
        Assert.All(daAna.Preferencias, p => Assert.Contains(p.Id, idsDaAna));
    }

    [Fact]
    public async Task Traz_O_Casamento_De_Cada_Preferencia()
    {
        var (banco, ana, _) = Montar();

        var porNome = (await Conferir(banco, ana.Id)).Preferencias.ToDictionary(p => p.Alimento);

        // "leite" tira tudo o que tem lactose (13 itens), com o grupo de cada um.
        Assert.True(porNome["leite"].Casou);
        Assert.Equal(13, porNome["leite"].Alimentos.Count);
        Assert.Contains(porNome["leite"].Alimentos, a => a.Nome == "Queijo muçarela" && a.Grupo == "Queijos");
        Assert.Empty(porNome["leite"].LidoComo);

        // "whey" não está na base.
        Assert.False(porNome["whey"].Casou);
        Assert.Equal(["whey"], porNome["whey"].SemCasamento);

        // "lactoze" casa pela tolerância a 1 letra e diz como foi lido.
        Assert.True(porNome["lactoze"].Casou);
        Assert.Equal(["lactose"], porNome["lactoze"].LidoComo);

        Assert.False(porNome["cordeiro"].Casou);
    }

    [Fact]
    public async Task Traz_Os_Minimos_Da_Lista()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Caio", Email = "caio@exemplo.com" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        banco.PreferenciasAlimentares.Add(new PreferenciaAlimentar { UsuarioId = usuario.Id, Alimento = "sou vegano", Tipo = "evitar" });
        banco.SaveChanges();

        var lista = (await Conferir(banco, usuario.Id)).Lista;

        Assert.False(lista.Atende);
        Assert.Equal(["Proteina"], lista.AbaixoDoMinimo);
        Assert.Empty(lista.Vazios);
        Assert.Equal(4, lista.ItensPorPapel["Proteina"]);
        Assert.Equal(1, lista.GruposDeProteina);
    }

    [Fact]
    public void Tolerancia_Diz_Como_Foi_Lido_Com_Acento()
    {
        Assert.Equal(["glúten"], CasamentoPreferencias.Casar("glutem", BaseNutricional.Itens).LidoComo);
        Assert.Equal(["porco"], CasamentoPreferencias.Casar("porko", BaseNutricional.Itens).LidoComo);
        Assert.Empty(CasamentoPreferencias.Casar("glúten", BaseNutricional.Itens).LidoComo);
    }
}
