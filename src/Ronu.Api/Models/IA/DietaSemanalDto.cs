namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa a dieta semanal gerada pela IA, contendo os dias da semana
/// e a meta diária de macronutrientes calculada para o usuário.
/// </summary>
public class DietaSemanalDto
{
    /// <summary>
    /// Lista de dias que compõem a dieta semanal,
    /// contendo as refeições planejadas para cada dia.
    /// </summary>
    public required List<DiaDietaDto> Dias { get; set; }

    /// <summary>
    /// Meta diária de macronutrientes calculada com base na frequência
    /// semanal de treino do usuário. Como a meta é definida para a semana
    /// e não considera quais dias específicos possuem treino, ela é a mesma
    /// para todos os dias e, por isso, é armazenada uma única vez na dieta
    /// semanal em vez de ser repetida em cada DiaDietaDto.
    /// </summary>
    public required MacrosDto MetaDiariaCalculada { get; set; }
}
