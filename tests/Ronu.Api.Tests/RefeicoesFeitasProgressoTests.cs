using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;

namespace Ronu.Api.Tests;

/// <summary>
/// Refeição feita, etapa 5: quantas refeições a pessoa marcou desde a pesagem da pergunta de aderência
/// (GET /api/objetivos/atual, RefeicoesFeitasDesdePesagem). Só o número, sem total. Só contas fictícias.
/// </summary>
public class RefeicoesFeitasProgressoTests
{
    private static ObjetivosController Controller(ApplicationDbContext banco, int usuarioId) => new(banco, new CalculadoraPesoTendencia())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"))
            }
        }
    };

    private static Usuario NovoUsuario(ApplicationDbContext banco, string email)
    {
        var usuario = new Usuario { Nome = "Teste", Email = email, Estado = "SP" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        return usuario;
    }

    private static void Pesagem(ApplicationDbContext banco, Usuario usuario, DateTime quando) =>
        banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 80, Objetivo = "perder peso", DataRegistro = quando });

    private static void Marcacao(ApplicationDbContext banco, Usuario usuario, DateOnly data, int refeicao = 0) =>
        banco.RefeicoesFeitas.Add(new RefeicaoFeita
        {
            UsuarioId = usuario.Id, DietaIAId = 1, Data = data, RefeicaoIndice = refeicao, Kcal = 400, RefeicoesNoDia = 5, MarcadaEm = DateTime.UtcNow
        });

    private static async Task<ObjetivoAtualResponse> Atual(ApplicationDbContext banco, Usuario usuario) =>
        Assert.IsType<ObjetivoAtualResponse>(Assert.IsType<OkObjectResult>(await Controller(banco, usuario.Id).ObterAtual()).Value);

    [Fact]
    public async Task Sem_Registro_Hoje_Conta_Desde_O_Dia_Da_Ultima_Pesagem_Inclusive()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "progresso1@exemplo.test");
        var outro = NovoUsuario(banco, "progresso1-outro@exemplo.test");
        var pesagem = DateTime.UtcNow.AddDays(-3);
        Pesagem(banco, usuario, DateTime.UtcNow.AddDays(-10));
        Pesagem(banco, usuario, pesagem);
        var diaDaPesagem = FusoHorarioEstado.Hoje("SP", pesagem);
        var hoje = FusoHorarioEstado.Hoje("SP", DateTime.UtcNow);
        Marcacao(banco, usuario, diaDaPesagem.AddDays(-1)); // antes: fora
        Marcacao(banco, usuario, diaDaPesagem, 0);
        Marcacao(banco, usuario, diaDaPesagem, 1);
        Marcacao(banco, usuario, hoje);
        Marcacao(banco, outro, hoje); // de outra pessoa: fora
        banco.SaveChanges();

        var resposta = await Atual(banco, usuario);

        Assert.False(resposta.RegistradoHoje);
        Assert.Equal(3, resposta.RefeicoesFeitasDesdePesagem);
    }

    [Fact]
    public async Task Com_Registro_Hoje_Conta_Desde_A_Pesagem_Anterior()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "progresso2@exemplo.test");
        var anterior = DateTime.UtcNow.AddDays(-5);
        Pesagem(banco, usuario, anterior);
        Pesagem(banco, usuario, DateTime.UtcNow);
        var diaAnterior = FusoHorarioEstado.Hoje("SP", anterior);
        Marcacao(banco, usuario, diaAnterior.AddDays(-1));
        Marcacao(banco, usuario, diaAnterior);
        Marcacao(banco, usuario, diaAnterior.AddDays(2));
        banco.SaveChanges();

        var resposta = await Atual(banco, usuario);

        Assert.True(resposta.RegistradoHoje);
        Assert.Equal(2, resposta.RefeicoesFeitasDesdePesagem);
    }

    [Fact]
    public async Task Sem_Pesagem_Anterior_E_Nulo_E_Sem_Marcacao_E_Zero()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var soHoje = NovoUsuario(banco, "progresso3@exemplo.test");
        Pesagem(banco, soHoje, DateTime.UtcNow);
        var semMarcar = NovoUsuario(banco, "progresso4@exemplo.test");
        Pesagem(banco, semMarcar, DateTime.UtcNow.AddDays(-4));
        banco.SaveChanges();

        Assert.Null((await Atual(banco, soHoje)).RefeicoesFeitasDesdePesagem);
        Assert.Equal(0, (await Atual(banco, semMarcar)).RefeicoesFeitasDesdePesagem);
    }
}
