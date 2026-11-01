using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limites por usuário de apagar a conta e baixar os dados. Na exclusão, contra quem tem um token roubado e tenta
/// adivinhar a senha por ela (a confirmação pede a senha); na exportação, contra um cliente em laço, porque o arquivo
/// junta todas as tabelas da conta. Em memória, como os outros.
/// </summary>
public static class LimitesConta
{
    public const string PoliticaExclusao = "conta-exclusao";
    public const string PoliticaExportacao = "conta-exportacao";

    public const int ExclusoesPorJanela = 5;
    public static readonly TimeSpan JanelaExclusao = TimeSpan.FromMinutes(15);

    public const int ExportacoesPorJanela = 5;
    public static readonly TimeSpan JanelaExportacao = TimeSpan.FromHours(1);

    public const string MensagemExclusao = "Muitas tentativas de apagar a conta. Espere 15 minutos e tente de novo.";
    public const string MensagemExportacao = "Você já baixou seus dados várias vezes na última hora. Tente de novo mais tarde.";

    public static void Configurar(RateLimiterOptions opcoes)
    {
        opcoes.AddPolicy(PoliticaExclusao, http => PorUsuario(http, ExclusoesPorJanela, JanelaExclusao));
        opcoes.AddPolicy(PoliticaExportacao, http => PorUsuario(http, ExportacoesPorJanela, JanelaExportacao));
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
