using Microsoft.AspNetCore.RateLimiting;

namespace Ronu.Api.Services;

/// <summary>
/// Os limites de requisição da API e a resposta 429. O OnRejected do ASP.NET Core é um só para todas as políticas,
/// então a mensagem é escolhida pela política da rota: quem estoura o limite da troca de alimento não pode ler
/// a mensagem da redefinição de senha.
/// </summary>
public static class LimitesRequisicoes
{
    public static void Configurar(RateLimiterOptions opcoes)
    {
        LimitesRedefinicaoSenha.Configurar(opcoes);
        LimitesTroca.Configurar(opcoes);

        opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opcoes.OnRejected = (contexto, cancelamento) =>
        {
            var politica = contexto.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
            return new ValueTask(contexto.HttpContext.Response.WriteAsJsonAsync(new { mensagem = Mensagem(politica) }, cancelamento));
        };
    }

    public static string Mensagem(string? politica) => politica switch
    {
        LimitesTroca.PoliticaLeitura or LimitesTroca.PoliticaEscrita => LimitesTroca.MensagemLimite,
        _ => LimitesRedefinicaoSenha.MensagemLimite
    };
}
