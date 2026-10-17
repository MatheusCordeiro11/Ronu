using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Campos novos do redesign no GET /perfil (dados da conta) e no
/// GET /objetivos/tendencia (id, objetivo e aderência de cada ponto).
/// </summary>
public class PerfilTendenciaTests
{
    // ---------- Itens 1 e 2 ----------

    [Fact]
    public async Task Perfil_Traz_Nome_Email_Estado_E_O_Tipo_De_Login()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Ana", Email = "ana@exemplo.com", Estado = "RJ", GoogleId = "sub-123" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        var controller = new PerfilController(banco)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()) }, "teste"))
                }
            }
        };

        var perfil = Assert.IsType<PerfilResponse>(Assert.IsType<OkObjectResult>(await controller.ObterAtual()).Value);

        Assert.Equal(("Ana", "ana@exemplo.com", "RJ"), (perfil.Nome, perfil.Email, perfil.Estado));
        Assert.False(perfil.TemSenha);
        Assert.True(perfil.TemGoogle);
        Assert.Null(perfil.Altura);
    }

    [Fact]
    public async Task Perfil_Sem_Estado_Devolve_Nulo_E_Nao_Vazio()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Bia", Email = "bia@exemplo.com", SenhaHash = "hash" }; // Estado = "" (padrão da coluna)
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        var controller = new PerfilController(banco)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()) }, "teste"))
                }
            }
        };

        var perfil = Assert.IsType<PerfilResponse>(Assert.IsType<OkObjectResult>(await controller.ObterAtual()).Value);

        Assert.Null(perfil.Estado);
        Assert.True(perfil.TemSenha);
        Assert.False(perfil.TemGoogle);
    }

    [Fact]
    public void Tendencia_Repassa_Id_Objetivo_E_Aderencia_De_Cada_Registro()
    {
        var agora = DateTime.UtcNow;
        var registros = new List<RegistroPesoDto>
        {
            new() { Data = agora, Peso = 79, Id = 7, Objetivo = "perder peso", Aderencia = "seguiu" },
            new() { Data = agora.AddDays(-3), Peso = 80, Id = 5, Objetivo = "manter peso" }
        };

        var pontos = new CalculadoraPesoTendencia().Calcular(registros);

        // Em ordem de data, cada ponto com o seu registro.
        Assert.Equal((5, "manter peso", (string?)null), (pontos[0].Id!.Value, pontos[0].Objetivo!, pontos[0].Aderencia));
        Assert.Equal((7, "perder peso", "seguiu"), (pontos[1].Id!.Value, pontos[1].Objetivo!, pontos[1].Aderencia!));
    }
}
