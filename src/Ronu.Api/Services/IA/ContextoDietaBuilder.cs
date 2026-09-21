using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de IContextoDietaBuilder que busca os dados do usuário
/// diretamente no banco via EF Core.
/// </summary>
public class ContextoDietaBuilder : IContextoDietaBuilder
{
    private readonly ApplicationDbContext _context;

    public ContextoDietaBuilder(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ContextoDietaDto> ConstruirAsync(int usuarioId)
    {
        var usuario = await _context.Usuarios
            .FirstAsync(u => u.Id == usuarioId);

        var objetivo = await _context.ObjetivosUsuario
            .Where(o => o.UsuarioId == usuarioId)
            .OrderByDescending(o => o.DataRegistro)
            .FirstAsync();

        var modalidades = await _context.UsuarioModalidades
            .Where(m => m.UsuarioId == usuarioId)
            .Include(m => m.Modalidade)
            .Select(m => new ModalidadeContextoDto
            {
                Nome = m.Modalidade.Nome,
                MetReferencia = m.Modalidade.MetReferencia,
                DuracaoHoras = m.DuracaoMediaHoras,
                DiasSemana = m.DiasSemana
            })
            .ToListAsync();

        var preferencias = await _context.PreferenciasAlimentares
            .Where(p => p.UsuarioId == usuarioId)
            .Select(p => new PreferenciaContextoDto
            {
                Alimento = p.Alimento,
                Tipo = p.Tipo
            })
            .ToListAsync();

        var idade = DateTime.UtcNow.Year - usuario.DataNascimento!.Value.Year;
        if (DateOnly.FromDateTime(DateTime.UtcNow) < usuario.DataNascimento.Value.AddYears(idade))
        {
            idade--;
        }

        return new ContextoDietaDto
        {
            Altura = usuario.Altura!.Value,
            Sexo = usuario.Sexo!,
            Idade = idade,
            Peso = objetivo.Peso,
            Objetivo = objetivo.Objetivo,
            Modalidades = modalidades,
            Preferencias = preferencias
        };
    }
}
