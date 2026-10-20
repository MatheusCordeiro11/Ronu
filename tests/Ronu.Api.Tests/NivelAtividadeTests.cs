using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;
using Ronu.Api.Validacao;

namespace Ronu.Api.Tests;

/// <summary>
/// Nível de atividade do dia a dia: fatores, perfil, simulação, composição da
/// meta e o recomeço da janela da meta adaptativa quando o fator muda.
/// </summary>
public class NivelAtividadeTests
{
    private static readonly CalculadoraGastoCalorico Calc = new();
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);

    // ---------- Fatores ----------

    [Theory]
    [InlineData("sentado", 1.30)]
    [InlineData("em_pe", 1.45)]
    [InlineData("andando", 1.60)]
    [InlineData("bracal", 1.75)]
    public void Fator_Por_Nivel(string nivel, double esperado) =>
        Assert.Equal((decimal)esperado, CalculadoraManutencao.FatorDoNivel(nivel));

    [Fact]
    public void Sem_Resposta_O_Fator_E_O_De_Antes()
    {
        Assert.Equal(1.4m, CalculadoraManutencao.FatorDoNivel(null));
        Assert.Equal(1.4m, CalculadoraManutencao.FatorPadrao);
    }

    [Fact]
    public void Os_Niveis_Aceitos_Sao_Os_Quatro_Da_Tabela_De_Fatores()
    {
        Assert.Equal(new[] { "andando", "bracal", "em_pe", "sentado" }, RegrasDadosCorporais.NiveisAtividade.Order());
        Assert.Equal(4, CalculadoraManutencao.FatoresPorNivel.Count);
    }

    [Fact]
    public void Versao_Da_Formula_E_3()
    {
        Assert.Equal(3, CalculadoraManutencao.VersaoFormula);
    }

    [Fact]
    public void Composicao_Carrega_O_Nivel_E_O_Fator_E_Muda_A_Manutencao()
    {
        ContextoDietaDto Contexto(string? nivel) => new()
        {
            Sexo = "Masculino", Peso = 80, Altura = 178, Idade = 30, Objetivo = "manter peso",
            Modalidades = new(), Preferencias = new(), NivelAtividade = nivel
        };

        var sem = CalculadoraManutencao.ComposicaoPorDia(Contexto(null), Calc)[0];
        var sentado = CalculadoraManutencao.ComposicaoPorDia(Contexto("sentado"), Calc)[0];

        Assert.Equal((null, 1.4m), (sem.NivelAtividade, sem.FatorAtividade));
        Assert.Equal(("sentado", 1.30m), (sentado.NivelAtividade, sentado.FatorAtividade));
        Assert.Equal(sem.GastoRepouso * 0.4m, sem.AtividadeForaDoTreino);
        Assert.Equal(sentado.GastoRepouso * 0.3m, sentado.AtividadeForaDoTreino);
        Assert.Equal(sentado.GastoRepouso * 1.3m, sentado.Manutencao);
    }

    [Fact]
    public void Composicao_Salva_Guarda_O_Nivel_E_O_Fator()
    {
        var dia = new ComposicaoFormulaDia(1, 1768m, 530.4m, Array.Empty<TreinoDoDia>(), 2298.4m, 0m, false, "sentado", 1.30m);

        var c = ComposicaoMeta.Montar(dia, 0m, 2298.4m);

        Assert.Equal(("sentado", 1.30m), (c.NivelAtividade, c.FatorAtividade));
        Assert.Equal(530, c.AtividadeForaDoTreino);
    }

    [Fact]
    public void MetaDieta_Grava_O_Fator_Da_Dieta_E_1_4_Quando_Falta()
    {
        DietaSemanalDto Dieta(decimal? fator) => new()
        {
            Dias = GeradorDietaGemini.NomesDias.Select(n => new DiaDietaDto
            {
                DiaSemana = n, Refeicoes = new(),
                TotalDoDia = new MacrosDto { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 },
                MetaCalculada = new MacrosDto { Calorias = 2000, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 }
            }).ToList(),
            FatorAtividade = fator
        };

        Assert.Equal(1.30m, RepositorioDietaIA.MontarMetaDieta(1, DateTime.UtcNow, Dieta(1.30m)).FatorAtividade);
        Assert.Equal(1.4m, RepositorioDietaIA.MontarMetaDieta(1, DateTime.UtcNow, Dieta(null)).FatorAtividade);
        Assert.Equal(3, RepositorioDietaIA.MontarMetaDieta(1, DateTime.UtcNow, Dieta(null)).VersaoFormula);
    }

    // ---------- Perfil ----------

    private static (ApplicationDbContext Banco, PerfilController Controller, Usuario Usuario) MontarPerfil(string? nivel = null)
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Teste", Email = "teste@exemplo.com", Estado = "MG", NivelAtividade = nivel };
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
        return (banco, controller, usuario);
    }

    private static PerfilRequest PedidoPerfil(string? nivel) => new()
    {
        Altura = 178, Sexo = "Masculino", DataNascimento = Hoje.AddYears(-30).AddDays(-1), NivelAtividade = nivel
    };

    private static string? Mensagem(IActionResult resultado)
    {
        var valor = ((ObjectResult)resultado).Value!;
        return valor.GetType().GetProperty("mensagem")?.GetValue(valor) as string;
    }

    [Theory]
    [InlineData("sentado")]
    [InlineData("em_pe")]
    [InlineData("andando")]
    [InlineData("bracal")]
    public async Task Perfil_Grava_E_Devolve_Cada_Nivel(string nivel)
    {
        var (banco, controller, usuario) = MontarPerfil();

        var resposta = Assert.IsType<PerfilResponse>(Assert.IsType<OkObjectResult>(await controller.Atualizar(PedidoPerfil(nivel))).Value);

        Assert.Equal(nivel, resposta.NivelAtividade);
        Assert.Equal(nivel, banco.Usuarios.Single(u => u.Id == usuario.Id).NivelAtividade);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Perfil_Com_Nivel_Ausente_Ou_Vazio_Nao_Altera(string? ausente)
    {
        var (banco, controller, usuario) = MontarPerfil("andando");

        var resposta = Assert.IsType<PerfilResponse>(Assert.IsType<OkObjectResult>(await controller.Atualizar(PedidoPerfil(ausente))).Value);

        Assert.Equal("andando", resposta.NivelAtividade);
        Assert.Equal("andando", banco.Usuarios.Single(u => u.Id == usuario.Id).NivelAtividade);
    }

    [Fact]
    public async Task Perfil_Sem_Resposta_Continua_Sem_Resposta()
    {
        var (_, controller, _) = MontarPerfil();

        var resposta = Assert.IsType<PerfilResponse>(Assert.IsType<OkObjectResult>(await controller.Atualizar(PedidoPerfil(null))).Value);

        Assert.Null(resposta.NivelAtividade);
    }

    [Theory]
    [InlineData("Sentado")]
    [InlineData("sedentario")]
    [InlineData("1,3")]
    public async Task Perfil_Com_Nivel_Invalido_Da_400_E_Nao_Grava(string nivel)
    {
        var (banco, controller, usuario) = MontarPerfil("sentado");

        var resultado = await controller.Atualizar(PedidoPerfil(nivel));

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal("Nível de atividade inválido.", Mensagem(resultado));
        Assert.Equal("sentado", banco.Usuarios.Single(u => u.Id == usuario.Id).NivelAtividade);
    }

    // ---------- Simulação ----------

    private static (MetasController Controller, Usuario Usuario) MontarSimulacao(string? nivelSalvo = null)
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Teste", Email = "teste@exemplo.com", Estado = "MG", NivelAtividade = nivelSalvo };
        banco.Usuarios.Add(usuario);
        banco.Modalidades.Add(new Modalidade { Id = 1, Nome = "Jiu-jitsu", MetReferencia = 7.8m, Categoria = CategoriasModalidade.Lutas });
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
        return (controller, usuario);
    }

    private static SimulacaoMetaRequest PedidoSimulacao(string? nivel) => new()
    {
        Sexo = "Masculino", Altura = 178, DataNascimento = Hoje.AddYears(-30).AddDays(-1), Peso = 80, Objetivo = "perder peso",
        NivelAtividade = nivel,
        Modalidades = new() { new() { ModalidadeId = 1, DiasSemana = new[] { 1, 2, 4, 5 }, DuracaoMediaHoras = 1.5m } }
    };

    private static SimulacaoMetaResponse Ok(IActionResult r) =>
        Assert.IsType<SimulacaoMetaResponse>(Assert.IsType<OkObjectResult>(r).Value);

    // Perfil A (TMB 1768): o dia a dia vale TMB × (fator − 1).
    [Theory]
    [InlineData("sentado", 1.30, 530)]
    [InlineData("em_pe", 1.45, 795)]
    [InlineData("andando", 1.60, 1061)]
    [InlineData("bracal", 1.75, 1326)]
    public async Task Simulacao_Por_Nivel_No_Perfil_A(string nivel, double fator, int kcal)
    {
        var (controller, _) = MontarSimulacao();

        var r = Ok(await controller.Simular(PedidoSimulacao(nivel)));

        Assert.Equal((nivel, (decimal)fator, kcal), (r.AtividadeForaDoTreino.Nivel, r.AtividadeForaDoTreino.Fator, r.AtividadeForaDoTreino.Kcal));
        Assert.All(r.Dias, d => Assert.Equal(r.GastoRepouso.Total + kcal + d.TreinoKcal, d.Manutencao));
    }

    [Fact]
    public async Task Simulacao_Sem_Nivel_No_Pedido_Nem_Salvo_Fica_Como_Antes()
    {
        var (controller, _) = MontarSimulacao();

        var r = Ok(await controller.Simular(PedidoSimulacao(null)));

        Assert.Null(r.AtividadeForaDoTreino.Nivel);
        Assert.Equal((1.4m, 707), (r.AtividadeForaDoTreino.Fator, r.AtividadeForaDoTreino.Kcal));
        Assert.Equal(2851, r.Dias[0].MetaFormula);
    }

    [Fact]
    public async Task Simulacao_Usa_O_Nivel_Salvo_E_O_Do_Pedido_Vence()
    {
        var (controller, _) = MontarSimulacao("em_pe");

        var salvo = Ok(await controller.Simular(PedidoSimulacao(null)));
        var pedido = Ok(await controller.Simular(PedidoSimulacao("bracal")));

        Assert.Equal(("em_pe", 795), (salvo.AtividadeForaDoTreino.Nivel, salvo.AtividadeForaDoTreino.Kcal));
        Assert.Equal(("bracal", 1326), (pedido.AtividadeForaDoTreino.Nivel, pedido.AtividadeForaDoTreino.Kcal));
    }

    [Fact]
    public async Task Simulacao_Com_Nivel_Invalido_Da_400()
    {
        var (controller, _) = MontarSimulacao();

        var resultado = await controller.Simular(PedidoSimulacao("sedentario"));

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal("Nível de atividade inválido.", Mensagem(resultado));
    }

    // ---------- Janela da meta adaptativa ----------

    private static readonly DateTime Agora = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Inicio_Da_Janela_E_A_Primeira_Dieta_Da_Sequencia_Final_Com_O_Fator_Atual()
    {
        var dietas = new List<(DateTime, decimal)>
        {
            (Agora.AddDays(-60), 1.4m), (Agora.AddDays(-40), 1.3m), (Agora.AddDays(-30), 1.4m),
            (Agora.AddDays(-20), 1.4m), (Agora.AddDays(-10), 1.4m)
        };

        Assert.Equal(Agora.AddDays(-30), CalculadoraAjusteAdaptativo.InicioDaJanela(dietas, 1.4m));
        Assert.Null(CalculadoraAjusteAdaptativo.InicioDaJanela(dietas, 1.3m)); // nível trocado, dieta nova ainda não gerada
        Assert.Null(CalculadoraAjusteAdaptativo.InicioDaJanela(new List<(DateTime, decimal)>(), 1.4m));
    }

    // A pessoa come a meta da dieta ativa e gasta "gastoReal" por dia; pesa a
    // cada 2 dias. Devolve o histórico para a meta adaptativa.
    private static List<RegistroPesoAderenciaDto> Historico(int dias, decimal meta, decimal gastoReal)
    {
        var peso = 85.0;
        var historico = new List<RegistroPesoAderenciaDto>();
        for (var dia = -dias; dia <= 0; dia++)
        {
            if ((dia + dias) % 2 == 0 || dia == 0)
            {
                historico.Add(new() { Data = Agora.AddDays(dia), Peso = (decimal)Math.Round(peso, 4), Objetivo = "perder peso", Aderencia = "seguiu" });
            }
            peso += ((double)meta - (double)gastoReal) / 7700;
        }
        return historico;
    }

    private static MetaDietaRegistroDto Dieta(int dia, decimal meta, decimal manutencao, decimal fator) => new()
    {
        DataGeracao = Agora.AddDays(dia),
        VersaoFormula = CalculadoraManutencao.VersaoFormula,
        MetasPorDia = Enumerable.Repeat(meta, 7).ToArray(),
        ManutencoesPorDia = Enumerable.Repeat(manutencao, 7).ToArray(),
        FatorAtividade = fator
    };

    private static CalculadoraAjusteAdaptativo NovaCalculadora() => new(new CalculadoraPesoTendencia());

    // O gasto real da pessoa é o que a fórmula nova (fator 1,3) calcula: 2850.
    // A dieta antiga (fator 1,4) calculou 3000. O erro "−150" é só a mudança do
    // fator: se a janela não recomeçasse, a meta adaptativa corrigiria em dobro.
    [Fact]
    public void Trocar_O_Fator_Recomeca_A_Janela_E_Nao_Corrige_Em_Dobro()
    {
        var historico = Historico(40, meta: 2500m, gastoReal: 2850m);
        var dietaAntiga = Dieta(-40, meta: 2500m, manutencao: 3000m, fator: 1.4m);
        var manutencaoNova = Enumerable.Repeat(2850m, 7).ToArray();

        // Sem recomeçar (comportamento antigo, fator "atual" igual ao da dieta): corrige.
        var semRecomeco = NovaCalculadora().Calcular(historico, new[] { dietaAntiga }, "perder peso", 2500m, manutencaoNova, Agora, 1.4m);
        // Nível trocado, nenhuma dieta com o fator novo ainda: fórmula pura.
        var comRecomeco = NovaCalculadora().Calcular(historico, new[] { dietaAntiga }, "perder peso", 2500m, manutencaoNova, Agora, 1.3m);

        Assert.True(semRecomeco.Percentual < -0.02m, $"sem recomeço {semRecomeco.Percentual}");
        Assert.Equal(0m, comRecomeco.Percentual);
        Assert.Equal(MotivoAjusteAdaptativo.HistoricoInsuficiente, comRecomeco.Motivo);
        Assert.Equal(0, comRecomeco.PontosUsados);
    }

    [Fact]
    public void Depois_Da_Dieta_Nova_A_Janela_Conta_So_A_Partir_Dela_E_Acerta()
    {
        var historico = Historico(40, meta: 2500m, gastoReal: 2850m);
        var dietas = new[]
        {
            Dieta(-40, meta: 2500m, manutencao: 3000m, fator: 1.4m),
            Dieta(-20, meta: 2500m, manutencao: 2850m, fator: 1.3m)
        };

        var r = NovaCalculadora().Calcular(historico, dietas, "perder peso", 2500m, Enumerable.Repeat(2850m, 7).ToArray(), Agora, 1.3m);

        Assert.Equal(0m, r.Percentual);
        Assert.Equal(MotivoAjusteAdaptativo.DentroDoEsperado, r.Motivo);
        Assert.Equal(11, r.PontosUsados); // pesagens dos últimos 20 dias, a cada 2 dias
    }

    [Fact]
    public void Resumo_Da_Janela_Fica_Vazio_Quando_O_Nivel_Foi_Trocado()
    {
        var historico = Historico(40, meta: 2500m, gastoReal: 2850m);

        var resumo = CalculadoraAjusteAdaptativo.ResumirJanela(historico, null, "perder peso", Agora);

        Assert.Equal((0, 0, false), (resumo.Pesagens, resumo.DiasCobertos, resumo.Suficiente));
    }
}
