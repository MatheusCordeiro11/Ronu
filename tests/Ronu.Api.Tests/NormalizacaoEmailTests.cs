using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Controllers;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;

namespace Ronu.Api.Tests;

/// <summary>
/// Email sempre gravado e comparado normalizado (minúsculas, sem espaços nas
/// pontas): no cadastro e no login (NormalizacaoEmail no AuthController).
/// </summary>
public class NormalizacaoEmailTests
{
    [Theory]
    [InlineData("Fulano@Gmail.com", "fulano@gmail.com")]
    [InlineData("  fulano@gmail.com ", "fulano@gmail.com")]
    [InlineData("FULANO@EXEMPLO.COM.BR", "fulano@exemplo.com.br")]
    public void Normaliza_Para_Minusculas_Sem_Espacos(string escrito, string esperado)
    {
        Assert.Equal(esperado, NormalizacaoEmail.Normalizar(escrito));
    }

    [Fact]
    public async Task Cadastro_Grava_O_Email_Normalizado()
    {
        using var contexto = BancoEmMemoria.NovoContexto();
        var controller = new AuthController(contexto, BancoEmMemoria.Configuracao());

        var resultado = await controller.Cadastro(Cadastro(" Fulano@Gmail.COM "));

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal("fulano@gmail.com", (await contexto.Usuarios.SingleAsync()).Email);
    }

    [Fact]
    public async Task Cadastro_Com_O_Mesmo_Email_Em_Outra_Caixa_E_Recusado()
    {
        using var contexto = BancoEmMemoria.NovoContexto();
        var controller = new AuthController(contexto, BancoEmMemoria.Configuracao());
        await controller.Cadastro(Cadastro("fulano@gmail.com"));

        var resultado = await controller.Cadastro(Cadastro("Fulano@Gmail.com"));

        Assert.IsType<ConflictObjectResult>(resultado);
        Assert.Equal(1, await contexto.Usuarios.CountAsync());
    }

    [Fact]
    public async Task Login_Aceita_O_Email_Em_Outra_Caixa_E_Com_Espacos()
    {
        using var contexto = BancoEmMemoria.NovoContexto();
        contexto.Usuarios.Add(new Usuario
        {
            Nome = "Fulano", Email = "fulano@gmail.com", Estado = "SP",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("senha-de-teste")
        });
        await contexto.SaveChangesAsync();
        var controller = new AuthController(contexto, BancoEmMemoria.Configuracao());

        var resultado = await controller.Login(new LoginRequest { Email = " FULANO@gmail.com", Senha = "senha-de-teste" });

        Assert.IsType<OkObjectResult>(resultado);
    }

    private static CadastroRequest Cadastro(string email) =>
        new() { Nome = "Fulano", Email = email, Senha = "senha-de-teste", Estado = "SP" };
}
