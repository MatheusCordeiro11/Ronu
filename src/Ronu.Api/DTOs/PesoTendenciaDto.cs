namespace Ronu.Api.DTOs;

/// <summary>
/// Ponto do gráfico de peso: o valor bruto registrado e o valor suavizado
/// (tendência) calculado a partir dele. Os dois são expostos juntos para o
/// frontend renderizar a linha de tendência sólida sobre os pontos brutos
/// esmaecidos (padrão usado por apps como MacroFactor/Libra).
/// </summary>
public class PesoTendenciaDto
{
    public required DateTime Data { get; set; }
    public required decimal PesoBruto { get; set; }
    public required decimal PesoTendencia { get; set; }
}
