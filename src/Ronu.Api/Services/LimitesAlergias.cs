using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limite por usuário das rotas de alergias (TACO 2c). Protege o servidor de um cliente em laço, não é regra de
/// negócio: marcar os 8 chips de uma vez, e desmarcar, cabe com folga. Em memória e por usuário (do token), como o
/// da troca de alimento.
/// </summary>
public static class LimitesAlergias
{
    public const string PoliticaAlergias = "alergias";

    public const int PedidosPorMinuto = 60;

    public const string MensagemLimite = "Muitas mudanças nas alergias em pouco tempo. Espere um minuto e tente de novo.";

    public static void Configurar(RateLimiterOptions opcoes) =>
        opcoes.AddPolicy(PoliticaAlergias, http => RateLimitPartition.GetFixedWindowLimiter(
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PedidosPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
}
