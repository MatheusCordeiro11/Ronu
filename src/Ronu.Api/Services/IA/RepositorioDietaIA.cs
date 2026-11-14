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

    public async Task SalvarAsync(int usuarioId, DietaSemanalDto dieta, int? semanaCasaId = null)
    {
        await AbrirEspacoAsync(_context, usuarioId);

        // A dieta e as metas dela vão juntas (ou nenhuma das duas): a MetaDieta
        // precisa do Id da DietaIA, que só existe depois do primeiro SaveChanges.
        await using var transacao = await _context.Database.BeginTransactionAsync();
        await GravarAsync(_context, usuarioId, dieta, DateTime.UtcNow, DietaIA.OrigemPessoa, semanaCasaId);
        await transacao.CommitAsync();
    }

    /// <summary>
    /// Marca para remoção as dietas mais antigas, até sobrar espaço para uma nova (sem gravar: vai no próximo
    /// SaveChanges). Compartilhado com a semana da casa, que cria uma dieta para cada membro.
    /// </summary>
    public static async Task AbrirEspacoAsync(ApplicationDbContext contexto, int usuarioId)
    {
        var dietasExistentes = await contexto.DietasIA
            .Where(d => d.UsuarioId == usuarioId)
            .OrderBy(d => d.DataGeracao)
            .ToListAsync();

        // Se já está no limite (ou acima, por algum motivo), remove as mais
        // antigas até sobrar espaço para a nova — normalmente remove só 1,
        // mas o Where cobre também um eventual excedente inesperado.
        if (dietasExistentes.Count >= LimiteDietasPorUsuario)
        {
            var quantidadeParaRemover = dietasExistentes.Count - LimiteDietasPorUsuario + 1;
            var maisAntigas = dietasExistentes.Take(quantidadeParaRemover).ToList();
            contexto.DietasIA.RemoveRange(maisAntigas);

            // As marcações "já tenho" dessas dietas saem junto. O banco já apaga em cascata; apagar aqui
            // também deixa a regra explícita e vale no banco em memória dos testes, que não tem cascata.
            var idsRemovidos = maisAntigas.Select(d => d.Id).ToList();
            contexto.ListaComprasMarcacoes.RemoveRange(
                contexto.ListaComprasMarcacoes.Where(m => idsRemovidos.Contains(m.DietaIAId)));
        }
    }

    /// <summary>A dieta e a MetaDieta dela, em dois SaveChanges (a meta precisa do Id da dieta). A transação é de quem chama.</summary>
    public static async Task<DietaIA> GravarAsync(
        ApplicationDbContext contexto, int usuarioId, DietaSemanalDto dieta, DateTime dataGeracao, string origem, int? semanaCasaId)
    {
        var novaDieta = new DietaIA
        {
            UsuarioId = usuarioId,
            DataGeracao = dataGeracao,
            ConteudoJson = JsonSerializer.Serialize(dieta),
            Origem = origem,
            SemanaCasaId = semanaCasaId
        };

        contexto.DietasIA.Add(novaDieta);
        await contexto.SaveChangesAsync();

        var meta = MontarMetaDieta(usuarioId, dataGeracao, dieta);
        meta.DietaIAId = novaDieta.Id;
        contexto.MetasDieta.Add(meta);
        await contexto.SaveChangesAsync();
        return novaDieta;
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
            FatorAtividade = dieta.FatorAtividade ?? CalculadoraManutencao.FatorPadrao,
            PercentualAjusteAdaptativo = dieta.AjusteAdaptativo?.Percentual
        };
    }
}
