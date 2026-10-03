namespace Ronu.Api.Models.IA;

/// <summary>
/// Um registro diário de peso com a resposta de aderência daquele dia e o
/// objetivo vigente quando foi feito — entrada da meta calórica adaptativa
/// (ICalculadoraAjusteAdaptativo). Usado só no cálculo da meta, nunca vai
/// para o prompt da IA.
/// </summary>
public class RegistroPesoAderenciaDto
{
    public required DateTime Data { get; set; }
    public required decimal Peso { get; set; }

    // "seguiu", "comeu_mais", "comeu_menos", "nao_seguiu" ou null (sem resposta).
    public string? Aderencia { get; set; }

    public required string Objetivo { get; set; }
}
