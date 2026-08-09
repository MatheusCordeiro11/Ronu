namespace Ronu.Api.DTOs;

public class PreferenciaAlimentarResponse
{
    public int Id { get; set; }
    public required string Alimento { get; set; }
    public required string Tipo { get; set; }
}