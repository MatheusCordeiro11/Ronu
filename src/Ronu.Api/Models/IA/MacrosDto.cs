using System;

namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa os macronutrientes de uma refeição, um dia ou a meta diária.
/// Reutilizada em múltiplos níveis da dieta (por isso é uma classe separada,
/// em vez de repetir as 4 propriedades em cada DTO que precisa de macros).
/// </summary>
public class MacrosDto
{
    public required decimal Calorias { get; set; }
    public required decimal ProteinasG { get; set; }
    public required decimal CarboidratosG { get; set; }
    public required decimal GordurasG { get; set; }
}
