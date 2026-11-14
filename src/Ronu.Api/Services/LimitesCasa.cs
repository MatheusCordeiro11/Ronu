using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limites das rotas da casa (plano família). Em memória, como os outros: zera quando o App Service reinicia.
/// - Gerar convite, por usuário: cada link novo é mais um token válido por 7 dias circulando.
/// - Verificar e aceitar convite, por IP: são as rotas que recebem um token, e o limite por IP atrapalha quem tenta
///   adivinhar tokens com várias contas. As duas dividem a mesma contagem, de propósito (o fluxo normal usa 2).
/// - Escrita (criar, orçamento, membros, sair, transferência), por usuário: contra um cliente em laço.
/// - Pedidos (Pedir nova semana / Pedir troca, etapa 5), por usuário: o pedido não guarda quem pediu, então este
///   limite, que fica só na memória e nunca vai ao banco, é o que segura um membro pedindo sem parar.
/// </summary>
public static class LimitesCasa
{
    public const string PoliticaConvite = "casa-convite";
    public const string PoliticaConviteVerificar = "casa-convite-verificar";
    public const string PoliticaEscrita = "casa-escrita";
    public const string PoliticaPedido = "casa-pedido";

    public const int ConvitesPorJanela = 10;
    public static readonly TimeSpan JanelaConvite = TimeSpan.FromHours(1);

    public const int VerificacoesPorJanela = 20;
    public static readonly TimeSpan JanelaVerificacao = TimeSpan.FromHours(1);

    public const int EscritasPorJanela = 30;
    public static readonly TimeSpan JanelaEscrita = TimeSpan.FromMinutes(1);

    public const int PedidosPorJanela = 10;
    public static readonly TimeSpan JanelaPedido = TimeSpan.FromHours(1);

    public const string MensagemConvite = "Você já gerou muitos convites na última hora. Tente de novo mais tarde.";
    public const string MensagemConviteVerificar = "Muitas tentativas de abrir convites a partir desta conexão. Tente de novo em até uma hora.";
    public const string MensagemEscrita = "Muitas mudanças na casa em pouco tempo. Espere um minuto e tente de novo.";
    public const string MensagemPedido = "Você já fez muitos pedidos na última hora. Tente de novo mais tarde.";

    public static void Configurar(RateLimiterOptions opcoes)
    {
        opcoes.AddPolicy(PoliticaConvite, http => PorUsuario(http, ConvitesPorJanela, JanelaConvite));
        opcoes.AddPolicy(PoliticaConviteVerificar, http => LimitesAutenticacao.PorIp(http, VerificacoesPorJanela, JanelaVerificacao));
        opcoes.AddPolicy(PoliticaEscrita, http => PorUsuario(http, EscritasPorJanela, JanelaEscrita));
        opcoes.AddPolicy(PoliticaPedido, http => PorUsuario(http, PedidosPorJanela, JanelaPedido));
    }

    private static RateLimitPartition<string> PorUsuario(HttpContext http, int limite, TimeSpan janela) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = janela,
                QueueLimit = 0
            });
}
