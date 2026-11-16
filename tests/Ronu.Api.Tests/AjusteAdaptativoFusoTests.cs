using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Controllers;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Meta adaptativa no fuso da UF (lacuna "A meta adaptativa ainda conta os dias em UTC", resolvida em 2026-11-16). Os
/// dias do calendário (dieta ativa, dia da semana da meta, intervalo de cada pesagem, início da janela) passam a ser os
/// do fuso da UF; as diferenças entre instantes continuam em horas corridas.
///
/// Regressão: os testes antigos (AjusteAdaptativoTests, AjusteAdaptativoPorPeriodoTests, ProgressoAjusteTests) passam
/// sem mudança, porque o fuso nulo é o comportamento de antes. Aqui: com tudo ao meio-dia UTC, o fuso não muda nada; e
/// a mesma história contada em hora local dá, no fuso, exatamente o resultado que antes só saía quando ela caía nos
/// mesmos dias em UTC.
/// </summary>
public class AjusteAdaptativoFusoTests
{
    private static CalculadoraAjusteAdaptativo NovaCalculadora() => new(new CalculadoraPesoTendencia());

    private static readonly TimeZoneInfo Sp = FusoHorarioEstado.Fuso("SP");
    private static readonly TimeZoneInfo Ac = FusoHorarioEstado.Fuso("AC");

    // Metas diferentes em cada dia da semana: deslocar um dia muda a ingestão.
    private static readonly decimal[] MetasA = [2100, 2200, 2300, 2400, 2500, 2600, 2700];
    private static readonly decimal[] MetasB = [2000, 1900, 2050, 1950, 2150, 2250, 2350];
    private static readonly decimal[] Manutencao = [2500, 2500, 2500, 2500, 2500, 2500, 2500];

    // Uma história contada no relógio da parede: pesagens a cada 2 dias às 22h, dietas às 22h30 e às 23h, aderências
    // variadas. "converter" leva cada hora local ao instante que o cálculo recebe.
    private static AjusteAdaptativoDto Calcular(DateTime agoraLocal, Func<DateTime, DateTime> converter, TimeZoneInfo? fuso)
    {
        var aderencias = new[] { "seguiu", "comeu_mais", "comeu_menos", null, "seguiu" };
        var historico = new List<RegistroPesoAderenciaDto>();
        for (var i = 0; i <= 13; i++)
        {
            var quando = agoraLocal.Date.AddDays(-26 + 2 * i).AddHours(22);
            historico.Add(new()
            {
                Data = converter(quando), Peso = 84m - 0.12m * i, Objetivo = "perder peso", Aderencia = aderencias[i % aderencias.Length]
            });
        }

        var dietas = new List<MetaDietaRegistroDto>
        {
            new() { DataGeracao = converter(agoraLocal.Date.AddDays(-30).AddHours(22.5)), VersaoFormula = CalculadoraManutencao.VersaoFormula, MetasPorDia = MetasA, ManutencoesPorDia = Manutencao },
            new() { DataGeracao = converter(agoraLocal.Date.AddDays(-11).AddHours(23)), VersaoFormula = CalculadoraManutencao.VersaoFormula, MetasPorDia = MetasB, ManutencoesPorDia = Manutencao }
        };

        return NovaCalculadora().Calcular(historico, dietas, "perder peso", 2300m, Manutencao, converter(agoraLocal), CalculadoraManutencao.FatorPadrao, fuso);
    }

    private static string Json(AjusteAdaptativoDto dto) => JsonSerializer.Serialize(new
    {
        dto.Percentual, dto.Motivo, dto.PontosUsados, dto.FatorBruto, dto.ErroFormulaKcalDia, dto.IngestaoMediaDiaria,
        dto.ManutencaoMediaDiaria, dto.MetaPrescritaMediaDiaria, dto.DiasComDieta, dto.RitmoRealKgSemana, dto.RitmoEsperadoKgSemana
    });

    // Quarta-feira, 18/11/2026, 23h50 no relógio da parede.
    private static readonly DateTime AgoraLocal = new(2026, 11, 18, 23, 50, 0, DateTimeKind.Unspecified);

    [Theory]
    [InlineData("SP")]
    [InlineData("AC")]
    [InlineData("AM")]
    public void A_Historia_Local_No_Fuso_Da_O_Mesmo_Que_A_Mesma_Historia_Em_Utc_Sem_Fuso(string uf)
    {
        var fuso = FusoHorarioEstado.Fuso(uf);

        var noFuso = Calcular(AgoraLocal, local => TimeZoneInfo.ConvertTimeToUtc(local, fuso), fuso);
        var referencia = Calcular(AgoraLocal, local => DateTime.SpecifyKind(local, DateTimeKind.Utc), fuso: null);

        Assert.True(noFuso.PontosUsados >= CalculadoraAjusteAdaptativo.MinimoPontos);
        Assert.Equal(Json(referencia), Json(noFuso));
    }

    [Fact]
    public void Contando_Em_Utc_A_Mesma_Historia_Muda_A_Ingestao()
    {
        // O erro que a lacuna descrevia: as pesagens das 22h e as dietas da noite caem no dia seguinte.
        var noFuso = Calcular(AgoraLocal, local => TimeZoneInfo.ConvertTimeToUtc(local, Sp), Sp);
        var emUtc = Calcular(AgoraLocal, local => TimeZoneInfo.ConvertTimeToUtc(local, Sp), fuso: null);

        Assert.NotEqual(noFuso.IngestaoMediaDiaria, emUtc.IngestaoMediaDiaria);
    }

    [Fact]
    public void Tudo_Ao_Meio_Dia_Utc_O_Fuso_Nao_Muda_Nada()
    {
        // Meio-dia UTC é de manhã em todo o Brasil: o dia é o mesmo. É o caso dos testes antigos.
        var meioDia = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        Func<DateTime, DateTime> aoMeioDia = local => DateTime.SpecifyKind(local.Date.AddHours(12), DateTimeKind.Utc);

        var semFuso = Calcular(meioDia, aoMeioDia, fuso: null);

        Assert.Equal(Json(semFuso), Json(Calcular(meioDia, aoMeioDia, Sp)));
        Assert.Equal(Json(semFuso), Json(Calcular(meioDia, aoMeioDia, Ac)));
    }

    [Fact]
    public void Dia_Local_De_Uma_Pesagem_As_22h_Em_Brasilia()
    {
        var pesagem = new DateTime(2026, 11, 17, 1, 0, 0, DateTimeKind.Utc); // 22h de 16/11 em Brasília

        Assert.Equal(new DateTime(2026, 11, 16), CalculadoraAjusteAdaptativo.DiaLocal(pesagem, Sp));
        Assert.Equal(new DateTime(2026, 11, 17), CalculadoraAjusteAdaptativo.DiaLocal(pesagem, fuso: null));
    }

    [Fact]
    public void Pesagem_As_22h_Fecha_O_Intervalo_Do_Dia_Local_Dela()
    {
        // Duas pesagens no mesmo dia UTC (02/11) e em dias locais diferentes: 22h de 01/11 ("comi um pouco mais") e 17h de
        // 02/11 ("segui") em Brasília. No calendário local, o dia 01/11 é fechado pela pesagem de 02/11 (a das 22h é do
        // próprio dia 01/11, e o peso de um dia reflete o que se comeu antes). Contando em UTC, a das 22h já era de 02/11
        // e fechava o dia 01/11 com 1,15.
        var dietas = new List<MetaDietaRegistroDto>
        {
            new() { DataGeracao = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc), VersaoFormula = CalculadoraManutencao.VersaoFormula, MetasPorDia = MetasA, ManutencoesPorDia = Manutencao }
        };
        var historico = new List<RegistroPesoAderenciaDto>
        {
            new() { Data = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc), Peso = 82, Objetivo = "perder peso", Aderencia = "seguiu" },
            new() { Data = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc), Peso = 81.8m, Objetivo = "perder peso", Aderencia = "seguiu" },
            new() { Data = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc), Peso = 81.6m, Objetivo = "perder peso", Aderencia = "seguiu" },
            new() { Data = new DateTime(2026, 11, 2, 1, 0, 0, DateTimeKind.Utc), Peso = 81.4m, Objetivo = "perder peso", Aderencia = "comeu_mais" },
            new() { Data = new DateTime(2026, 11, 2, 20, 0, 0, DateTimeKind.Utc), Peso = 81.3m, Objetivo = "perder peso", Aderencia = "seguiu" }
        };
        var agora = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        var noFuso = NovaCalculadora().Calcular(historico, dietas, "perder peso", 2300m, Manutencao, agora, CalculadoraManutencao.FatorPadrao, Sp);
        var emUtc = NovaCalculadora().Calcular(historico, dietas, "perder peso", 2300m, Manutencao, agora, CalculadoraManutencao.FatorPadrao);

        // De 18/10 a 01/11: 15 dias nos dois. 01/11 é domingo (meta 2.700): em UTC, ele vale 1,15.
        Assert.Equal(15, noFuso.DiasComDieta);
        Assert.Equal(15, emUtc.DiasComDieta);
        Assert.Equal(Math.Round(0.15m * 2700m / 15m, 1), emUtc.IngestaoMediaDiaria!.Value - noFuso.IngestaoMediaDiaria!.Value, 1);
    }

    [Fact]
    public void A_Janela_Comeca_Na_Meia_Noite_Local_Do_Dia_Da_Dieta()
    {
        // Dieta gerada às 23h de 10/11 em Brasília (02h UTC de 11/11). Uma pesagem às 20h de 10/11 (23h UTC de 10/11)
        // é do mesmo dia local da dieta e entra; contando em UTC, ela era de um dia antes da dieta e ficava de fora.
        var dieta = new DateTime(2026, 11, 11, 2, 0, 0, DateTimeKind.Utc);
        var agora = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
        var historico = new[]
        {
            new RegistroPesoAderenciaDto { Data = new DateTime(2026, 11, 10, 23, 0, 0, DateTimeKind.Utc), Peso = 80, Objetivo = "perder peso", Aderencia = "seguiu" },
            new RegistroPesoAderenciaDto { Data = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc), Peso = 80, Objetivo = "perder peso", Aderencia = "seguiu" }
        };

        Assert.Equal(2, CalculadoraAjusteAdaptativo.ResumirJanela(historico, dieta, "perder peso", agora, Sp).Pesagens);
        Assert.Equal(1, CalculadoraAjusteAdaptativo.ResumirJanela(historico, dieta, "perder peso", agora).Pesagens);
    }

    // ---------- Quem chama passa o fuso da UF ----------

    private sealed class AjusteEspiao : ICalculadoraAjusteAdaptativo
    {
        public TimeZoneInfo? Fuso { get; private set; }
        public bool Chamado { get; private set; }

        public AjusteAdaptativoDto Calcular(
            IReadOnlyList<RegistroPesoAderenciaDto> historico, IReadOnlyList<MetaDietaRegistroDto> metasDietas, string objetivoAtual,
            decimal metaBaseMediaDiaria, IReadOnlyList<decimal> manutencaoAtualPorDia, DateTime agoraUtc,
            decimal fatorAtividadeAtual = CalculadoraManutencao.FatorPadrao, TimeZoneInfo? fuso = null)
        {
            Chamado = true;
            Fuso = fuso;
            return new() { Percentual = 0m, Motivo = MotivoAjusteAdaptativo.HistoricoInsuficiente };
        }
    }

    private sealed class SemRede : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    [Theory]
    [InlineData("AC", -5)]
    [InlineData("MT", -4)]
    [InlineData("SP", -3)]
    [InlineData("", -3)]
    public void A_Geracao_Passa_O_Fuso_Da_Uf_Para_A_Meta_Adaptativa(string uf, int horasUtc)
    {
        var espiao = new AjusteEspiao();
        var gerador = new GeradorDietaGemini(
            new HttpClient(new SemRede()), new CalculadoraGastoCalorico(), espiao,
            new GeminiOptions { ApiKey = "teste" }, NullLogger<GeradorDietaGemini>.Instance);
        var contexto = new ContextoDietaDto
        {
            Altura = 175, Sexo = "Masculino", Idade = 30, Peso = 80, Objetivo = "manter peso", Estado = uf,
            Modalidades = new(), Preferencias = new(),
            HistoricoPeso = new() { new() { Data = DateTime.UtcNow, Peso = 80, Objetivo = "manter peso" } }
        };

        gerador.CalcularMetas(contexto);

        Assert.True(espiao.Chamado);
        Assert.NotNull(espiao.Fuso);
        Assert.Equal(TimeSpan.FromHours(horasUtc), espiao.Fuso!.GetUtcOffset(DateTime.UtcNow));
    }

    [Fact]
    public async Task O_Resumo_Do_Progresso_Conta_No_Fuso_Da_Uf()
    {
        // A mesma situação do teste da janela, no relógio de agora: dieta às 23h de um dia em Brasília e uma pesagem às
        // 20h do mesmo dia. No fuso, a pesagem entra.
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Ana", Email = "fuso-progresso@exemplo.test", SenhaHash = "hash", Estado = "SP" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        var diaDaDieta = FusoHorarioEstado.Hoje("SP", DateTime.UtcNow).AddDays(-10).ToDateTime(TimeOnly.MinValue);
        DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(local, Sp);
        banco.MetasDieta.Add(new MetaDieta { UsuarioId = usuario.Id, DataGeracao = Utc(diaDaDieta.AddHours(23)), MetasPorDia = new decimal[7] });
        foreach (var horas in new[] { 20, 3 * 24 + 12, 5 * 24 + 12, 7 * 24 + 12, 9 * 24 + 12 })
        {
            banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 80, Objetivo = "perder peso", DataRegistro = Utc(diaDaDieta.AddHours(horas)), Aderencia = "seguiu" });
        }
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

        Assert.Equal(5, resposta.ProgressoAjuste!.Pesagens);
    }
}
