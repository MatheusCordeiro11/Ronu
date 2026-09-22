namespace Ronu.Api.DTOs;

/// <summary>
/// Um registro de peso bruto num ponto no tempo — entrada para o cálculo do
/// peso de tendência (suavização).
/// </summary>
public class RegistroPesoDto
{
    public required DateTime Data { get; set; }
    public required decimal Peso { get; set; }
}
