using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limite por usuário das rotas de troca de alimento. A troca é só C# (sem Gemini), mas cada gravação regrava o
/// JSON inteiro da dieta, e o banco é o plano mais simples: o limite protege o servidor de um cliente em laço,
/// não é uma regra de negócio. Em memória, como o da redefinição de senha: zera quando o App Service reinicia.
/// Por usuário (do token), e não por IP, porque as rotas exigem login.
/// </summary>
public static class LimitesTroca
{
    public const string PoliticaLeitura = "trocas-leitura";
    public const string PoliticaEscrita = "trocas-escrita";

    public const int LeiturasPorMinuto = 120;
    public const int EscritasPorMinuto = 30;

    // A janela é de um minuto: dá para dizer quanto esperar, sem o "mais tarde" da redefinição de senha.
    public const string MensagemLimite = "Muitos pedidos de troca em pouco tempo. Espere um minuto e tente de novo.";

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
