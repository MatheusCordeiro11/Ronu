namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa uma preferência ou restrição alimentar do usuário, permitindo
/// que a IA personalize os alimentos incluídos ou evitados no cardápio.
/// </summary>
public class PreferenciaContextoDto
{
    public required string Alimento { get; set; }
    public required string Tipo { get; set; }
}
