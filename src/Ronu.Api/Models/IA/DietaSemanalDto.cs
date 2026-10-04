using System.Text.Json.Serialization;

namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa a dieta semanal gerada pela IA. Não tem mais uma meta única
/// para a semana toda — cada DiaDietaDto carrega sua própria MetaCalculada,
/// já que dias de treino têm meta diferente de dias de descanso.
/// </summary>
public class DietaSemanalDto
{
    public required List<DiaDietaDto> Dias { get; set; }

    // Como a meta adaptativa atuou nesta dieta (registro histórico, para o
    // dashboard). Nulo em dietas geradas antes da meta adaptativa existir.
    public AjusteAdaptativoDto? AjusteAdaptativo { get; set; }

    // Manutenção da fórmula de cada dia (índice 0 = segunda), só para o
    // RepositorioDietaIA gravar em MetaDieta. Fora do JSON salvo e da resposta
    // da API: a tabela MetasDieta é o lugar dela.
    [JsonIgnore]
    public decimal[]? ManutencaoPorDia { get; set; }
}
