using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
using Ronu.Api.Validacao;

namespace Ronu.Api.Tests;

/// <summary>
/// As regras dos dados da fórmula (RegrasDadosCorporais) no PUT /api/perfil e
/// no POST /api/objetivos: sexo e objetivo só com os valores reconhecidos,
/// faixas de altura e peso, e data de nascimento entre 18 e 100 anos.
/// </summary>
public class RegrasDadosCorporaisTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 17);

    // ---------- Data de nascimento ----------

    [Theory]
    [InlineData("2026-10-18", RegrasDadosCorporais.MensagemNascimentoFuturo)]
    [InlineData("2026-10-17", RegrasDadosCorporais.MensagemIdadeMinima)]   // idade 0
    [InlineData("2008-10-18", RegrasDadosCorporais.MensagemIdadeMinima)]   // faz 18 amanhã
    [InlineData("1926-10-16", RegrasDadosCorporais.MensagemIdadeMaxima)]   // 100 anos e 1 dia
    public void Nascimento_Fora_Da_Faixa(string data, string mensagem)
    {
        Assert.Equal(mensagem, RegrasDadosCorporais.ValidarNascimento(DateOnly.Parse(data), Hoje));
    }

    [Theory]
    [InlineData("2008-10-17")] // faz 18 hoje
    [InlineData("1996-03-12")]
    [InlineData("1926-10-17")] // 100 anos exatos
    public void Nascimento_Dentro_Da_Faixa(string data)
    {
        Assert.Null(RegrasDadosCorporais.ValidarNascimento(DateOnly.Parse(data), Hoje));
    }

    // Nascido em 29/02: num ano não bissexto, faz aniversário em 28/02 pela
    // conta da idade da dieta, e a regra dos 18 anos segue a mesma conta.
    [Fact]
    public void Nascido_Em_29_De_Fevereiro_Segue_A_Mesma_Conta_Da_Idade()
    {
        var nascimento = new DateOnly(2008, 2, 29);

        Assert.Null(RegrasDadosCorporais.ValidarNascimento(nascimento, new DateOnly(2026, 2, 28)));
        Assert.Equal(RegrasDadosCorporais.MensagemIdadeMinima, RegrasDadosCorporais.ValidarNascimento(nascimento, new DateOnly(2026, 2, 27)));
    }

    // ---------- PUT /api/perfil ----------

    private static (ApplicationDbContext Banco, Usuario Usuario, ControllerContext Contexto) Conta()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Teste", Email = "teste@exemplo.com", Estado = "SP" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        var contexto = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()) }, "teste"))
            }
        };
        return (banco, usuario, contexto);
    }

    private static string? Mensagem(IActionResult resultado)
    {
        var valor = ((ObjectResult)resultado).Value!;
        return valor.GetType().GetProperty("mensagem")?.GetValue(valor) as string;
    }

    private static PerfilRequest PerfilValido() => new()
    {
        Altura = 178, Sexo = "Masculino", DataNascimento = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30)
    };

    [Theory]
    [InlineData("masculino", RegrasDadosCorporais.MensagemSexo)]
    [InlineData("M", RegrasDadosCorporais.MensagemSexo)]
    [InlineData("menor", RegrasDadosCorporais.MensagemIdadeMinima)]
    [InlineData("futuro", RegrasDadosCorporais.MensagemNascimentoFuturo)]
    [InlineData("velho", RegrasDadosCorporais.MensagemIdadeMaxima)]
    public async Task Perfil_Com_Valor_Fora_Recusa_Com_400_E_Nao_Grava(string caso, string mensagem)
    {
        var (banco, usuario, contexto) = Conta();
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var pedido = PerfilValido();
        switch (caso)
        {
            case "menor": pedido.DataNascimento = hoje.AddYears(-17); break;
            case "futuro": pedido.DataNascimento = hoje.AddDays(1); break;
            case "velho": pedido.DataNascimento = hoje.AddYears(-100).AddDays(-1); break;
            default: pedido.Sexo = caso; break;
        }

        var resultado = await new PerfilController(banco) { ControllerContext = contexto }.Atualizar(pedido);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(mensagem, Mensagem(resultado));
        Assert.Null(banco.Usuarios.Single(u => u.Id == usuario.Id).Sexo);
    }

    [Fact]
    public async Task Perfil_Valido_Continua_Gravando()
    {
        var (banco, usuario, contexto) = Conta();

        var resultado = await new PerfilController(banco) { ControllerContext = contexto }.Atualizar(PerfilValido());

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal("Masculino", banco.Usuarios.Single(u => u.Id == usuario.Id).Sexo);
    }

    [Theory]
    [InlineData(99.9)]
    [InlineData(250.1)]
    public void Altura_Do_Perfil_Fora_Da_Faixa(double altura)
    {
        var pedido = PerfilValido();
        pedido.Altura = (decimal)altura;

        Assert.Equal(new[] { RegrasDadosCorporais.MensagemAltura }, Validar(pedido));
    }

    // ---------- POST /api/objetivos ----------

    [Theory]
    [InlineData("emagrecer")]
    [InlineData("Perder peso")]
    [InlineData("")]
    public async Task Objetivo_Desconhecido_Recusa_Com_400_E_Nao_Grava(string objetivo)
    {
        var (banco, _, contexto) = Conta();
        var controller = new ObjetivosController(banco, new CalculadoraPesoTendencia()) { ControllerContext = contexto };

        var resultado = await controller.Criar(new ObjetivoRequest { Peso = 80, Objetivo = objetivo });

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(RegrasDadosCorporais.MensagemObjetivo, Mensagem(resultado));
        Assert.Empty(banco.ObjetivosUsuario);
    }

    [Fact]
    public async Task Objetivo_Valido_Continua_Gravando()
    {
        var (banco, _, contexto) = Conta();
        var controller = new ObjetivosController(banco, new CalculadoraPesoTendencia()) { ControllerContext = contexto };

        var resultado = await controller.Criar(new ObjetivoRequest { Peso = 80, Objetivo = "perder peso" });

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Single(banco.ObjetivosUsuario);
    }

    [Theory]
    [InlineData(19.9)]
    [InlineData(300.1)]
    public void Peso_Fora_Da_Faixa(double peso)
    {
        Assert.Equal(new[] { RegrasDadosCorporais.MensagemPeso }, Validar(new ObjetivoRequest { Peso = (decimal)peso, Objetivo = "manter peso" }));
    }

    [Fact]
    public void Limites_De_Peso_E_Altura_Valem()
    {
        Assert.Empty(Validar(new ObjetivoRequest { Peso = 20, Objetivo = "manter peso" }));
        Assert.Empty(Validar(new ObjetivoRequest { Peso = 300, Objetivo = "manter peso" }));
        var perfil = PerfilValido();
        perfil.Altura = 100;
        Assert.Empty(Validar(perfil));
        perfil.Altura = 250;
        Assert.Empty(Validar(perfil));
    }

    private static List<string> Validar(object pedido)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(pedido, new ValidationContext(pedido), resultados, validateAllProperties: true);
        return resultados.Select(r => r.ErrorMessage!).ToList();
    }
}
