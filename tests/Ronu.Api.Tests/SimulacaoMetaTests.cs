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
/// Simulação da meta da fórmula (POST /api/metas/simulacao, MetasController):
/// valores, uso dos dados salvos, validação e erros.
/// </summary>
public class SimulacaoMetaTests
{
    private static readonly CalculadoraGastoCalorico Calc = new();

    // ---------- Simulação (peça A) ----------

    private static (ApplicationDbContext Banco, MetasController Controller, Usuario Usuario) Montar(Action<Usuario>? ajustar = null)
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Teste", Email = "teste@exemplo.com", Estado = "MG" };
        ajustar?.Invoke(usuario);
        banco.Usuarios.Add(usuario);
        banco.Modalidades.AddRange(
            new Modalidade { Id = 1, Nome = "Jiu-jitsu", MetReferencia = 7.8m },
            new Modalidade { Id = 2, Nome = "Musculação", MetReferencia = 3.5m });
        banco.SaveChanges();

        var controller = new MetasController(banco, Calc, new CalculadoraPesoTendencia())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()) }, "teste"))
                }
            }
        };
        return (banco, controller, usuario);
    }

    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

    private static SimulacaoMetaRequest PedidoCompleto() => new()
    {
        Sexo = "Masculino", Altura = 178, DataNascimento = Hoje.AddYears(-30).AddDays(-1), Peso = 80, Objetivo = "perder peso",
        Modalidades = new() { new() { ModalidadeId = 1, DiasSemana = new[] { 1, 2, 4, 5 }, DuracaoMediaHoras = 1.5m } }
    };

    private static SimulacaoMetaResponse Ok(IActionResult resultado) =>
        Assert.IsType<SimulacaoMetaResponse>(Assert.IsType<OkObjectResult>(resultado).Value);

    private static string? Mensagem(IActionResult resultado)
    {
        var valor = ((ObjectResult)resultado).Value!;
        return valor.GetType().GetProperty("mensagem")?.GetValue(valor) as string;
    }

    [Fact]
    public async Task Simulacao_Do_Perfil_A_Com_Tudo_No_Pedido()
    {
        var (_, controller, _) = Montar();

        var r = Ok(await controller.Simular(PedidoCompleto()));

        Assert.Equal(CalculadoraManutencao.VersaoFormula, r.VersaoFormula);
        Assert.Equal(30, r.Entradas.Idade);
        Assert.Equal("informado", r.Entradas.PesoOrigem);
        Assert.Equal(1768, r.GastoRepouso.Total);
        Assert.Equal(new[] { 800, 1113, -150, 5 }, new[] { r.GastoRepouso.Peso, r.GastoRepouso.Altura, r.GastoRepouso.Idade, r.GastoRepouso.Sexo });
        Assert.Equal(707, r.AtividadeForaDoTreino.Kcal);
        Assert.Equal(1.4m, r.AtividadeForaDoTreino.Fator);
        Assert.Equal(-0.4m, r.RitmoEsperadoKgSemana);

        var jj = Assert.Single(r.Modalidades);
        Assert.Equal(816, jj.KcalSessao);
        Assert.Equal("Jiu-jitsu", jj.Nome);

        Assert.Equal(7, r.Dias.Count);
        var segunda = r.Dias[0];
        Assert.Equal((816, 3291, -440, 2851), (segunda.TreinoKcal, segunda.Manutencao, segunda.ObjetivoKcal, segunda.MetaFormula));
        var quarta = r.Dias[2];
        Assert.Equal((0, 2475, -440, 2035), (quarta.TreinoKcal, quarta.Manutencao, quarta.ObjetivoKcal, quarta.MetaFormula));
    }

    [Fact]
    public async Task Todo_Total_Da_Simulacao_E_A_Soma_Das_Partes_E_Bate_Com_A_Formula()
    {
        var (_, controller, _) = Montar();
        var pedido = PedidoCompleto();
        pedido.Peso = 73.3m;
        pedido.Altura = 171;
        pedido.Modalidades!.Add(new() { ModalidadeId = 2, DiasSemana = new[] { 1, 3 }, DuracaoMediaHoras = 1.25m });

        var r = Ok(await controller.Simular(pedido));

        var contexto = new ContextoDietaDto
        {
            Sexo = "Masculino", Peso = 73.3m, Altura = 171, Idade = 30, Objetivo = "perder peso",
            Modalidades = new()
            {
                new() { Nome = "Jiu-jitsu", MetReferencia = 7.8m, DuracaoHoras = 1.5m, DiasSemana = new[] { 1, 2, 4, 5 } },
                new() { Nome = "Musculação", MetReferencia = 3.5m, DuracaoHoras = 1.25m, DiasSemana = new[] { 1, 3 } }
            },
            Preferencias = new()
        };
        var exatas = CalculadoraManutencao.CaloriasBasePorDia(contexto, Calc);

        Assert.Equal(r.GastoRepouso.Total, r.GastoRepouso.Peso + r.GastoRepouso.Altura + r.GastoRepouso.Idade + r.GastoRepouso.Sexo);
        Assert.All(r.Dias, d =>
        {
            var treinoDoDia = r.Modalidades.Where(m => m.DiasSemana.Contains(d.DiaSemana)).Sum(m => m.KcalSessao);
            Assert.Equal(treinoDoDia, d.TreinoKcal);
            Assert.Equal(r.GastoRepouso.Total + r.AtividadeForaDoTreino.Kcal + d.TreinoKcal, d.Manutencao);
            Assert.Equal(d.Manutencao + d.ObjetivoKcal, d.MetaFormula);
            // Cada parte arredondada erra no máximo 0,5: com até 5 partes, a
            // soma fica a menos de 2,5 kcal da meta exata da dieta.
            Assert.InRange(d.MetaFormula - exatas[d.DiaSemana], -2.5m, 2.5m);
        });
    }

    [Fact]
    public async Task Sem_Nada_No_Pedido_Usa_Os_Dados_Salvos_E_O_Peso_De_Tendencia()
    {
        var (banco, controller, usuario) = Montar(u =>
        {
            u.Sexo = "Feminino"; u.Altura = 165; u.DataNascimento = Hoje.AddYears(-28);
        });
        banco.ObjetivosUsuario.AddRange(
            new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 70, Objetivo = "manter peso", DataRegistro = DateTime.UtcNow.AddDays(-10) },
            new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 68, Objetivo = "ganhar peso", DataRegistro = DateTime.UtcNow.AddDays(-1) });
        banco.UsuarioModalidades.Add(new UsuarioModalidade { UsuarioId = usuario.Id, ModalidadeId = 2, DiasSemana = new[] { 2, 4 }, DuracaoMediaHoras = 1 });
        banco.SaveChanges();

        var r = Ok(await controller.Simular(new SimulacaoMetaRequest()));

        var historico = banco.ObjetivosUsuario.OrderBy(o => o.DataRegistro)
            .Select(o => new RegistroPesoAderenciaDto { Data = o.DataRegistro, Peso = o.Peso, Objetivo = o.Objetivo }).ToList();
        Assert.Equal(ContextoDietaBuilder.PesoDeTendencia(historico, new CalculadoraPesoTendencia()), r.Entradas.Peso);
        Assert.Equal("tendencia", r.Entradas.PesoOrigem);
        Assert.Equal("ganhar peso", r.Entradas.Objetivo); // o do registro mais recente
        Assert.Equal("Feminino", r.Entradas.Sexo);
        Assert.Equal(-161, r.GastoRepouso.Sexo);
        Assert.Equal("Musculação", Assert.Single(r.Modalidades).Nome);
        Assert.True(r.Dias[1].TreinoKcal > 0);  // terça
        Assert.Equal(0, r.Dias[0].TreinoKcal);  // segunda
    }

    [Fact]
    public async Task Lista_Vazia_De_Modalidades_Substitui_A_Salva()
    {
        var (banco, controller, usuario) = Montar();
        banco.UsuarioModalidades.Add(new UsuarioModalidade { UsuarioId = usuario.Id, ModalidadeId = 1, DiasSemana = new[] { 1 }, DuracaoMediaHoras = 1 });
        banco.SaveChanges();
        var pedido = PedidoCompleto();
        pedido.Modalidades = new();

        var r = Ok(await controller.Simular(pedido));

        Assert.Empty(r.Modalidades);
        Assert.All(r.Dias, d => Assert.Equal(0, d.TreinoKcal));
    }

    [Theory]
    [InlineData("sexo", "masculino", "Sexo inválido.")]
    [InlineData("objetivo", "emagrecer", "Objetivo inválido.")]
    [InlineData("futuro", null, "A data de nascimento não pode ser no futuro.")]
    [InlineData("velho", null, "Informe uma data de nascimento de até 100 anos atrás.")]
    [InlineData("dia", null, "Os dias da semana devem estar entre 1 (Segunda) e 7 (Domingo).")]
    [InlineData("diaRepetido", null, "Não é possível repetir o mesmo dia da semana.")]
    [InlineData("modalidadeRepetida", null, "Não é possível repetir a mesma modalidade.")]
    public async Task Valor_Fora_Do_Permitido_Recusa_Com_400(string caso, string? valor, string mensagem)
    {
        var (_, controller, _) = Montar();
        var pedido = PedidoCompleto();
        switch (caso)
        {
            case "sexo": pedido.Sexo = valor; break;
            case "objetivo": pedido.Objetivo = valor; break;
            case "futuro": pedido.DataNascimento = Hoje.AddDays(1); break;
            case "velho": pedido.DataNascimento = Hoje.AddYears(-100).AddDays(-1); break;
            case "dia": pedido.Modalidades![0].DiasSemana = new[] { 0, 3 }; break;
            case "diaRepetido": pedido.Modalidades![0].DiasSemana = new[] { 3, 3 }; break;
            case "modalidadeRepetida": pedido.Modalidades!.Add(new() { ModalidadeId = 1, DiasSemana = new[] { 6 }, DuracaoMediaHoras = 1 }); break;
        }

        var resultado = await controller.Simular(pedido);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(mensagem, Mensagem(resultado));
    }

    [Fact]
    public async Task Cem_Anos_Exatos_Ainda_Vale()
    {
        var (_, controller, _) = Montar();
        var pedido = PedidoCompleto();
        pedido.DataNascimento = Hoje.AddYears(-100);

        Assert.Equal(100, Ok(await controller.Simular(pedido)).Entradas.Idade);
    }

    [Theory]
    [InlineData("sexo", "Informe seu sexo.")]
    [InlineData("altura", "Informe sua altura.")]
    [InlineData("nascimento", "Informe sua data de nascimento.")]
    [InlineData("peso", "Informe seu peso.")]
    [InlineData("objetivo", "Informe seu objetivo.")]
    public async Task Sem_O_Dado_No_Pedido_Nem_Salvo_Recusa_Com_400(string faltando, string mensagem)
    {
        var (_, controller, _) = Montar();
        var pedido = PedidoCompleto();
        switch (faltando)
        {
            case "sexo": pedido.Sexo = null; break;
            case "altura": pedido.Altura = null; break;
            case "nascimento": pedido.DataNascimento = null; break;
            case "peso": pedido.Peso = null; break;
            case "objetivo": pedido.Objetivo = null; break;
        }

        var resultado = await controller.Simular(pedido);

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal(mensagem, Mensagem(resultado));
    }

    [Fact]
    public async Task Modalidade_Que_Nao_Existe_Da_404()
    {
        var (_, controller, _) = Montar();
        var pedido = PedidoCompleto();
        pedido.Modalidades![0].ModalidadeId = 99;

        var resultado = await controller.Simular(pedido);

        Assert.IsType<NotFoundObjectResult>(resultado);
        Assert.Equal("Modalidade não encontrada.", Mensagem(resultado));
    }

    // Altura e peso fora da faixa: atributos do DTO (validação automática do
    // [ApiController], que devolve a mensagem pelo RespostaValidacao).
    [Theory]
    [InlineData(99, 80, "Informe uma altura entre 1,00 e 2,50 m.")]
    [InlineData(251, 80, "Informe uma altura entre 1,00 e 2,50 m.")]
    [InlineData(178, 19.9, "Informe um peso entre 20 e 300 kg.")]
    [InlineData(178, 300.1, "Informe um peso entre 20 e 300 kg.")]
    public void Altura_E_Peso_Fora_Da_Faixa_Dao_Mensagem_Em_Portugues(double altura, double peso, string mensagem)
    {
        var pedido = new SimulacaoMetaRequest { Altura = (decimal)altura, Peso = (decimal)peso };
        var resultados = new List<ValidationResult>();

        Validator.TryValidateObject(pedido, new ValidationContext(pedido), resultados, validateAllProperties: true);

        Assert.Equal(mensagem, Assert.Single(resultados).ErrorMessage);
    }

    [Fact]
    public void Limites_Da_Faixa_Valem()
    {
        foreach (var pedido in new[] { new SimulacaoMetaRequest { Altura = 100, Peso = 20 }, new SimulacaoMetaRequest { Altura = 250, Peso = 300 } })
        {
            Assert.True(Validator.TryValidateObject(pedido, new ValidationContext(pedido), new List<ValidationResult>(), validateAllProperties: true));
        }
    }
}
