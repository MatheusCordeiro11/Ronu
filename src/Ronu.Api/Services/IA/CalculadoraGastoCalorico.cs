namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de ICalculadoraGastoCalorico usando a fórmula padrão
/// baseada em MET: Calorias = MET × Peso(kg) × Tempo(horas). A duração da
/// sessão agora vem de dado real por modalidade/usuário (antes era uma
/// constante fixa de 1h para todos — lacuna documentada, agora resolvida).
/// </summary>
public class CalculadoraGastoCalorico : ICalculadoraGastoCalorico
{
    public decimal CalcularGastoSemanal(decimal metReferencia, decimal pesoKg, int frequenciaSemanal, decimal duracaoHoras)
    {
        var caloriasPorSessao = metReferencia * pesoKg * duracaoHoras;
        return caloriasPorSessao * frequenciaSemanal;
    }
}
