using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Limite por IP das rotas públicas de entrada. No login, contra força bruta de senha; no Google e no cadastro,
/// contra conta criada em massa, que vira custo quando cada conta pode gerar dietas no Gemini pago. Em memória,
/// como o da redefinição de senha: zera quando o App Service reinicia, aceitável para um limite de defesa. O IP é o
/// do cliente graças aos forwarded headers (Program.cs).
///
/// O limitador conta toda requisição, certa ou errada: ele roda antes da ação e não sabe o resultado. As folgas
/// cobrem IP compartilhado (CGNAT do celular, rede de faculdade).
/// </summary>
public static class LimitesAutenticacao
{
    public const string PoliticaLogin = "auth-login";
    public const string PoliticaGoogle = "auth-google";
    public const string PoliticaCadastro = "auth-cadastro";

    public const int TentativasEntradaPorJanela = 20;
    public static readonly TimeSpan JanelaEntrada = TimeSpan.FromMinutes(15);

    public const int CadastrosPorJanela = 5;
    public static readonly TimeSpan JanelaCadastro = TimeSpan.FromHours(1);

    public const string MensagemEntrada = "Muitas tentativas de entrar. Espere 15 minutos e tente de novo.";
    public const string MensagemCadastro = "Muitas contas criadas a partir desta conexão. Tente de novo em até uma hora.";

    public static void Configurar(RateLimiterOptions opcoes)
    {
        // Login e Google com o mesmo limite, mas cada um com a sua contagem: quem errou a senha ainda pode entrar
        // com o Google.
        opcoes.AddPolicy(PoliticaLogin, http => PorIp(http, TentativasEntradaPorJanela, JanelaEntrada));
        opcoes.AddPolicy(PoliticaGoogle, http => PorIp(http, TentativasEntradaPorJanela, JanelaEntrada));
        opcoes.AddPolicy(PoliticaCadastro, http => PorIp(http, CadastrosPorJanela, JanelaCadastro));
    }

    public static RateLimitPartition<string> PorIp(HttpContext http, int limite, TimeSpan janela) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = janela,
                QueueLimit = 0
            });
}
