namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa a dieta semanal gerada pela IA. Não tem mais uma meta única
/// para a semana toda — cada DiaDietaDto carrega sua própria MetaCalculada,
/// já que dias de treino têm meta diferente de dias de descanso.
/// </summary>
public class DietaSemanalDto
{
    public required List<DiaDietaDto> Dias { get; set; }

    // Como a meta adaptativa atuou nesta dieta (registro histórico, para o
    // dashboard). Nulo em dietas geradas antes da meta adaptativa existir.
    public AjusteAdaptativoDto? AjusteAdaptativo { get; set; }
}
