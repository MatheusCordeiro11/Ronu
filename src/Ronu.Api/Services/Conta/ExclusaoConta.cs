using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;

namespace Ronu.Api.Services.Conta;

/// <summary>
/// Apaga uma conta e tudo dela (LGPD, art. 18, VI): usada pelo "Apagar minha conta" e pela limpeza de conta sem
/// aceite (LimpezaRetencao). Cada tabela é apagada explicitamente, e num SaveChanges só, que o EF faz numa transação:
/// ou sai tudo, ou nada. A cascata do PostgreSQL fica como segunda garantia, e o banco em memória dos testes, que não
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

        contexto.ListaComprasMarcacoes.RemoveRange(await contexto.ListaComprasMarcacoes.Where(m => m.UsuarioId == usuarioId).ToListAsync());
        contexto.PedidosRedefinicaoSenha.RemoveRange(await contexto.PedidosRedefinicaoSenha.Where(p => p.UsuarioId == usuarioId).ToListAsync());
        contexto.MetasDieta.RemoveRange(await contexto.MetasDieta.Where(m => m.UsuarioId == usuarioId).ToListAsync());
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
        }
        catch (DbUpdateConcurrencyException)
        {
            // Duas abas apagando ao mesmo tempo: a outra já apagou.
            return false;
        }

        return true;
    }
}
