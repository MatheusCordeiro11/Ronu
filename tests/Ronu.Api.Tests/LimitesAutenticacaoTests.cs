using System.Net;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Ronu.Api.Controllers;
using Ronu.Api.Services;

namespace Ronu.Api.Tests;

/// <summary>
/// Os limites por IP do login, do Google e do cadastro: os números, a contagem separada por IP e cada rota com a
/// sua política. Sem o atributo na ação, a política existiria e nunca seria aplicada.
/// </summary>
public class LimitesAutenticacaoTests
{
    private static HttpContext DoIp(string ip)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return http;
    }

    private static RateLimiter Limitador(RateLimitPartition<string> particao) => particao.Factory(particao.PartitionKey);

    [Fact]
    public void Numeros_Aprovados()
    {
        Assert.Equal(20, LimitesAutenticacao.TentativasEntradaPorJanela);
        Assert.Equal(TimeSpan.FromMinutes(15), LimitesAutenticacao.JanelaEntrada);
        Assert.Equal(5, LimitesAutenticacao.CadastrosPorJanela);
        Assert.Equal(TimeSpan.FromHours(1), LimitesAutenticacao.JanelaCadastro);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(5)]
    public void Recusa_A_Primeira_Acima_Do_Limite(int limite)
    {
        var limitador = Limitador(LimitesAutenticacao.PorIp(DoIp("203.0.113.7"), limite, TimeSpan.FromHours(1)));

        for (var i = 0; i < limite; i++)
        {
            using var permitida = limitador.AttemptAcquire();
            Assert.True(permitida.IsAcquired, $"a tentativa {i + 1} deveria passar");
        }

        using var recusada = limitador.AttemptAcquire();
        Assert.False(recusada.IsAcquired);
    }

    [Fact]
    public void Cada_IP_Tem_A_Propria_Contagem()
    {
        var janela = TimeSpan.FromMinutes(15);
        var deUm = LimitesAutenticacao.PorIp(DoIp("203.0.113.7"), 20, janela);
        var deOutro = LimitesAutenticacao.PorIp(DoIp("198.51.100.23"), 20, janela);

        Assert.Equal("203.0.113.7", deUm.PartitionKey);
        Assert.NotEqual(deUm.PartitionKey, deOutro.PartitionKey);
    }

    [Theory]
    [InlineData(nameof(AuthController.Login), LimitesAutenticacao.PoliticaLogin)]
    [InlineData(nameof(AuthController.LoginComGoogle), LimitesAutenticacao.PoliticaGoogle)]
    [InlineData(nameof(AuthController.Cadastro), LimitesAutenticacao.PoliticaCadastro)]
    public void Rota_Usa_A_Politica_Dela(string acao, string politica)
    {
        var atributo = typeof(AuthController).GetMethod(acao)!.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal(politica, atributo.PolicyName);
    }

    [Fact]
    public void Politicas_Estao_Registradas()
    {
        var opcoes = new RateLimiterOptions();

        LimitesRequisicoes.Configurar(opcoes);

        // Registrar duas vezes o mesmo nome lança exceção: se as três já estão lá, registrar de novo falha.
        foreach (var politica in new[] { LimitesAutenticacao.PoliticaLogin, LimitesAutenticacao.PoliticaGoogle, LimitesAutenticacao.PoliticaCadastro })
        {
            Assert.Throws<ArgumentException>(() => opcoes.AddPolicy(politica, http => RateLimitPartition.GetNoLimiter("x")));
        }
    }
}
