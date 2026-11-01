using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Ronu.Api.Services;

namespace Ronu.Api.Tests;

/// <summary>A resposta 429: o status e a mensagem de cada política, para a tela da troca não dizer "mais tarde".</summary>
public class LimitesRequisicoesTests
{
    private sealed class LeaseRecusada : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => [];
        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }

    private static async Task<(int Status, string? Mensagem)> Recusar(string? politica)
    {
        var opcoes = new RateLimiterOptions();
        LimitesRequisicoes.Configurar(opcoes);

        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        var metadados = politica is null ? new EndpointMetadataCollection() : new EndpointMetadataCollection(new EnableRateLimitingAttribute(politica));
        http.SetEndpoint(new Endpoint(null, metadados, "teste"));
        http.Response.StatusCode = opcoes.RejectionStatusCode;

        await opcoes.OnRejected!(new OnRejectedContext { HttpContext = http, Lease = new LeaseRecusada() }, CancellationToken.None);

        http.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(http.Response.Body);
        return (http.Response.StatusCode, json.RootElement.GetProperty("mensagem").GetString());
    }

    [Theory]
    [InlineData(LimitesTroca.PoliticaLeitura)]
    [InlineData(LimitesTroca.PoliticaEscrita)]
    public async Task Troca_Tem_A_Propria_Mensagem(string politica)
    {
        Assert.Equal((429, LimitesTroca.MensagemLimite), await Recusar(politica));
    }

    [Theory]
    [InlineData(LimitesRedefinicaoSenha.PoliticaPedido)]
    [InlineData(LimitesRedefinicaoSenha.PoliticaLink)]
    public async Task Redefinicao_De_Senha_Continua_Com_A_Mensagem_Dela(string politica)
    {
        Assert.Equal((429, LimitesRedefinicaoSenha.MensagemLimite), await Recusar(politica));
    }

    [Theory]
    [InlineData(LimitesAutenticacao.PoliticaLogin)]
    [InlineData(LimitesAutenticacao.PoliticaGoogle)]
    public async Task Login_E_Google_Dizem_Quanto_Esperar(string politica)
    {
        Assert.Equal((429, LimitesAutenticacao.MensagemEntrada), await Recusar(politica));
    }

    [Fact]
    public async Task Cadastro_Tem_A_Propria_Mensagem()
    {
        Assert.Equal((429, LimitesAutenticacao.MensagemCadastro), await Recusar(LimitesAutenticacao.PoliticaCadastro));
    }

    [Fact]
    public async Task Aceite_Do_Consentimento_Tem_A_Propria_Mensagem()
    {
        Assert.Equal((429, LimitesConsentimento.MensagemLimite), await Recusar(LimitesConsentimento.PoliticaAceite));
    }

    [Theory]
    [InlineData(LimitesConta.PoliticaExclusao, LimitesConta.MensagemExclusao)]
    [InlineData(LimitesConta.PoliticaExportacao, LimitesConta.MensagemExportacao)]
    public async Task Conta_Tem_As_Proprias_Mensagens(string politica, string mensagem)
    {
        Assert.Equal((429, mensagem), await Recusar(politica));
    }

    [Fact]
    public void Mensagens_Sao_Diferentes()
    {
        string[] mensagens =
        [
            LimitesRedefinicaoSenha.MensagemLimite, LimitesTroca.MensagemLimite,
            LimitesAutenticacao.MensagemEntrada, LimitesAutenticacao.MensagemCadastro,
            LimitesConsentimento.MensagemLimite, LimitesConta.MensagemExclusao, LimitesConta.MensagemExportacao
        ];
        Assert.Equal(mensagens.Length, mensagens.Distinct().Count());
    }
}
