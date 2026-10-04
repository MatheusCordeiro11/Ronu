using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using System.Text.Json;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de IRepositorioDietaIA usando EF Core.
/// </summary>
public class RepositorioDietaIA : IRepositorioDietaIA
{
    private const int LimiteDietasPorUsuario = 3;

    private readonly ApplicationDbContext _context;

    public RepositorioDietaIA(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SalvarAsync(int usuarioId, DietaSemanalDto dieta)
    {
        var dietasExistentes = await _context.DietasIA
            .Where(d => d.UsuarioId == usuarioId)
            .OrderBy(d => d.DataGeracao)
            .ToListAsync();

        // Se já está no limite (ou acima, por algum motivo), remove as mais
        // antigas até sobrar espaço para a nova — normalmente remove só 1,
        // mas o Where cobre também um eventual excedente inesperado.
        if (dietasExistentes.Count >= LimiteDietasPorUsuario)
        {
            var quantidadeParaRemover = dietasExistentes.Count - LimiteDietasPorUsuario + 1;
            var maisAntigas = dietasExistentes.Take(quantidadeParaRemover);
            _context.DietasIA.RemoveRange(maisAntigas);
        }

        // A dieta e as metas dela vão juntas (ou nenhuma das duas): a MetaDieta
        // precisa do Id da DietaIA, que só existe depois do primeiro SaveChanges.
        await using var transacao = await _context.Database.BeginTransactionAsync();

        var dataGeracao = DateTime.UtcNow;
        var novaDieta = new DietaIA
        {
            UsuarioId = usuarioId,
            DataGeracao = dataGeracao,
            ConteudoJson = JsonSerializer.Serialize(dieta)
        };

        _context.DietasIA.Add(novaDieta);
        await _context.SaveChangesAsync();

        var meta = MontarMetaDieta(usuarioId, dataGeracao, dieta);
        meta.DietaIAId = novaDieta.Id;
        _context.MetasDieta.Add(meta);
        await _context.SaveChangesAsync();

        await transacao.CommitAsync();
    }

    /// <summary>
    /// As metas de uma dieta para a tabela MetasDieta (sem o DietaIAId, que só
    /// existe depois de salvar). Os dias vão pela ordem segunda ... domingo,
    /// casados PELO NOME — a lista da IA pode vir em outra ordem.
    /// </summary>
    public static MetaDieta MontarMetaDieta(int usuarioId, DateTime dataGeracao, DietaSemanalDto dieta)
    {
        var metas = new decimal[GeradorDietaGemini.NomesDias.Length];

        foreach (var dia in dieta.Dias)
        {
            var indice = Array.FindIndex(GeradorDietaGemini.NomesDias,
                n => string.Equals(n, dia.DiaSemana.Trim(), StringComparison.OrdinalIgnoreCase));

            // O GeradorDietaGemini já garante os 7 nomes, sem repetição.
            if (indice < 0)
            {
                throw new InvalidOperationException($"Dia '{dia.DiaSemana}' fora dos 7 dias esperados.");
            }

            metas[indice] = dia.MetaCalculada.Calorias;
        }

        return new MetaDieta
        {
            UsuarioId = usuarioId,
            DataGeracao = dataGeracao,
            VersaoFormula = CalculadoraManutencao.VersaoFormula,
            MetasPorDia = metas,
            ManutencoesPorDia = dieta.ManutencaoPorDia,
            PercentualAjusteAdaptativo = dieta.AjusteAdaptativo?.Percentual
        };
    }
}
