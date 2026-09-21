namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de ICalculadoraGastoCalorico usando a fórmula padrão
/// baseada em MET: Calorias = MET × Peso(kg) × Tempo(horas).
/// </summary>
public class CalculadoraGastoCalorico : ICalculadoraGastoCalorico
{
    public decimal CalcularGastoSessao(decimal metReferencia, decimal pesoKg, decimal duracaoHoras)
    {
        return metReferencia * pesoKg * duracaoHoras;
    }
}
