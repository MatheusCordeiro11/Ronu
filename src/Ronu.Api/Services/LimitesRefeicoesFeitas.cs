using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limite por usuário das rotas de refeição feita. Protege o servidor de um cliente em laço, não é regra de negócio:
/// a escrita é uma linha pequena (não regrava o JSON da dieta como a troca), então o limite é mais folgado que o da
/// troca. Em memória e por usuário (do token), como o da troca de alimento.
/// </summary>
public static class LimitesRefeicoesFeitas
{
    public const string PoliticaLeitura = "refeicoes-feitas-leitura";
    public const string PoliticaEscrita = "refeicoes-feitas-escrita";

    public const int LeiturasPorMinuto = 120;
    public const int EscritasPorMinuto = 60;

    public const string MensagemLimite = "Muitas marcações em pouco tempo. Espere um minuto e tente de novo.";

    public static void Configurar(RateLimiterOptions opcoes)
    {
        opcoes.AddPolicy(PoliticaLeitura, http => PorUsuario(http, LeiturasPorMinuto));
        opcoes.AddPolicy(PoliticaEscrita, http => PorUsuario(http, EscritasPorMinuto));
    }

    private static RateLimitPartition<string> PorUsuario(HttpContext http, int limitePorMinuto) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limitePorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
}
