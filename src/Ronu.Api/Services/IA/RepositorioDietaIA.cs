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

        var novaDieta = new DietaIA
        {
            UsuarioId = usuarioId,
            DataGeracao = DateTime.UtcNow,
            ConteudoJson = JsonSerializer.Serialize(dieta)
        };

        _context.DietasIA.Add(novaDieta);
        await _context.SaveChangesAsync();
    }
}
