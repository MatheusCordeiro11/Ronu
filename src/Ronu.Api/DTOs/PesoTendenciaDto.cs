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

    // Do registro de origem (ObjetivoUsuario), para a tabela "Com a dieta" e
    // o contador "5 pesagens com o mesmo objetivo" do Progresso. Só
    // acrescentam campos: o frontend antigo ignora.
    public int? Id { get; set; }
    public string? Objetivo { get; set; }
    public string? Aderencia { get; set; }
}
