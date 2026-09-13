namespace Ronu.Api.DTOs;

/// <summary>
/// Representação pública de uma PreferenciaAlimentar, retornada pela API.
/// </summary>
public class PreferenciaAlimentarResponse
{
    public int Id { get; set; }
    public required string Alimento { get; set; }
    public required string Tipo { get; set; }
}