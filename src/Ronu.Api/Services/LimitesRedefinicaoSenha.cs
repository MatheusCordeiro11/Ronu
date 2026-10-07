using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limite por IP das rotas de redefinição de senha (o limite por conta fica no
/// ServicoRedefinicaoSenha, contado no banco). Em memória: zera quando o App
/// Service reinicia, o que é aceitável para um limite de defesa. O IP é o do
/// cliente graças aos forwarded headers (Program.cs); sem eles, atrás do proxy
/// do App Service, todos teriam o mesmo IP.
/// </summary>
public static class LimitesRedefinicaoSenha
{
    public const string PoliticaPedido = "redefinicao-senha-pedido";
    public const string PoliticaLink = "redefinicao-senha-link";

    public const int PedidosPorHoraPorIp = 10;
    public const int TentativasPorHoraPorIp = 20;

    public const string MensagemLimite = "Muitas tentativas. Tente de novo mais tarde.";

    public static void Configurar(RateLimiterOptions opcoes)
    {
        opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opcoes.OnRejected = (contexto, cancelamento) =>
            new ValueTask(contexto.HttpContext.Response.WriteAsJsonAsync(new { mensagem = MensagemLimite }, cancelamento));

        opcoes.AddPolicy(PoliticaPedido, http => PorIp(http, PedidosPorHoraPorIp));
        // Verificar e redefinir dividem o mesmo limite.
        opcoes.AddPolicy(PoliticaLink, http => PorIp(http, TentativasPorHoraPorIp));
    }

    private static RateLimitPartition<string> PorIp(HttpContext http, int limitePorHora) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limitePorHora,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            });
}
