using System.Threading.Channels;

namespace Ronu.Api.Services.Email;

/// <summary>Pede o envio de um email sem esperar por ele.</summary>
public interface IFilaEmail
{
    /// <summary>Falso se a fila estiver cheia (o email não será enviado).</summary>
    bool Enfileirar(MensagemEmail mensagem);
}

/// <summary>
/// Fila em memória, lida pelo ServicoEnvioEmail. O envio em segundo plano faz
/// a resposta do "esqueci minha senha" levar o mesmo tempo com ou sem conta
/// (o SMTP leva 1–2 s). Se o App Service reiniciar com algo na fila, o email
/// se perde — a pessoa pede de novo.
/// </summary>
public class FilaEmail : IFilaEmail
{
    // Folga de sobra para o volume do Ronu; cheia, Enfileirar devolve falso.
    private const int Capacidade = 100;

    private readonly Channel<MensagemEmail> _canal =
        Channel.CreateBounded<MensagemEmail>(new BoundedChannelOptions(Capacidade) { FullMode = BoundedChannelFullMode.Wait });

    public bool Enfileirar(MensagemEmail mensagem) => _canal.Writer.TryWrite(mensagem);

    public ChannelReader<MensagemEmail> Leitor => _canal.Reader;
}
