using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.ListaCompras;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// Plano família, etapa 6 (decisão de 2026-10-26, "a lista da casa soma tudo"): a lista de compras da casa é a semana
/// inteira de todos os membros, ou seja, o almoço e o jantar da casa com a porção de CADA um e o café e os lanches de cada
/// um, somados por alimento numa lista só. Reaproveita a lista individual inteira (MontadorListaCompras: soma por
/// AlimentoId, cozido para cru, seções do mercado e arredondamento para cima), que recebe as dietas de todos como se
/// fossem uma só. O arredondamento é feito uma vez, sobre o total da casa, e não por pessoa.
///
/// Calculada na hora, a cada consulta: quem entra na casa passa a contar assim que tiver a dieta da semana, e quem sai
/// deixa de contar na hora (a linha dele em MembrosCasa some). Nada daqui é gravado, só o "já tenho" (ListaCasaMarcacao).
///
/// Entra a dieta atual (a mais recente, a mesma regra do GET /api/dietas/atual) de cada membro que seja da semana atual
/// da casa (SemanaCasaId). A de quem ainda não tem a semana na dieta (perfil incompleto, ou a entrada que não conseguiu
/// calcular as porções) fica de fora, e a resposta diz só que a lista não está completa, sem dizer de quem.
/// </summary>
public static class ListaComprasCasa
{
    public record Montada(SemanaCasa Semana, ListaComprasMontada Lista, bool Completa);

    /// <summary>A lista da semana atual da casa, ou nula se a casa ainda não tem semana.</summary>
    /// <param name="uf">A UF de quem está vendo: escolhe o nome regional dos itens (Aipim, Macaxeira).</param>
    public static async Task<Montada?> MontarAsync(ApplicationDbContext contexto, int casaId, string? uf)
    {
        var semana = await GeracaoSemanaCasa.AtualAsync(contexto, casaId);
        if (semana is null)
        {
            return null;
        }

        var membros = await contexto.MembrosCasa.AsNoTracking()
            .Where(m => m.CasaId == casaId)
            .Select(m => m.UsuarioId)
            .ToListAsync();

        // No máximo 3 dietas por pessoa (o histórico) e 6 pessoas por casa: lê todas e escolhe a atual de cada um aqui.
        var linhas = await contexto.DietasIA.AsNoTracking()
            .Where(d => membros.Contains(d.UsuarioId))
            .Select(d => new { d.UsuarioId, d.Id, d.DataGeracao, d.SemanaCasaId, d.ConteudoJson })
            .ToListAsync();

        var dietas = new List<DietaSemanalDto>();
        var completa = true;
        foreach (var usuarioId in membros)
        {
            var atual = linhas
                .Where(d => d.UsuarioId == usuarioId)
                .OrderByDescending(d => d.DataGeracao).ThenByDescending(d => d.Id)
                .FirstOrDefault();
            if (atual is null || atual.SemanaCasaId != semana.Id)
            {
                completa = false;
                continue;
            }

            dietas.Add(JsonSerializer.Deserialize<DietaSemanalDto>(atual.ConteudoJson)!);
        }

        return new Montada(semana, Somar(dietas, uf), completa);
    }

    /// <summary>
    /// A soma das dietas numa lista só. Pura: junta os dias de todas numa dieta e passa pelo mesmo montador da lista
    /// individual, que agrupa por AlimentoId (e o item de compra por ConversaoCompras). Nenhuma origem diz de quem veio.
    /// </summary>
    public static ListaComprasMontada Somar(IReadOnlyList<DietaSemanalDto> dietas, string? uf) =>
        MontadorListaCompras.Montar(new DietaSemanalDto { Dias = dietas.SelectMany(d => d.Dias).ToList() }, uf);

    /// <summary>"Lista de compras da casa · semana de 26/10": a data em que a semana foi gerada, no fuso da UF.</summary>
    public static string TituloCompartilhar(DateTime geradaEmUtc, string? uf) =>
        $"Lista de compras da casa · semana de {FusoHorarioEstado.Hoje(uf, geradaEmUtc).ToString("dd/MM", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// As marcações da casa que valem agora: a do item que ainda está na lista e cuja quantidade não passou da que foi
    /// marcada. A que deixou de valer não é apagada: se a quantidade voltar a caber, ela volta (a casa tem aquilo).
    /// </summary>
    public static HashSet<string> Marcados(IEnumerable<(string Chave, decimal Quantidade)> marcacoes, ListaComprasMontada lista)
    {
        var atuais = lista.Secoes.SelectMany(s => s.Itens).ToDictionary(i => i.Chave, i => i.Quantidade);
        return marcacoes
            .Where(m => atuais.TryGetValue(m.Chave, out var agora) && agora <= m.Quantidade)
            .Select(m => m.Chave)
            .ToHashSet();
    }
}
