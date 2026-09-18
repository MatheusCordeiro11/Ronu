namespace Ronu.Api.Services.IA;

/// <summary>
/// Implementação de ICalculadoraGastoCalorico usando a fórmula padrão
/// baseada em MET: Calorias = MET × Peso(kg) × Tempo(horas).
/// </summary>
public class CalculadoraGastoCalorico : ICalculadoraGastoCalorico
{
    // MVP: duração de sessão fixa. V2: duração configurável por modalidade
    // (registrado como lacuna conhecida do projeto).
    private const decimal DuracaoSessaoHorasMvp = 1m;

    public decimal CalcularGastoSemanal(decimal metReferencia, decimal pesoKg, int frequenciaSemanal)
    {
        var caloriasPorSessao = metReferencia * pesoKg * DuracaoSessaoHorasMvp;
        return caloriasPorSessao * frequenciaSemanal;
    }
}
