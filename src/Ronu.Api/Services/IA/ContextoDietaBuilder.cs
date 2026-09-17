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
        // FirstAsync (não FirstOrDefaultAsync) de propósito: quem chama este
        // método (DietasController) já valida antes que o usuário tem perfil
        // completo e pelo menos um objetivo registrado. Se isso falhar aqui,
        // é inconsistência real, não caso esperado.
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
                FrequenciaSemanal = m.FrequenciaSemanal,
                MetReferencia = m.Modalidade.MetReferencia
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

        // Idade calculada a partir da DataNascimento porque o valor real
        // muda a cada aniversário — armazenar uma "Idade" fixa no banco
        // ficaria desatualizado com o tempo.
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
