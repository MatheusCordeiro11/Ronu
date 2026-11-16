using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Ronu.Api.Data;
using Ronu.Api.Migrations;

namespace Ronu.Api.Tests;

/// <summary>
/// Catálogo de modalidades: os valores fixos da migration AmpliaCatalogoModalidades
/// contra a fonte no código (MetsCompendio2024, CategoriasModalidade) e a
/// migration rodando de verdade num Postgres local descartável.
/// </summary>
public class CatalogoModalidadesTests
{
    [Fact]
    public void Valores_Da_Migration_Batem_Com_MetsCompendio2024_E_As_Categorias()
    {
        var naMigration = AmpliaCatalogoModalidades.Novas.Select(n => n.Nome)
            .Concat(AmpliaCatalogoModalidades.Existentes.Select(e => e.Nome))
            .ToList();

        Assert.Equal(26, naMigration.Count);
        Assert.Equal(naMigration.Count, naMigration.Distinct().Count());
        Assert.Equal(MetsCompendio2024.PorModalidade.Keys.OrderBy(n => n), naMigration.OrderBy(n => n));
        Assert.All(AmpliaCatalogoModalidades.Novas, n => Assert.Equal(MetsCompendio2024.PorModalidade[n.Nome], n.Met));
        Assert.All(AmpliaCatalogoModalidades.Novas.Select(n => n.Categoria)
            .Concat(AmpliaCatalogoModalidades.Existentes.Select(e => e.Categoria)),
            c => Assert.Contains(c, CategoriasModalidade.Ordem));
    }

    [Fact]
    public void METs_Das_Sete_Modalidades_Antigas_Nao_Mudaram()
    {
        var antigos = new Dictionary<string, decimal>
        {
            ["Jiu-jitsu"] = 7.8m, ["Muay Thai"] = 7.8m, ["Boxe"] = 6.8m, ["Musculação"] = 3.5m,
            ["Futebol"] = 7.0m, ["Basquete"] = 7.5m, ["Natação"] = 5.8m
        };

        Assert.All(antigos, a => Assert.Equal(a.Value, MetsCompendio2024.PorModalidade[a.Key]));
    }

    // Ritmo da aula: os blocos escritos na migration AdicionaIntensidadeTreino batem com a fonte no código; só as 11
    // aulas em blocos os têm (o Vôlei, média de quadra e areia, fica sem), e o MetReferencia de cada uma é a aula
    // com a premissa de 50/50.
    [Fact]
    public void Blocos_Da_Migration_Batem_Com_MetsCompendio2024()
    {
        var naMigration = AdicionaIntensidadeTreino.Blocos.ToDictionary(b => b.Nome, b => (b.Leve, b.Intenso));
        var noCodigo = MetsCompendio2024.BlocosPorModalidade.ToDictionary(b => b.Key, b => (b.Value.Leve.Met, b.Value.Intenso.Met));

        Assert.Equal(11, naMigration.Count);
        Assert.Equal(noCodigo.OrderBy(b => b.Key), naMigration.OrderBy(b => b.Key));
        Assert.All(naMigration.Keys, nome => Assert.Contains(nome, MetsCompendio2024.PorModalidade.Keys));
        Assert.DoesNotContain("Vôlei", naMigration.Keys);
        Assert.Equal(15, MetsCompendio2024.PorModalidade.Keys.Count(n => !naMigration.ContainsKey(n)));
        Assert.All(MetsCompendio2024.BlocosPorModalidade, b => Assert.Equal(
            MetsCompendio2024.PorModalidade[b.Key], MetsCompendio2024.Aula(b.Value.Leve, b.Value.Intenso, 0.5m)));
    }

    /// <summary>
    /// Banco descartável no Postgres LOCAL: aplica as migrations até a anterior,
    /// grava o catálogo antigo do seed e um vínculo de usuário, aplica a nova e
    /// confere 26 modalidades, os 7 ids, nomes e METs intactos, o vínculo
    /// intacto e a categoria de todas. Depois apaga o banco.
    /// Precisa da variável RONU_TESTE_POSTGRES com uma connection string do
    /// servidor local (o banco dela não é usado; o teste cria e apaga o seu).
    /// </summary>
    [FactComPostgresLocal]
    public async Task Migration_Insere_19_E_Nao_Mexe_Nas_7_Antigas_Nem_Nos_Vinculos()
    {
        var servidor = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(FactComPostgresLocal.Variavel));
        var nomeBanco = $"ronu_teste_migracao_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(servidor.ConnectionString) { Database = "postgres", Pooling = false };
        var teste = new NpgsqlConnectionStringBuilder(servidor.ConnectionString) { Database = nomeBanco, Pooling = false };

        await Executar(admin.ConnectionString, $"CREATE DATABASE \"{nomeBanco}\";");
        try
        {
            var opcoes = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(teste.ConnectionString).Options;
            await using (var banco = new ApplicationDbContext(opcoes))
            {
                await banco.GetService<IMigrator>().MigrateAsync("20261007183702_CriaPedidosRedefinicaoSenha");
            }

            // O catálogo como está hoje nos bancos (docs/seed-modalidades.sql
            // antes desta migration) e um usuário que pratica duas delas.
            await Executar(teste.ConnectionString, """
                INSERT INTO "Modalidades" ("Nome", "MetReferencia") VALUES ('Jiu-jitsu', 7.8);
                INSERT INTO "Modalidades" ("Nome", "MetReferencia") VALUES ('Muay Thai', 7.8);
                INSERT INTO "Modalidades" ("Nome", "MetReferencia") VALUES ('Boxe', 6.8);
                INSERT INTO "Modalidades" ("Nome", "MetReferencia") VALUES ('Musculação', 3.5);
                INSERT INTO "Modalidades" ("Nome", "MetReferencia") VALUES ('Futebol', 7.0);
                INSERT INTO "Modalidades" ("Nome", "MetReferencia") VALUES ('Basquete', 7.5);
                INSERT INTO "Modalidades" ("Nome", "MetReferencia") VALUES ('Natação', 5.8);
                INSERT INTO "Usuarios" ("Nome", "Email", "Estado") VALUES ('Teste', 'teste-migracao@exemplo.test', 'MG');
                INSERT INTO "UsuarioModalidades" ("UsuarioId", "ModalidadeId", "DiasSemana", "DuracaoMediaHoras")
                    SELECT u."Id", m."Id", ARRAY[1, 3], 1.5 FROM "Usuarios" u, "Modalidades" m WHERE m."Nome" IN ('Jiu-jitsu', 'Natação');
                """);
            var antes = await Ler(teste.ConnectionString,
                "SELECT \"Id\", \"Nome\", \"MetReferencia\" FROM \"Modalidades\" ORDER BY \"Id\";");
            var vinculosAntes = await Ler(teste.ConnectionString,
                "SELECT \"Id\", \"UsuarioId\", \"ModalidadeId\", \"DuracaoMediaHoras\" FROM \"UsuarioModalidades\" ORDER BY \"Id\";");

            await using (var banco = new ApplicationDbContext(opcoes))
            {
                await banco.Database.MigrateAsync();
            }

            var depois = await Ler(teste.ConnectionString,
                "SELECT \"Id\", \"Nome\", \"MetReferencia\", \"Categoria\" FROM \"Modalidades\" ORDER BY \"Id\";");
            Assert.Equal(26, depois.Count);
            Assert.Equal(26, depois.Select(l => l[1]).Distinct().Count());
            Assert.Equal(antes, depois.Take(7).Select(l => l.Take(3).ToArray()).ToList());
            Assert.All(depois, l => Assert.Contains((string)l[3], CategoriasModalidade.Ordem));
            Assert.All(depois, l => Assert.Equal(MetsCompendio2024.PorModalidade[(string)l[1]], (decimal)l[2]));

            // AdicionaIntensidadeTreino: blocos só nas 11 aulas em blocos; os vínculos de antes ficam em "normal".
            var blocos = await Ler(teste.ConnectionString,
                "SELECT \"Nome\", \"MetBlocoLeve\", \"MetBlocoIntenso\" FROM \"Modalidades\";");
            Assert.All(blocos, l =>
            {
                if (MetsCompendio2024.BlocosPorModalidade.TryGetValue((string)l[0], out var b))
                {
                    Assert.Equal((b.Leve.Met, b.Intenso.Met), ((decimal)l[1], (decimal)l[2]));
                }
                else
                {
                    Assert.Equal((DBNull.Value, DBNull.Value), (l[1], l[2]));
                }
            });
            var intensidades = await Ler(teste.ConnectionString, "SELECT \"Intensidade\" FROM \"UsuarioModalidades\";");
            Assert.All(intensidades, l => Assert.Equal("normal", l[0]));

            var vinculosDepois = await Ler(teste.ConnectionString,
                "SELECT \"Id\", \"UsuarioId\", \"ModalidadeId\", \"DuracaoMediaHoras\" FROM \"UsuarioModalidades\" ORDER BY \"Id\";");
            Assert.Equal(vinculosAntes, vinculosDepois);

            // Down volta às 7 de antes (mesmos ids); Up de novo volta às 26.
            await using (var banco = new ApplicationDbContext(opcoes))
            {
                var migrator = banco.GetService<IMigrator>();
                await migrator.MigrateAsync("20261007183702_CriaPedidosRedefinicaoSenha");
                var depoisDoDown = await Ler(teste.ConnectionString, "SELECT \"Id\" FROM \"Modalidades\" ORDER BY \"Id\";");
                Assert.Equal(antes.Select(l => l[0]), depoisDoDown.Select(l => l[0]));
                await migrator.MigrateAsync();
            }
            Assert.Equal(26, (await Ler(teste.ConnectionString, "SELECT \"Id\" FROM \"Modalidades\";")).Count);
        }
        finally
        {
            await Executar(admin.ConnectionString, $"DROP DATABASE IF EXISTS \"{nomeBanco}\" WITH (FORCE);");
        }
    }

    private static async Task Executar(string conexao, string sql)
    {
        await using var c = new NpgsqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<List<object[]>> Ler(string conexao, string sql)
    {
        await using var c = new NpgsqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, c);
        await using var leitor = await comando.ExecuteReaderAsync();
        var linhas = new List<object[]>();
        while (await leitor.ReadAsync())
        {
            var linha = new object[leitor.FieldCount];
            leitor.GetValues(linha);
            linhas.Add(linha);
        }
        return linhas;
    }
}

/// <summary>
/// Teste que só roda com a variável RONU_TESTE_POSTGRES apontando para um
/// Postgres em localhost; sem ela (ou com outro host) aparece como pulado.
/// A checagem do host impede que um teste que cria e apaga bancos chegue
/// perto da produção.
/// </summary>
public sealed class FactComPostgresLocal : FactAttribute
{
    public const string Variavel = "RONU_TESTE_POSTGRES";

    public FactComPostgresLocal()
    {
        var conexao = Environment.GetEnvironmentVariable(Variavel);
        if (string.IsNullOrWhiteSpace(conexao))
        {
            Skip = $"Defina {Variavel} com a connection string do Postgres local para rodar.";
            return;
        }

        var host = new NpgsqlConnectionStringBuilder(conexao).Host;
        if (host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            Skip = $"{Variavel} precisa apontar para localhost (está em outro host).";
        }
    }
}
