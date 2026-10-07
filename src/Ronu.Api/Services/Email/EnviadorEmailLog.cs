namespace Ronu.Api.Services.Email;

/// <summary>
/// Só em desenvolvimento, sem credencial de email: em vez de enviar, escreve
/// o email inteiro no log (inclusive o link de redefinição de senha), para
/// testar localmente sem segredo nenhum. Nunca é usado em produção — lá, o
/// link no log seria um vazamento do token.
/// </summary>
public class EnviadorEmailLog : IEnviadorEmail
{
    private readonly ILogger<EnviadorEmailLog> _logger;

    public EnviadorEmailLog(ILogger<EnviadorEmailLog> logger)
    {
        _logger = logger;
    }

    public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancelamento)
    {
        _logger.LogInformation(
            "Email NÃO enviado (modo de desenvolvimento, sem credencial) para {Para}. Assunto: {Assunto}\n{Texto}",
            mensagem.Para, mensagem.Assunto, mensagem.Texto);
        return Task.CompletedTask;
    }
}
