using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limite por usuário da gravação de aceite. A tela grava um aceite por vez; o limite só protege o banco de um
/// cliente em laço. Em memória, como os outros. Por usuário (do token), porque a rota exige login.
/// </summary>
public static class LimitesConsentimento
{
    public const string PoliticaAceite = "consentimento-aceite";

    public const int AceitesPorMinuto = 10;

    public const string MensagemLimite = "Muitos pedidos em pouco tempo. Espere um minuto e tente de novo.";

    public static void Configurar(RateLimiterOptions opcoes)
    {
        opcoes.AddPolicy(PoliticaAceite, http => RateLimitPartition.GetFixedWindowLimiter(
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = AceitesPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    }
}
