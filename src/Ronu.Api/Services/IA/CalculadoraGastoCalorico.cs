namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de ICalculadoraGastoCalorico com o MET líquido:
/// Calorias = (MET − 1) × Peso(kg) × Tempo(horas).
/// O MET do Compêndio de Atividades Físicas é bruto — inclui o repouso
/// (1 MET ≈ 1 kcal/kg/h). Como o repouso do dia inteiro já está na TMB
/// (CalculadoraManutencao), somar o MET bruto contaria as horas de treino
/// duas vezes; o Compêndio recomenda descontar 1 MET nesse caso.
/// </summary>
public class CalculadoraGastoCalorico : ICalculadoraGastoCalorico
{
    private const decimal MetRepouso = 1m;

    public decimal CalcularGastoSessao(decimal metReferencia, decimal pesoKg, decimal duracaoHoras)
    {
        return Math.Max(0m, metReferencia - MetRepouso) * pesoKg * duracaoHoras;
    }
}
