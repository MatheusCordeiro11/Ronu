namespace Ronu.Api.Services.IA;

/// <summary>
/// Calcula o gasto calórico de UMA sessão de treino, a partir do MET de
/// referência, peso e duração. Não recebe mais frequência semanal — quem
/// soma quantas vezes isso ocorre na semana é responsabilidade de quem chama
/// (agora contando dias específicos, não mais um número solto).
/// </summary>
public interface ICalculadoraGastoCalorico
{
    decimal CalcularGastoSessao(decimal metReferencia, decimal pesoKg, decimal duracaoHoras);
}
