using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Validacao;

namespace Ronu.Api.Tests;

/// <summary>
/// Horários estruturados (acordar, dormir, início do treino), que vão ao prompt no lugar da rotina em texto. As telas
/// que já existem reenviam o perfil e a modalidade sem estes campos: ausente tem de manter o salvo.
/// </summary>
public class HorariosEstruturadosTests
{
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

    [Theory]
    [InlineData("06:00", 6, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData("00:00", 0, 0)]
    public void Formato_Valido_Grava(string texto, int hora, int minuto)
    {
        var r = RegrasHorario.Interpretar(texto);
        Assert.Equal((RegrasHorario.Acao.Gravar, new TimeOnly(hora, minuto), (string?)null), r);
    }

    [Theory]
    [InlineData("7:30")]
    [InlineData("07:30:00")]
    [InlineData("24:00")]
    [InlineData("07h30")]
    [InlineData(" 07:30")]
    public void Formato_Invalido_Da_Erro(string texto) =>
        Assert.Equal(RegrasHorario.MensagemFormato, RegrasHorario.Interpretar(texto).Erro);

    [Fact]
    public void Nulo_Mantem_E_Vazio_Apaga()
    {
        var atual = new TimeOnly(6, 0);
        Assert.Equal(atual, RegrasHorario.Aplicar(atual, RegrasHorario.Interpretar(null)));
        Assert.Null(RegrasHorario.Aplicar(atual, RegrasHorario.Interpretar("")));
    }

    // ---------- PUT /api/perfil ----------

    [Fact]
    public async Task Perfil_Grava_E_Devolve_Os_Horarios()
    {
        var (banco, controller, usuario) = MontarPerfil();

        var resposta = Assert.IsType<PerfilResponse>(Assert.IsType<OkObjectResult>(
            await controller.Atualizar(Pedido(acordar: "06:00", dormir: "23:00"))).Value);

        Assert.Equal(("06:00", "23:00"), (resposta.HorarioAcordar, resposta.HorarioDormir));
        var salvo = await banco.Usuarios.SingleAsync(u => u.Id == usuario.Id);
        Assert.Equal((new TimeOnly(6, 0), new TimeOnly(23, 0)), (salvo.HorarioAcordar, salvo.HorarioDormir));
    }

    // Quem dorme depois da meia-noite grava "durmo" menor que "acordo": a API aceita, sem comparar os dois.
    [Fact]
    public async Task Perfil_Aceita_Dormir_Depois_Da_Meia_Noite()
    {
        var (banco, controller, usuario) = MontarPerfil();

        var resposta = Assert.IsType<PerfilResponse>(Assert.IsType<OkObjectResult>(
            await controller.Atualizar(Pedido(acordar: "07:00", dormir: "00:30"))).Value);

        Assert.Equal(("07:00", "00:30"), (resposta.HorarioAcordar, resposta.HorarioDormir));
        var salvo = await banco.Usuarios.SingleAsync(u => u.Id == usuario.Id);
        Assert.Equal((new TimeOnly(7, 0), new TimeOnly(0, 30)), (salvo.HorarioAcordar, salvo.HorarioDormir));
    }

    [Fact]
    public async Task Perfil_Sem_Os_Campos_Mantem_E_Vazio_Apaga()
    {
        var (banco, controller, usuario) = MontarPerfil();
        await controller.Atualizar(Pedido(acordar: "06:00", dormir: "23:00"));

        // A Alimentação e o Perfil de hoje reenviam o perfil sem os horários.
        await controller.Atualizar(Pedido(acordar: null, dormir: null));
        var salvo = await banco.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.Equal((new TimeOnly(6, 0), new TimeOnly(23, 0)), (salvo.HorarioAcordar, salvo.HorarioDormir));

        await controller.Atualizar(Pedido(acordar: "", dormir: null));
        salvo = await banco.Usuarios.AsNoTracking().SingleAsync(u => u.Id == usuario.Id);
        Assert.Equal(((TimeOnly?)null, (TimeOnly?)new TimeOnly(23, 0)), (salvo.HorarioAcordar, salvo.HorarioDormir));
    }

    [Fact]
    public async Task Perfil_Com_Horario_Invalido_Da_400_E_Nao_Grava_Nada()
    {
        var (banco, controller, usuario) = MontarPerfil();

        var resultado = await controller.Atualizar(Pedido(acordar: "06:00", dormir: "25:00"));

        Assert.IsType<BadRequestObjectResult>(resultado);
        var salvo = await banco.Usuarios.SingleAsync(u => u.Id == usuario.Id);
        Assert.Null(salvo.HorarioAcordar);
        Assert.Null(salvo.Altura);
    }

    // ---------- POST /api/usuarios/modalidades ----------

    [Fact]
    public async Task Modalidade_Grava_O_Inicio_E_Sem_O_Campo_Mantem()
    {
        var (banco, controller, usuario, modalidadeId) = MontarModalidade();

        await controller.AdicionarModalidade(PedidoModalidade(modalidadeId, "19:30"));
        // A tela de treino de hoje reenvia o vínculo sem o horário.
        await controller.AdicionarModalidade(PedidoModalidade(modalidadeId, null));

        var vinculo = await banco.UsuarioModalidades.AsNoTracking().SingleAsync(m => m.UsuarioId == usuario.Id);
        Assert.Equal(new TimeOnly(19, 30), vinculo.HorarioInicio);
        Assert.Equal(new[] { 1, 3 }, vinculo.DiasSemana);

        var lista = Assert.IsAssignableFrom<IEnumerable<UsuarioModalidadeResponse>>(
            Assert.IsType<OkObjectResult>(await controller.ListarMinhasModalidades()).Value);
        Assert.Equal("19:30", Assert.Single(lista).HorarioInicio);

        await controller.AdicionarModalidade(PedidoModalidade(modalidadeId, ""));
        vinculo = await banco.UsuarioModalidades.AsNoTracking().SingleAsync(m => m.UsuarioId == usuario.Id);
        Assert.Null(vinculo.HorarioInicio);
    }

    [Fact]
    public async Task Modalidade_Com_Horario_Invalido_Da_400()
    {
        var (_, controller, _, modalidadeId) = MontarModalidade();

        Assert.IsType<BadRequestObjectResult>(await controller.AdicionarModalidade(PedidoModalidade(modalidadeId, "19h30")));
    }

    private static ClaimsPrincipal Usuario(int id) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "teste"));

    private static (ApplicationDbContext Banco, PerfilController Controller, Usuario Usuario) MontarPerfil()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Teste", Email = "horarios@exemplo.test", Estado = "MG" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        var controller = new PerfilController(banco)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Usuario(usuario.Id) } }
        };
        return (banco, controller, usuario);
    }

    private static PerfilRequest Pedido(string? acordar, string? dormir) => new()
    {
        Altura = 178, Sexo = "Masculino", DataNascimento = Hoje.AddYears(-30).AddDays(-1),
        HorarioAcordar = acordar, HorarioDormir = dormir
    };

    private static (ApplicationDbContext Banco, ModalidadesController Controller, Usuario Usuario, int ModalidadeId) MontarModalidade()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Teste", Email = "horarios-treino@exemplo.test", Estado = "MG" };
        banco.Usuarios.Add(usuario);
        var modalidade = banco.Modalidades.Add(new Modalidade { Nome = "Judô", MetReferencia = 7.8m, Categoria = "Lutas" }).Entity;
        banco.SaveChanges();
        var controller = new ModalidadesController(banco)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Usuario(usuario.Id) } }
        };
        return (banco, controller, usuario, modalidade.Id);
    }

    private static UsuarioModalidadeRequest PedidoModalidade(int modalidadeId, string? horario) => new()
    {
        ModalidadeId = modalidadeId, DiasSemana = new[] { 1, 3 }, DuracaoMediaHoras = 1.5m, HorarioInicio = horario
    };
}
