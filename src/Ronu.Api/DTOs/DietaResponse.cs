using Ronu.Api.Models.IA;

namespace Ronu.Api.DTOs;

/// <summary>
/// Envolve uma dieta gerada com a data em que foi gerada, para exibição
/// no frontend (a dieta em si não carrega essa informação).
/// </summary>
public class DietaResponse
{
    public required DateTime DataGeracao { get; set; }
    public required DietaSemanalDto Dieta { get; set; }
}
