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

        // Histórico inteiro de peso (um registro por dia, pelo upsert do
        // ObjetivosController): a meta adaptativa precisa da série completa para
        // a tendência assentar antes da janela recente. O mais recente continua
        // sendo a fonte do peso e do objetivo atuais.
        var historicoPeso = await _context.ObjetivosUsuario
            .Where(o => o.UsuarioId == usuarioId)
            .OrderBy(o => o.DataRegistro)
            .Select(o => new RegistroPesoAderenciaDto
            {
                Data = o.DataRegistro,
                Peso = o.Peso,
                Aderencia = o.Aderencia,
                Objetivo = o.Objetivo
            })
            .ToListAsync();

        // O DietasController já exige um objetivo antes de chegar aqui; mesma
        // exceção que o FirstAsync anterior lançaria se não houvesse nenhum.
        if (historicoPeso.Count == 0)
        {
            throw new InvalidOperationException("Usuário sem nenhum objetivo/peso registrado.");
        }

        var objetivo = historicoPeso[^1];

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
            Preferencias = preferencias,
            Estado = usuario.Estado,
            RotinaDiaria = usuario.RotinaDiaria,
            OrcamentoSemanal = usuario.OrcamentoSemanal,
            HistoricoPeso = historicoPeso
        };
    }
}
