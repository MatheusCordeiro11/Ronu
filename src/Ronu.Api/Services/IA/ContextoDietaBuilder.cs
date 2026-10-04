using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de IContextoDietaBuilder que busca os dados do usuário
/// diretamente no banco via EF Core.
/// </summary>
public class ContextoDietaBuilder : IContextoDietaBuilder
{
    private readonly ApplicationDbContext _context;
    private readonly ICalculadoraPesoTendencia _calculadoraPesoTendencia;

    public ContextoDietaBuilder(ApplicationDbContext context, ICalculadoraPesoTendencia calculadoraPesoTendencia)
    {
        _context = context;
        _calculadoraPesoTendencia = calculadoraPesoTendencia;
    }

    /// <summary>
    /// Peso das contas da dieta (TMB, treino, objetivo, macros e prompt): o
    /// peso de tendência mais recente, não a última pesagem — a pesagem do dia
    /// oscila com água e sal, e a meta oscilaria junto. A tendência sempre
    /// existe com pelo menos um registro (a EMA começa no primeiro peso, então
    /// com um registro só ela é o próprio peso); a última pesagem bruta fica só
    /// como defesa, se a calculadora devolver vazio. Histórico em ordem de data.
    /// </summary>
    public static decimal PesoDeTendencia(
        IReadOnlyList<RegistroPesoAderenciaDto> historico, ICalculadoraPesoTendencia calculadora)
    {
        var tendencia = calculadora.Calcular(
            historico.Select(r => new RegistroPesoDto { Data = r.Data, Peso = r.Peso }).ToList());

        return tendencia.Count > 0
            ? Math.Round(tendencia[^1].PesoTendencia, 1, MidpointRounding.AwayFromZero)
            : historico[^1].Peso;
    }

    public async Task<ContextoDietaDto> ConstruirAsync(int usuarioId)
    {
        var usuario = await _context.Usuarios
            .FirstAsync(u => u.Id == usuarioId);

        // Histórico inteiro de peso (um registro por dia, pelo upsert do
        // ObjetivosController): a meta adaptativa e o peso de tendência precisam
        // da série completa para a tendência assentar. O mais recente continua
        // sendo a fonte do objetivo atual.
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

        // Metas de todas as dietas já geradas (tabela pequena, ~1 linha por
        // dieta): a meta adaptativa escolhe a dieta ativa de cada dia da janela.
        var historicoMetas = await _context.MetasDieta
            .Where(m => m.UsuarioId == usuarioId)
            .OrderBy(m => m.DataGeracao)
            .Select(m => new MetaDietaRegistroDto
            {
                DataGeracao = m.DataGeracao,
                VersaoFormula = m.VersaoFormula,
                MetasPorDia = m.MetasPorDia,
                ManutencoesPorDia = m.ManutencoesPorDia
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
            Peso = PesoDeTendencia(historicoPeso, _calculadoraPesoTendencia),
            Objetivo = objetivo.Objetivo,
            Modalidades = modalidades,
            Preferencias = preferencias,
            Estado = usuario.Estado,
            RotinaDiaria = usuario.RotinaDiaria,
            OrcamentoSemanal = usuario.OrcamentoSemanal,
            HistoricoPeso = historicoPeso,
            HistoricoMetas = historicoMetas
        };
    }
}
