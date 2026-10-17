namespace Ronu.Api.DTOs;

/// <summary>
/// Resposta do POST /api/metas/simulacao: a meta da fórmula de cada dia e as
/// suas partes, em kcal inteiras, sem a meta adaptativa (que depende do
/// histórico e só existe numa dieta gerada). Cada parte é arredondada uma vez
/// e repetida igual em todo lugar da resposta, e cada total é a soma das
/// partes mostradas, então nada na tela fica com 1 kcal de diferença.
/// </summary>
public class SimulacaoMetaResponse
{
    public int VersaoFormula { get; set; }
    public required EntradasSimulacaoDto Entradas { get; set; }
    public required GastoRepousoSimulacaoDto GastoRepouso { get; set; }
    public required AtividadeSimulacaoDto AtividadeForaDoTreino { get; set; }

    // Negativo = perder. 0 para manter.
    public decimal RitmoEsperadoKgSemana { get; set; }

    public required List<ModalidadeSimulacaoDto> Modalidades { get; set; }

    // Sempre 7, de 1 = segunda a 7 = domingo.
    public required List<DiaSimulacaoDto> Dias { get; set; }
}

/// <summary>Os valores usados na conta, informados ou salvos.</summary>
public class EntradasSimulacaoDto
{
    public required string Sexo { get; set; }
    public decimal Altura { get; set; }
    public DateOnly DataNascimento { get; set; }
    public int Idade { get; set; }
    public decimal Peso { get; set; }

    // "informado" (veio no pedido) ou "tendencia" (peso de tendência salvo).
    public required string PesoOrigem { get; set; }
    public required string Objetivo { get; set; }
}

/// <summary>
/// TMB (Mifflin-St Jeor) e as suas parcelas: 10 × peso, 6,25 × altura,
/// −5 × idade e +5 (masculino) ou −161 (feminino). As parcelas somam Total.
/// </summary>
public class GastoRepousoSimulacaoDto
{
    public int Total { get; set; }
    public int Peso { get; set; }
    public int Altura { get; set; }
    public int Idade { get; set; }
    public int Sexo { get; set; }
}

public class AtividadeSimulacaoDto
{
    public decimal Fator { get; set; }

    // TMB × (Fator − 1): o dia a dia fora do treino.
    public int Kcal { get; set; }
}

public class ModalidadeSimulacaoDto
{
    public int ModalidadeId { get; set; }
    public required string Nome { get; set; }
    public decimal MetReferencia { get; set; }
    public decimal DuracaoHoras { get; set; }
    public required int[] DiasSemana { get; set; }

    // (MET − 1) × peso × horas.
    public int KcalSessao { get; set; }
}

public class DiaSimulacaoDto
{
    public int DiaSemana { get; set; }

    // Soma das KcalSessao das modalidades deste dia.
    public int TreinoKcal { get; set; }

    // GastoRepouso.Total + AtividadeForaDoTreino.Kcal + TreinoKcal.
    public int Manutencao { get; set; }

    // Parcela do objetivo, já com o teto do déficit (25% da manutenção).
    public int ObjetivoKcal { get; set; }
    public bool TetoDeficitAplicado { get; set; }

    // Manutencao + ObjetivoKcal.
    public int MetaFormula { get; set; }
}
