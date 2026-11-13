using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// O ponto único que monta a lista fechada de uma pessoa (TACO 2c): lê do banco a UF, as preferências e as alergias e
/// chama o ListaAlimentosPessoa.Montar. Geração, conferência das preferências e troca de alimento passam por aqui, e um
/// teste varre o código para o Montar não ser chamado de nenhum outro lugar. Antes da 2c, cada chamada lia as
/// preferências por conta própria, e nenhuma passava as alergias: um lugar novo esqueceria de novo.
///
/// É também o único lugar que lê as alergias (AlergiasAsync), para quem não monta lista mas precisa delas: a lista de
/// compras e o aviso da dieta atual. Toda leitura filtra pelo usuário do token que quem chama recebeu.
/// </summary>
public static class ListaDaPessoa
{
    /// <param name="Preferencias">As preferências na ordem do Id, a mesma de Lista.Preferencias.</param>
    /// <param name="Alergias">Os códigos dos chips marcados, na ordem da tela.</param>
    public record Resultado(ListaAlimentos Lista, string? Uf, IReadOnlyList<PreferenciaAlimentar> Preferencias, IReadOnlyList<string> Alergias);

    public static async Task<Resultado> MontarAsync(ApplicationDbContext contexto, int usuarioId)
    {
        var uf = await contexto.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.Estado)
            .FirstOrDefaultAsync();

        var preferencias = await contexto.PreferenciasAlimentares.AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .OrderBy(p => p.Id)
            .ToListAsync();

        var alergias = await AlergiasAsync(contexto, usuarioId);

        var lista = ListaAlimentosPessoa.Montar(
            uf,
            preferencias.Select(p => new PreferenciaContextoDto { Alimento = p.Alimento, Tipo = p.Tipo }),
            CodigosAlergia.Marcas(alergias));

        return new Resultado(lista, uf, preferencias, alergias);
    }

    /// <summary>Os códigos marcados pela pessoa, na ordem dos chips.</summary>
    public static async Task<IReadOnlyList<string>> AlergiasAsync(ApplicationDbContext contexto, int usuarioId)
    {
        var codigos = await contexto.AlergiasUsuario.AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId)
            .Select(a => a.Codigo)
            .ToListAsync();

        return CodigosAlergia.Chips.Select(c => c.Codigo).Where(codigos.Contains).ToList();
    }
}
