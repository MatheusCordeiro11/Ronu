namespace Ronu.Api.Services.Email;

/// <summary>
/// Envia, um por vez, os emails da FilaEmail. Uma falha num email vai para o
/// log (sem o conteúdo, que pode ter o link de redefinição) e não para a fila.
/// </summary>
public class ServicoEnvioEmail : BackgroundService
{
    private readonly FilaEmail _fila;
    private readonly IEnviadorEmail _enviador;
    private readonly ILogger<ServicoEnvioEmail> _logger;

    public ServicoEnvioEmail(FilaEmail fila, IEnviadorEmail enviador, ILogger<ServicoEnvioEmail> logger)
    {
        _fila = fila;
        _enviador = enviador;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelamento)
    {
        await foreach (var mensagem in _fila.Leitor.ReadAllAsync(cancelamento))
        {
            try
            {
                await _enviador.EnviarAsync(mensagem, cancelamento);
            }
            catch (Exception ex) when (!cancelamento.IsCancellationRequested)
            {
                _logger.LogError(ex, "Falha ao enviar email. Assunto: {Assunto}", mensagem.Assunto);
            }
        }
    }
}
