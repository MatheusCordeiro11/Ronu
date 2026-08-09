namespace Ronu.Api.DTOs;

public class ObjetivoResponse
{
    public int Id { get; set; }
    public decimal Peso { get; set; }
    public required string Objetivo { get; set; }
    public DateTime DataRegistro { get; set; }
}