using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ronu.Api.Data;

namespace Ronu.Api.Tests;

/// <summary>
/// Apoio dos testes que precisam de banco: um ApplicationDbContext em memória,
/// isolado por teste (nome único). O provedor em memória não aplica índices
/// únicos nem transações; o que depende disso fica coberto pela migration.
/// </summary>
internal static class BancoEmMemoria
{
    public static ApplicationDbContext NovoContexto(string? nome = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nome ?? Guid.NewGuid().ToString())
            .Options);

    // A chave precisa de pelo menos 32 bytes para o HMAC-SHA256 do JWT.
    public static IConfiguration Configuracao(Dictionary<string, string?>? extra = null)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Jwt:ChaveSecreta"] = "chave-de-teste-com-pelo-menos-32-bytes-0123456789",
            ["Frontend:UrlBase"] = "http://127.0.0.1:5500/frontend"
        };

        foreach (var (chave, valor) in extra ?? new())
        {
            valores[chave] = valor;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }
}
