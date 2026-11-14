using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Models;

namespace Ronu.Api.Services.IA;

/// <summary>
/// O limite de gerações por usuário numa janela móvel de 1 hora, contado na própria tabela DietasIA (nada em memória:
/// o App Service no plano F1 reinicia com frequência e perderia o contador). Não pode passar de 3: a tabela guarda só
/// as 3 dietas mais recentes, então uma contagem maior nunca seria atingida.
///
/// Plano família: a semana da casa cria uma dieta para cada membro (Origem = casa). Para o membro, ela não conta: quem
/// pediu foi o dono. Para o dono, conta tudo (as dele e as semanas da casa, que também lhe criam uma dieta), e as duas
/// rotas dividem a mesma cota de chamadas ao Gemini.
/// </summary>
public static class LimiteGeracoes
{
    public const int PorHora = 3;
    public static readonly TimeSpan Janela = TimeSpan.FromHours(1);

    /// <summary>A mensagem do 429, ou nula quando ainda há vaga.</summary>
    /// <param name="ehDonoDeCasa">Conta também as dietas que a semana da casa criou para a pessoa.</param>
    /// <param name="oQue">"dietas" ou "semanas ou dietas", para o texto.</param>
    public static async Task<string?> ConferirAsync(ApplicationDbContext contexto, int usuarioId, bool ehDonoDeCasa, DateTime agora, string oQue = "dietas")
    {
        var inicioJanela = agora - Janela;
        var geracoesNaJanela = await contexto.DietasIA
            .Where(d => d.UsuarioId == usuarioId && d.DataGeracao >= inicioJanela)
            .Where(d => ehDonoDeCasa || d.Origem == DietaIA.OrigemPessoa)
            .Select(d => d.DataGeracao)
            .ToListAsync();

        if (geracoesNaJanela.Count < PorHora)
        {
            return null;
        }

        // A vaga volta quando a geração mais antiga da janela completa 1 hora.
        var libera = geracoesNaJanela.Min() + Janela;
        var minutos = Math.Max(1, (int)Math.Ceiling((libera - agora).TotalMinutes));
        var quando = minutos == 1 ? "1 minuto" : $"{minutos} minutos";
        return $"Você já gerou {PorHora} {oQue} na última hora, o limite por hora. Tente novamente em {quando}.";
    }
}
