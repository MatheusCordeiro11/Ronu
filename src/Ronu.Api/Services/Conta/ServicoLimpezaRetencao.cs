using Ronu.Api.Data;

namespace Ronu.Api.Services.Conta;

/// <summary>
/// Roda a LimpezaRetencao pouco depois de a API subir e a cada 24 horas. No App Service F1 a API dorme sem uso e
/// sobe de novo no primeiro acesso, então a limpeza roda pelo menos a cada subida. Um erro vai para o log e não
/// derruba a API (um BackgroundService que lança exceção para o host inteiro).
/// </summary>
public class ServicoLimpezaRetencao : BackgroundService
{
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<ServicoLimpezaRetencao> _logger;

    public ServicoLimpezaRetencao(IServiceScopeFactory escopos, ILogger<ServicoLimpezaRetencao> logger)
    {
        _escopos = escopos;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancelamento)
    {
        try
        {
            // A subida fica livre para atender o primeiro acesso antes.
            await Task.Delay(EsperaInicial, cancelamento);

            using var relogio = new PeriodicTimer(Intervalo);
            do
            {
                await RodarAsync();
            }
            while (await relogio.WaitForNextTickAsync(cancelamento));
        }
        catch (OperationCanceledException)
        {
            // A API está parando.
        }
    }

    private async Task RodarAsync()
    {
        try
        {
            using var escopo = _escopos.CreateScope();
            var contexto = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var resultado = await LimpezaRetencao.ExecutarAsync(contexto, DateTime.UtcNow);

            // Só as contagens: nem ids nem emails das contas apagadas.
            _logger.LogInformation("Limpeza de retenção: {Pedidos} pedidos de redefinição de senha e {Contas} contas sem aceite apagados",
                resultado.PedidosApagados, resultado.ContasApagadas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na limpeza de retenção; tenta de novo na próxima rodada");
        }
    }
}
