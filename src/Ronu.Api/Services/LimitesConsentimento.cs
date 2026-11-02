using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limites do consentimento. A gravação do aceite é por usuário: a tela grava um aceite por vez, e o limite só
/// protege o banco de um cliente em laço. O texto público (sem login, para o cadastro) é por IP e folgado: a tela lê
/// uma vez por visita, e o limite só corta quem martela a rota. Em memória, como os outros.
/// </summary>
public static class LimitesConsentimento
{
    public const string PoliticaAceite = "consentimento-aceite";
    public const string PoliticaTextoPublico = "consentimento-texto";

    public const int AceitesPorMinuto = 10;
    public const int LeiturasTextoPorMinuto = 60;

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
        opcoes.AddPolicy(PoliticaTextoPublico, http =>
            LimitesAutenticacao.PorIp(http, LeiturasTextoPorMinuto, TimeSpan.FromMinutes(1)));
    }
}
