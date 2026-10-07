using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Services.Email;

namespace Ronu.Api.Tests;

/// <summary>
/// Envio de email em segundo plano: o que entra na FilaEmail é enviado pelo
/// ServicoEnvioEmail, e uma falha num email não para os seguintes.
/// </summary>
public class EnvioEmailTests
{
    [Fact]
    public async Task Email_Da_Fila_E_Enviado_Em_Segundo_Plano()
    {
        var fila = new FilaEmail();
        var enviador = new EnviadorFalso();
        using var servico = new ServicoEnvioEmail(fila, enviador, NullLogger<ServicoEnvioEmail>.Instance);
        await servico.StartAsync(CancellationToken.None);

        Assert.True(fila.Enfileirar(Mensagem("a@teste.local")));

        await enviador.EsperarAsync(1);
        Assert.Equal(["a@teste.local"], enviador.Enviados);
        await servico.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Falha_Num_Email_Nao_Para_Os_Seguintes()
    {
        var fila = new FilaEmail();
        var enviador = new EnviadorFalso { FalharPara = "falha@teste.local" };
        using var servico = new ServicoEnvioEmail(fila, enviador, NullLogger<ServicoEnvioEmail>.Instance);
        await servico.StartAsync(CancellationToken.None);

        fila.Enfileirar(Mensagem("falha@teste.local"));
        fila.Enfileirar(Mensagem("b@teste.local"));

        await enviador.EsperarAsync(1);
        Assert.Equal(["b@teste.local"], enviador.Enviados);
        await servico.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Configurado_Exige_Usuario_E_Senha_De_App()
    {
        Assert.False(new EmailOptions().Configurado);
        Assert.False(new EmailOptions { Usuario = "ronu@gmail.com" }.Configurado);
        Assert.True(new EmailOptions { Usuario = "ronu@gmail.com", SenhaApp = "abcd efgh ijkl mnop" }.Configurado);
        Assert.Equal(587, new EmailOptions().Porta);
    }

    private static MensagemEmail Mensagem(string para) => new(para, "Fulano", "Assunto", "Texto", "<p>Texto</p>");

    private sealed class EnviadorFalso : IEnviadorEmail
    {
        private readonly object _trava = new();
        private readonly List<string> _enviados = new();
        private TaskCompletionSource _sinal = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? FalharPara { get; init; }

        public IReadOnlyList<string> Enviados
        {
            get { lock (_trava) { return _enviados.ToList(); } }
        }

        public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancelamento)
        {
            if (mensagem.Para == FalharPara)
            {
                throw new InvalidOperationException("SMTP fora do ar");
            }

            lock (_trava)
            {
                _enviados.Add(mensagem.Para);
                _sinal.TrySetResult();
            }

            return Task.CompletedTask;
        }

        public async Task EsperarAsync(int quantidade)
        {
            var limite = DateTime.UtcNow.AddSeconds(5);
            while (Enviados.Count < quantidade && DateTime.UtcNow < limite)
            {
                await Task.WhenAny(_sinal.Task, Task.Delay(50));
                lock (_trava) { _sinal = new(TaskCreationOptions.RunContinuationsAsynchronously); }
            }
        }
    }
}
