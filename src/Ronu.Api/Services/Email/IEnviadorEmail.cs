namespace Ronu.Api.Services.Email;

/// <summary>
/// Envia um email. A implementação é escolhida no Program.cs: SMTP (Gmail)
/// quando há credencial, log em desenvolvimento, e um registro de erro em
/// produção sem credencial. Trocar de provedor é trocar a implementação.
/// Quem pede o envio não chama isto direto: usa a IFilaEmail, que envia em
/// segundo plano (ServicoEnvioEmail).
/// </summary>
public interface IEnviadorEmail
{
    Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancelamento);
}
