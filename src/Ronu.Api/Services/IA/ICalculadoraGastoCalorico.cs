namespace Ronu.Api.Services.IA;

/// <summary>
/// Calcula o gasto calórico de uma modalidade praticada pelo usuário, a
/// partir do MET de referência, peso e frequência semanal. Responsabilidade
/// única: calcula uma modalidade por vez — a soma entre várias modalidades
/// (quando o usuário pratica mais de uma) é responsabilidade de quem chama.
/// </summary>
public interface ICalculadoraGastoCalorico
{
    decimal CalcularGastoSemanal(decimal metReferencia, decimal pesoKg, int frequenciaSemanal);
}
