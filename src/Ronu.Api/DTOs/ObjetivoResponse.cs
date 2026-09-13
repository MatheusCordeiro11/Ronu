namespace Ronu.Api.DTOs;

/// <summary>
/// Representação pública de um ObjetivoUsuario, retornada pela API.
/// </summary>
public class ObjetivoResponse
{
    public int Id { get; set; }
    public decimal Peso { get; set; }
    public required string Objetivo { get; set; }
    public DateTime DataRegistro { get; set; }
}