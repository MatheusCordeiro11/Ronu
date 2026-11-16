using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Services.PlanoFamilia;

namespace Ronu.Api.Services.Conta;

/// <summary>
/// Apaga uma conta e tudo dela (LGPD, art. 18, VI): usada pelo "Apagar minha conta" e pela limpeza de conta sem
/// aceite (LimpezaRetencao). Cada tabela é apagada explicitamente, numa transação: ou sai tudo, ou nada (um SaveChanges
/// só, exceto quando o dono de uma casa com membros a passa adiante, que pede um a mais antes). A cascata do PostgreSQL fica como segunda garantia, e o banco em memória dos testes, que não
/// tem cascata, prova que a lista daqui está completa (ExclusaoContaTests confere toda entidade com UsuarioId).
///
/// Tabela nova com UsuarioId entra aqui. O teste por reflexão falha enquanto ela não entrar.
/// </summary>
public static class ExclusaoConta
{
    // Devolve falso se a conta não existe mais (outra aba apagou antes).
    public static async Task<bool> ApagarAsync(ApplicationDbContext contexto, int usuarioId)
    {
        var usuario = await contexto.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario is null)
        {
            return false;
        }

        // Plano família: a casa antes do resto, porque o dono com membros passa a casa adiante num SaveChanges próprio
        // (OperacoesCasa). A transação mantém o "ou sai tudo, ou nada" com os dois SaveChanges.
        await using var transacao = await OperacoesCasa.IniciarTransacaoAsync(contexto);
        try
        {
            await OperacoesCasa.SairParaExclusaoAsync(contexto, usuarioId);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra aba apagando a mesma conta já tirou o dono da casa.
            return false;
        }

        contexto.MembrosCasa.RemoveRange(await contexto.MembrosCasa.Where(m => m.UsuarioId == usuarioId).ToListAsync());
        contexto.ConvitesCasa.RemoveRange(await contexto.ConvitesCasa.Where(c => c.UsuarioId == usuarioId).ToListAsync());
        contexto.ListaComprasMarcacoes.RemoveRange(await contexto.ListaComprasMarcacoes.Where(m => m.UsuarioId == usuarioId).ToListAsync());
        contexto.PedidosRedefinicaoSenha.RemoveRange(await contexto.PedidosRedefinicaoSenha.Where(p => p.UsuarioId == usuarioId).ToListAsync());
        contexto.MetasDieta.RemoveRange(await contexto.MetasDieta.Where(m => m.UsuarioId == usuarioId).ToListAsync());
        contexto.RefeicoesFeitas.RemoveRange(await contexto.RefeicoesFeitas.Where(r => r.UsuarioId == usuarioId).ToListAsync());
        contexto.DietasIA.RemoveRange(await contexto.DietasIA.Where(d => d.UsuarioId == usuarioId).ToListAsync());
        contexto.PreferenciasAlimentares.RemoveRange(await contexto.PreferenciasAlimentares.Where(p => p.UsuarioId == usuarioId).ToListAsync());
        contexto.UsuarioModalidades.RemoveRange(await contexto.UsuarioModalidades.Where(m => m.UsuarioId == usuarioId).ToListAsync());
        contexto.ObjetivosUsuario.RemoveRange(await contexto.ObjetivosUsuario.Where(o => o.UsuarioId == usuarioId).ToListAsync());
        contexto.Consentimentos.RemoveRange(await contexto.Consentimentos.Where(c => c.UsuarioId == usuarioId).ToListAsync());
        contexto.AlergiasUsuario.RemoveRange(await contexto.AlergiasUsuario.Where(a => a.UsuarioId == usuarioId).ToListAsync());
        contexto.Usuarios.Remove(usuario);

        try
        {
            await contexto.SaveChangesAsync();
            if (transacao is not null)
            {
                await transacao.CommitAsync();
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            // Duas abas apagando ao mesmo tempo: a outra já apagou.
            return false;
        }

        return true;
    }
}
