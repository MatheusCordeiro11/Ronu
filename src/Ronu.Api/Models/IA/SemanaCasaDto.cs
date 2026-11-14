namespace Ronu.Api.Models.IA;

/// <summary>
/// O conteúdo de uma SemanaCasa: os 7 dias, cada um com o almoço e o jantar numa porção de referência (as quantidades
/// que o Gemini devolveu para a referência de 650 e 550 kcal, sem o rótulo da contagem). Nenhuma quantidade daqui vai
/// para a resposta da API: a porção de cada pessoa sai desta referência (PorcoesCasa) e fica só na dieta dela.
/// </summary>
public class SemanaCasaDto
{
    // Dias na ordem segunda ... domingo; em cada um, as refeições "Almoço" e "Jantar", com ChaveCasa e DaCasa.
    // A MetaCalculada de cada dia é o próprio total da referência: serve só à regra dos 5% da troca.
    public required List<DiaDietaDto> Dias { get; set; }

    public int VersaoBase { get; set; }

    // A lista da casa ficou abaixo do mínimo em algum papel do prato (sem contar fruta). Só o dono vê.
    public AvisoListaPobreDto? AvisoListaPobre { get; set; }

    // Etapa 5: a última troca na casa (do dono), com os itens da referência como estavam antes, para o "Desfazer",
    // como na DietaSemanalDto. A revisão é a da SemanaCasa. Fora do JSON quando nulo.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public UltimaTrocaDto? UltimaTroca { get; set; }
}
