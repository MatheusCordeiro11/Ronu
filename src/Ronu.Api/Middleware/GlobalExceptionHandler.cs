using Microsoft.AspNetCore.Diagnostics;

namespace Ronu.Api.Middleware;

/// <summary>
/// Captura qualquer exceção não tratada em qualquer Controller, antes que o
/// ASP.NET Core a transforme numa resposta padrão. Em Development, devolve
/// a mensagem real da exceção (necessário para depurar localmente, como já
/// fizemos várias vezes durante o desenvolvimento). Em Production, devolve
/// sempre uma mensagem genérica — nunca stack trace, nome de classe ou
/// detalhe interno — mas registra o erro real no log do servidor, para
/// investigação posterior.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _ambiente;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IHostEnvironment ambiente, ILogger<GlobalExceptionHandler> logger)
    {
        _ambiente = ambiente;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exceção não tratada em {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        var mensagem = _ambiente.IsDevelopment()
            ? exception.ToString()
            : "Ocorreu um erro interno. Tente novamente.";

        await httpContext.Response.WriteAsJsonAsync(new { mensagem }, cancellationToken);

        return true;
    }
}
