using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;

namespace Ronu.Api.Services;

/// <summary>
/// Checagem extra de cada token JWT, depois da assinatura e da validade
/// (JwtBearer, OnTokenValidated): o usuário ainda existe, e o token foi
/// emitido depois da última troca de senha (claim "iat" contra
/// Usuario.SenhaAlteradaEm). Assim, redefinir a senha encerra as sessões
/// abertas, inclusive a de quem tenha o token roubado.
///
/// Tokens sem "iat" (emitidos antes desta checagem existir) valem até
/// expirar, no máximo 2 horas. Custa uma consulta por chave primária a cada
/// requisição autenticada.
/// </summary>
public static class ValidacaoSessaoJwt
{
    public static async Task<bool> SessaoValidaAsync(ClaimsPrincipal principal, ApplicationDbContext context)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return false;
        }

        var usuario = await context.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new { u.SenhaAlteradaEm })
            .FirstOrDefaultAsync();

        if (usuario is null)
        {
            return false;
        }

        if (usuario.SenhaAlteradaEm is null)
        {
            return true;
        }

        if (!long.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Iat), out var iatSegundos))
        {
            return true;
        }

        // O "iat" tem precisão de segundos: a troca de senha é arredondada
        // para CIMA, então um token emitido no mesmo segundo da troca (antes
        // ou depois dela, não dá para saber) também deixa de valer. Um login
        // de verdade pela tela nunca acontece nesse mesmo segundo.
        var trocaEmMs = new DateTimeOffset(DateTime.SpecifyKind(usuario.SenhaAlteradaEm.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var trocaEmSegundos = (trocaEmMs + 999) / 1000;
        return iatSegundos >= trocaEmSegundos;
    }
}
