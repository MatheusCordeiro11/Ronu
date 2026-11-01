using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;

namespace Ronu.Api.Services.Conta;

/// <summary>
/// Os prazos de guarda de docs/privacidade.md que a API cumpre sozinha:
/// - pedidos de redefinição de senha com mais de 30 dias saem (o limite por conta só olha as últimas 24 horas, e o
///   link vale 30 minutos: depois disso o pedido só serviria para investigar abuso recente);
/// - conta que nunca aceitou o consentimento geral sai depois de 30 dias, mas só quando as três coisas valem (decisão
///   de 2026-11-01): nunca aceitou nenhuma versão; não tem nenhum dado além de nome e email (nem perfil, nem UF, nem
///   linha em outra tabela); e CriadoEm é conhecido e passou de 30 dias. As contas anteriores ao CriadoEm ficam com
///   ele nulo e nunca entram: muitas não têm aceite porque o consentimento não existia, e algumas têm peso e dietas.
/// </summary>
public static class LimpezaRetencao
{
    public static readonly TimeSpan PrazoPedidosRedefinicao = TimeSpan.FromDays(30);
    public static readonly TimeSpan PrazoContaSemAceite = TimeSpan.FromDays(30);

    public record Resultado(int PedidosApagados, int ContasApagadas);

    public static async Task<Resultado> ExecutarAsync(ApplicationDbContext contexto, DateTime agora)
    {
        var limitePedidos = agora - PrazoPedidosRedefinicao;
        var pedidos = await contexto.PedidosRedefinicaoSenha.Where(p => p.CriadoEm < limitePedidos).ToListAsync();
        contexto.PedidosRedefinicaoSenha.RemoveRange(pedidos);
        await contexto.SaveChangesAsync();

        var limiteContas = agora - PrazoContaSemAceite;
        var contas = await contexto.Usuarios
            .Where(u => u.CriadoEm != null && u.CriadoEm < limiteContas)
            .Where(u => !contexto.Consentimentos.Any(c => c.UsuarioId == u.Id))
            .Where(u => u.Altura == null && u.DataNascimento == null && u.Sexo == null && u.RotinaDiaria == null
                && u.OrcamentoSemanal == null && u.NivelAtividade == null && (u.Estado == null || u.Estado == ""))
            .Where(u => !contexto.ObjetivosUsuario.Any(x => x.UsuarioId == u.Id)
                && !contexto.UsuarioModalidades.Any(x => x.UsuarioId == u.Id)
                && !contexto.PreferenciasAlimentares.Any(x => x.UsuarioId == u.Id)
                && !contexto.DietasIA.Any(x => x.UsuarioId == u.Id)
                && !contexto.MetasDieta.Any(x => x.UsuarioId == u.Id)
                && !contexto.ListaComprasMarcacoes.Any(x => x.UsuarioId == u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        var apagadas = 0;
        foreach (var id in contas)
        {
            if (await ExclusaoConta.ApagarAsync(contexto, id))
            {
                apagadas++;
            }
        }

        return new Resultado(pedidos.Count, apagadas);
    }
}
