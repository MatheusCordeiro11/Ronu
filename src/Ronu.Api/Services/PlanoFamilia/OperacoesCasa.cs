using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Services.Consentimentos;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// O que o CasaController e a exclusão de conta fazem igual: sair da casa, passar a casa adiante, o token do convite e
/// a trava da casa no banco.
///
/// Transação e trava só no PostgreSQL. O banco em memória dos testes não tem transação (e reclama dela) nem
/// SELECT ... FOR UPDATE; lá as mesmas operações rodam em sequência, e o que depende de concorrência fica coberto pelos
/// índices únicos da migration.
/// </summary>
public static class OperacoesCasa
{
    public const int MaximoMembros = 6;
    public static readonly TimeSpan ValidadeConvite = TimeSpan.FromDays(7);

    // 32 bytes aleatórios (256 bits), como o link de redefinição de senha: impossível de adivinhar, por isso basta um
    // SHA-256 no banco.
    public static string NovoToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // Só a primeira palavra do nome: o sobrenome não é da conta da casa.
    public static string PrimeiroNome(string? nome) =>
        nome?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    public static async Task<IDbContextTransaction?> IniciarTransacaoAsync(ApplicationDbContext contexto) =>
        contexto.Database.IsRelational() ? await contexto.Database.BeginTransactionAsync() : null;

    /// <summary>
    /// Trava a linha da casa até o fim da transação. Serializa o que conta os membros e depois grava (aceitar convite,
    /// o dono sair sozinho, a exclusão de conta do dono): sem ela, dois aceites ao mesmo tempo passariam do máximo, e o
    /// dono "sozinho" apagaria a casa com alguém que acabou de entrar.
    /// </summary>
    public static async Task TravarAsync(ApplicationDbContext contexto, int casaId)
    {
        if (contexto.Database.IsRelational())
        {
            await contexto.Database.ExecuteSqlAsync($"SELECT 1 FROM \"Casas\" WHERE \"Id\" = {casaId} FOR UPDATE");
        }
    }

    public static bool EhChaveDuplicada(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>
    /// Grava o UsadoEm só se o convite ainda serve, e diz se foi este pedido que o usou. No PostgreSQL é um UPDATE com a
    /// condição no WHERE: de dois aceites ao mesmo tempo, só um muda a linha. No banco em memória, que não tem
    /// ExecuteUpdate, carrega e marca (o SaveChanges de quem chamou grava).
    /// </summary>
    public static async Task<bool> UsarConviteAsync(ApplicationDbContext contexto, int conviteId, DateTime agora)
    {
        var aindaServe = contexto.ConvitesCasa.Where(c => c.Id == conviteId && c.UsadoEm == null && c.RevogadoEm == null && c.ExpiraEm > agora);

        if (contexto.Database.IsRelational())
        {
            return await aindaServe.ExecuteUpdateAsync(s => s.SetProperty(c => c.UsadoEm, agora)) == 1;
        }

        var convite = await aindaServe.FirstOrDefaultAsync();
        if (convite is null)
        {
            return false;
        }

        convite.UsadoEm = agora;
        return true;
    }

    // O aceite do tipo casa fica (histórico), com RevogadoEm: sair da casa é o "desfazer" da autorização.
    public static async Task RevogarConsentimentoAsync(ApplicationDbContext contexto, int usuarioId, DateTime agora)
    {
        var aceites = await contexto.Consentimentos
            .Where(c => c.UsuarioId == usuarioId && c.Tipo == TextosConsentimento.TipoCasa && c.RevogadoEm == null)
            .ToListAsync();
        foreach (var aceite in aceites)
        {
            aceite.RevogadoEm = agora;
        }
    }

    /// <summary>
    /// Tira a pessoa da casa (sair ou ser removida) e revoga o aceite dela. Se era o dono sozinho, a casa acaba, com os
    /// convites. Quem chama já conferiu que o dono está sozinho. Não grava: o SaveChanges é de quem chamou.
    /// </summary>
    public static async Task SairAsync(ApplicationDbContext contexto, MembroCasa membro, DateTime agora)
    {
        contexto.MembrosCasa.Remove(membro);
        await RevogarConsentimentoAsync(contexto, membro.UsuarioId, agora);

        if (membro.Papel == MembroCasa.PapelDono)
        {
            await ApagarCasaAsync(contexto, membro.CasaId);
        }
    }

    /// <summary>
    /// Exclusão de conta (decisão D3): o direito de apagar a conta não pode depender de transferir a casa. Membro sai;
    /// dono sozinho leva a casa junto; dono com membros passa a casa para o membro mais antigo. Os convites que o dono
    /// gerou saem com a conta dele (são linhas dele), e o novo dono gera outros. Quem chama abre a transação.
    /// </summary>
    public static async Task SairParaExclusaoAsync(ApplicationDbContext contexto, int usuarioId)
    {
        var membro = await contexto.MembrosCasa.FirstOrDefaultAsync(m => m.UsuarioId == usuarioId);
        if (membro is null || membro.Papel != MembroCasa.PapelDono)
        {
            // O membro sai pela lista de tabelas da ExclusaoConta (MembrosCasa por UsuarioId).
            return;
        }

        await TravarAsync(contexto, membro.CasaId);
        var proximo = await contexto.MembrosCasa
            .Where(m => m.CasaId == membro.CasaId && m.Id != membro.Id)
            .OrderBy(m => m.EntrouEm).ThenBy(m => m.Id)
            .FirstOrDefaultAsync();

        if (proximo is null)
        {
            await ApagarCasaAsync(contexto, membro.CasaId);
            return;
        }

        // Em dois SaveChanges: o índice único parcial (um dono por casa) recusaria o novo dono enquanto a linha do
        // antigo existe, e o EF não garante a ordem entre um DELETE e um UPDATE da mesma tabela.
        contexto.MembrosCasa.Remove(membro);
        await contexto.SaveChangesAsync();

        foreach (var oferecido in await contexto.MembrosCasa.Where(m => m.CasaId == membro.CasaId && m.TransferenciaOferecidaEm != null).ToListAsync())
        {
            oferecido.TransferenciaOferecidaEm = null;
        }
        proximo.Papel = MembroCasa.PapelDono;
    }

    // Explícito, e não só pela cascata: o banco em memória dos testes não tem cascata.
    private static async Task ApagarCasaAsync(ApplicationDbContext contexto, int casaId)
    {
        contexto.ConvitesCasa.RemoveRange(await contexto.ConvitesCasa.Where(c => c.CasaId == casaId).ToListAsync());
        // As dietas dos membros guardam o SemanaCasaId sem chave estrangeira: continuam com as refeições como estão.
        contexto.SemanasCasa.RemoveRange(await contexto.SemanasCasa.Where(s => s.CasaId == casaId).ToListAsync());
        contexto.MembrosCasa.RemoveRange(await contexto.MembrosCasa.Where(m => m.CasaId == casaId).ToListAsync());
        var casa = await contexto.Casas.FirstOrDefaultAsync(c => c.Id == casaId);
        if (casa is not null)
        {
            contexto.Casas.Remove(casa);
        }
    }
}
