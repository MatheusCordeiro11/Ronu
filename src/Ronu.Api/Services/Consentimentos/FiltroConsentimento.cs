using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ronu.Api.Data;

namespace Ronu.Api.Services.Consentimentos;

/// <summary>
/// Barra no servidor quem não aceitou o consentimento geral vigente: toda rota com [Authorize] responde 403
/// { mensagem, motivo: "consentimento" }, e a tela leva a pessoa para o aceite. Bloquear só na tela não protegeria
/// nada: o dado de saúde só pode ser tratado depois do aceite (art. 11, I), chame quem chamar a API.
///
/// 403 e não 401 de propósito: o token vale, e o 401 faria a tela encerrar a sessão. Ficam de fora as rotas
/// públicas e as com [DispensaConsentimento]. Registrado para todos os controllers (Program.cs), então uma rota nova
/// já nasce protegida. Custa uma consulta indexada por requisição autenticada.
/// </summary>
public class FiltroConsentimento : IAsyncAuthorizationFilter
{
    private readonly ApplicationDbContext _contexto;
    private readonly IConfiguration _configuracao;

    public FiltroConsentimento(ApplicationDbContext contexto, IConfiguration configuracao)
    {
        _contexto = contexto;
        _configuracao = configuracao;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext contexto)
    {
        var metadados = contexto.ActionDescriptor.EndpointMetadata;
        var exigeLogin = metadados.OfType<IAuthorizeData>().Any() && !metadados.OfType<IAllowAnonymous>().Any();
        if (!exigeLogin || metadados.OfType<DispensaConsentimentoAttribute>().Any() || !RegrasConsentimento.Exigir(_configuracao))
        {
            return;
        }

        // Sem usuário no token, o [Authorize] já respondeu 401 antes de chegar aqui.
        if (!int.TryParse(contexto.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            return;
        }

        if (!await RegrasConsentimento.TemAceiteVigenteAsync(_contexto, usuarioId, TextosConsentimento.TipoGeral))
        {
            contexto.Result = new ObjectResult(new
            {
                mensagem = RegrasConsentimento.MensagemSemAceite,
                motivo = RegrasConsentimento.MotivoSemAceite
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
