namespace Ronu.Api.Services.Email;

/// <summary>
/// Fora de desenvolvimento e sem Email:Usuario/Email:SenhaApp: não envia e
/// registra um erro — sem o conteúdo do email, que pode ter o link de
/// redefinição. A resposta da API não muda (o pedido de redefinição devolve
/// sempre 202), então o log é o único sinal de que o envio está parado.
/// </summary>
public class EnviadorEmailNaoConfigurado : IEnviadorEmail
{
    private readonly ILogger<EnviadorEmailNaoConfigurado> _logger;

    public EnviadorEmailNaoConfigurado(ILogger<EnviadorEmailNaoConfigurado> logger)
    {
        _logger = logger;
    }

    public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancelamento)
    {
        _logger.LogError(
            "Email não enviado: Email:Usuario e Email:SenhaApp não estão configurados. Assunto: {Assunto}",
            mensagem.Assunto);
        return Task.CompletedTask;
    }
}
