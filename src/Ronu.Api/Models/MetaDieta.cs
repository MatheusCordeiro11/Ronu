using Microsoft.EntityFrameworkCore;

namespace Ronu.Api.Models;

/// <summary>
/// As metas calóricas de uma dieta gerada, guardadas à parte da DietaIA para
/// a meta adaptativa saber o que a pessoa recebeu para comer em cada dia da
/// janela de 28 dias. A DietaIA guarda só as 3 mais recentes (regra da tela);
/// esta tabela guarda todas, e é pequena (~150 bytes por dieta).
/// </summary>
[Index(nameof(UsuarioId), nameof(DataGeracao))]
public class MetaDieta
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    // Só para rastrear de qual dieta veio. Sem chave estrangeira de propósito:
    // a regra das 3 dietas apaga a DietaIA e esta linha precisa continuar.
    public int? DietaIAId { get; set; }

    // O mesmo instante gravado na DietaIA.
    public DateTime DataGeracao { get; set; }

    // Versão da fórmula de manutenção (CalculadoraManutencao.VersaoFormula)
    // usada nesta dieta. 1 = antes da revisão de 2026-10-03.
    public int VersaoFormula { get; set; }

    // MetaCalculada.Calorias de cada dia (com ajuste adaptativo e piso), o que
    // a pessoa recebeu para comer. Índice 0 = segunda ... 6 = domingo.
    public decimal[] MetasPorDia { get; set; } = Array.Empty<decimal>();

    // Manutenção da fórmula de cada dia (sem o objetivo), mesma ordem. Nulo
    // quando desconhecida (dietas anteriores a esta tabela).
    public decimal[]? ManutencoesPorDia { get; set; }

    // Percentual da meta adaptativa aplicado nesta dieta. Só registro: o
    // cálculo não usa (nulo nas dietas anteriores à meta adaptativa).
    public decimal? PercentualAjusteAdaptativo { get; set; }
}
