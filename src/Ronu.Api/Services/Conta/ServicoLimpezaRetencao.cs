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

    // Lida uma vez, na subida: a variável do App Service só muda com reinício. O formato já foi conferido no
    // Program.cs, que não deixa a API subir com uma data inválida.
    private readonly DateTime? _rotinaForaDaTelaEm;

    public ServicoLimpezaRetencao(IServiceScopeFactory escopos, ILogger<ServicoLimpezaRetencao> logger, IConfiguration configuracao)
    {
        _escopos = escopos;
        _logger = logger;
        _rotinaForaDaTelaEm = LimpezaRetencao.LerRotinaForaDaTelaEm(configuracao);
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
            var resultado = await LimpezaRetencao.ExecutarAsync(contexto, DateTime.UtcNow, _rotinaForaDaTelaEm);

            // Só as contagens: nem ids nem emails das contas, nem o texto das rotinas. A data da rotina vai junto para
            // dar para conferir no log que a configuração chegou (vazia = a limpeza da rotina ainda não está ligada).
            _logger.LogInformation("Limpeza de retenção: {Pedidos} pedidos de redefinição de senha, {Contas} contas sem aceite e {Rotinas} rotinas em texto apagados (rotina fora da tela desde: {RotinaForaDaTelaEm})",
                resultado.PedidosApagados, resultado.ContasApagadas, resultado.RotinasApagadas,
                _rotinaForaDaTelaEm?.ToString("yyyy-MM-dd") ?? "não configurado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na limpeza de retenção; tenta de novo na próxima rodada");
        }
    }
}
