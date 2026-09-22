namespace Ronu.Api.Services;

/// <summary>
/// Calcula o peso de tendência (suavizado) a partir do histórico de peso
/// bruto de um usuário, usando a variante "time-aware" da média móvel
/// exponencial (baseada no Hacker's Diet, adaptada para intervalos
/// irregulares entre registros — Ronu não força pesagem diária).
/// Responsabilidade única: só sabe suavizar uma série de peso, nada sobre
/// banco de dados, dieta ou objetivo.
/// </summary>
public interface ICalculadoraPesoTendencia
{
    List<Ronu.Api.DTOs.PesoTendenciaDto> Calcular(List<Ronu.Api.DTOs.RegistroPesoDto> registros);
}
