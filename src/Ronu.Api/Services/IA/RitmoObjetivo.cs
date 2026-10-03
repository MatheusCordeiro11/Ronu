namespace Ronu.Api.Services.IA;

/// <summary>
/// Ritmo de mudança de peso que cada objetivo pede. Fonte única para a meta
/// da fórmula (CalculadoraManutencao, que converte o ritmo em kcal/dia) e para
/// a meta adaptativa (CalculadoraAjusteAdaptativo, que compara o ritmo real
/// com este). Antes, a fórmula usava ±15% das calorias e a adaptativa este
/// ritmo — os dois discordavam, e a adaptativa corrigia uma fórmula certa.
/// </summary>
public static class RitmoObjetivo
{
    // 1 kg de gordura ≈ 7700 kcal.
    public const decimal KcalPorKg = 7700m;

    // Fração do peso corporal por semana. Perder 0,5%/semana fica na ponta
    // conservadora do recomendado para preservar massa magra (Helms et al.
    // 2014: 0,5–1%/semana).
    public const decimal PerderPorSemana = -0.005m;
    public const decimal GanharPorSemana = 0.0025m;

    /// <summary>Ritmo esperado em kg/semana (negativo = perder). 0 para manter.</summary>
    public static decimal KgPorSemana(string objetivo, decimal pesoKg) => objetivo switch
    {
        "perder peso" => PerderPorSemana * pesoKg,
        "ganhar peso" => GanharPorSemana * pesoKg,
        _ => 0m
    };

    /// <summary>
    /// O mesmo ritmo em kcal por dia (negativo = déficit): perder dá
    /// −5,5 kcal por kg de peso por dia; ganhar, +2,75.
    /// </summary>
    public static decimal KcalPorDia(string objetivo, decimal pesoKg) =>
        KgPorSemana(objetivo, pesoKg) * KcalPorKg / 7m;
}
