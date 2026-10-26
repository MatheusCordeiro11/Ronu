namespace Ronu.Api.Models.IA;

/// <summary>
/// A lista de alimentos da pessoa ficou abaixo do mínimo em algum papel no prato
/// (MinimosPorPapel) depois do "evitar": a semana pode ficar repetitiva e longe
/// da meta daquele papel. Gravado na dieta para o dashboard avisar (fase 2b-3).
/// </summary>
public class AvisoListaPobreDto
{
    // Papéis abaixo do mínimo: "Proteina", "Carboidrato", "Fruta" ou "Hortalica".
    public required List<string> Papeis { get; set; }

    // Quantos itens sobraram em cada papel da lista, para o texto do aviso.
    public required Dictionary<string, int> ItensPorPapel { get; set; }

    // De quantos grupos diferentes são as proteínas que sobraram (o mínimo é 2).
    public int GruposDeProteina { get; set; }
}
