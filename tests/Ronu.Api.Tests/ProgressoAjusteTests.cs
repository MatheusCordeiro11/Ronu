using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ronu.Api.Controllers;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Resumo da janela da meta adaptativa no GET /objetivos/atual (card "Ajuste
/// pelo progresso" do Progresso). A contagem tem de bater com a da geração da
/// dieta, porque as duas usam CalculadoraAjusteAdaptativo.IndicesDaJanela.
/// </summary>
public class ProgressoAjusteTests
{
    private static readonly DateTime Agora = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private static RegistroPesoAderenciaDto Registro(double diasAtras, decimal peso = 80, string objetivo = "perder peso", string? aderencia = "seguiu") =>
        new() { Data = Agora.AddDays(-diasAtras), Peso = peso, Objetivo = objetivo, Aderencia = aderencia };

    [Fact]
    public void Sem_Dieta_Nada_Conta()
    {
        var historico = new[] { Registro(20), Registro(15), Registro(10), Registro(5), Registro(0) };

        var resumo = CalculadoraAjusteAdaptativo.ResumirJanela(historico, inicioDaJanela: null, "perder peso", Agora);

        Assert.Equal((0, 0, false), (resumo.Pesagens, resumo.DiasCobertos, resumo.Suficiente));
    }

    [Fact]
    public void Conta_Igual_A_Geracao_Da_Dieta()
    {
        // Uma pesagem a cada 3 dias por 27 dias, caindo devagar.
        var historico = Enumerable.Range(0, 10).Select(i => Registro(27 - i * 3, 81 - i * 0.1m)).ToList();
        var dieta = new MetaDietaRegistroDto
        {
            DataGeracao = Agora.AddDays(-60),
            VersaoFormula = CalculadoraManutencao.VersaoFormula,
            MetasPorDia = Enumerable.Repeat(2300m, 7).ToArray(),
            ManutencoesPorDia = Enumerable.Repeat(2700m, 7).ToArray()
        };

        var ajuste = new CalculadoraAjusteAdaptativo(new CalculadoraPesoTendencia())
            .Calcular(historico, new[] { dieta }, "perder peso", 2300m, Enumerable.Repeat(2700m, 7).ToArray(), Agora);
        var resumo = CalculadoraAjusteAdaptativo.ResumirJanela(historico, dieta.DataGeracao, "perder peso", Agora);

        Assert.Equal(ajuste.PontosUsados, resumo.Pesagens);
        Assert.NotEqual(MotivoAjusteAdaptativo.HistoricoInsuficiente, ajuste.Motivo);
        Assert.True(resumo.Suficiente);
        Assert.Equal(27, resumo.DiasCobertos);
    }

    [Fact]
    public void Fora_Da_Janela_Nao_Conta()
    {
        var historico = new List<RegistroPesoAderenciaDto>
        {
            Registro(40),                              // antes dos 28 dias
            Registro(25, objetivo: "manter peso"),     // outro objetivo, antes da troca
            Registro(20),
            Registro(15, aderencia: "nao_seguiu"),     // descartado
            Registro(10, aderencia: null),             // sem resposta conta
            Registro(5)
        };

        var resumo = CalculadoraAjusteAdaptativo.ResumirJanela(historico, Agora.AddDays(-90), "perder peso", Agora);

        Assert.Equal((3, 15, false), (resumo.Pesagens, resumo.DiasCobertos, resumo.Suficiente));
    }

    [Fact]
    public void Janela_Comeca_Na_Primeira_Dieta()
    {
        var historico = new[] { Registro(20), Registro(12), Registro(8), Registro(4), Registro(0) };

        // Primeira dieta há 10 dias: as pesagens de 20 e 12 dias atrás ficam de fora.
        var resumo = CalculadoraAjusteAdaptativo.ResumirJanela(historico, Agora.AddDays(-10), "perder peso", Agora);

        Assert.Equal((3, 8), (resumo.Pesagens, resumo.DiasCobertos));
    }

    [Fact]
    public void Dias_Arredondam_Para_Baixo_E_Treze_E_Meio_Nao_Basta()
    {
        var historico = new[] { Registro(13.5), Registro(10), Registro(7), Registro(3), Registro(0) };

        var resumo = CalculadoraAjusteAdaptativo.ResumirJanela(historico, Agora.AddDays(-60), "perder peso", Agora);

        Assert.Equal((5, 13, false), (resumo.Pesagens, resumo.DiasCobertos, resumo.Suficiente));
        Assert.Equal((5, 14, 28), (resumo.MinimoPesagens, resumo.MinimoDias, resumo.JanelaDias));
    }

    [Fact]
    public async Task Objetivo_Atual_Usa_A_Primeira_Dieta_De_MetasDieta()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Ana", Email = "ana@exemplo.com", SenhaHash = "hash" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        var agora = DateTime.UtcNow;
        foreach (var dias in new[] { 20, 16, 12, 8, 4 })
        {
            banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 80, Objetivo = "perder peso", DataRegistro = agora.AddDays(-dias), Aderencia = "seguiu" });
        }
        // MetasDieta guarda todas as dietas; a primeira foi há 30 dias.
        banco.MetasDieta.Add(new MetaDieta { UsuarioId = usuario.Id, DataGeracao = agora.AddDays(-30), MetasPorDia = new decimal[7] });
        banco.MetasDieta.Add(new MetaDieta { UsuarioId = usuario.Id, DataGeracao = agora.AddDays(-2), MetasPorDia = new decimal[7] });
        banco.SaveChanges();
        var controller = new ObjetivosController(banco, new CalculadoraPesoTendencia())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()) }, "teste"))
                }
            }
        };

        var resposta = Assert.IsType<ObjetivoAtualResponse>(Assert.IsType<OkObjectResult>(await controller.ObterAtual()).Value);

        Assert.NotNull(resposta.ProgressoAjuste);
        Assert.Equal((5, 16, true), (resposta.ProgressoAjuste!.Pesagens, resposta.ProgressoAjuste.DiasCobertos, resposta.ProgressoAjuste.Suficiente));
    }
}
