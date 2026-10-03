using System.Text.Json.Serialization;

namespace Ronu.Api.Models.IA;

/// <summary>
/// Por que a meta calórica adaptativa aplicou (ou não) um ajuste. Vai como
/// texto no JSON ("HistoricoInsuficiente", ...): é um contrato estável que o
/// dashboard traduz para uma frase em pt-BR — a redação fica perto da tela.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MotivoAjusteAdaptativo>))]
public enum MotivoAjusteAdaptativo
{
    // Menos de 14 dias de histórico ou poucos registros válidos: 0%.
    HistoricoInsuficiente,

    // Diferença entre o real e o esperado abaixo da zona morta: 0%.
    DentroDoEsperado,

    // Considerando o que a pessoa comeu, o peso está acima do esperado
    // (perdendo mais devagar ou ganhando mais rápido): a meta diminui.
    PesoAcimaDoEsperado,

    // Considerando o que a pessoa comeu, o peso está abaixo do esperado
    // (perdendo mais rápido ou ganhando mais devagar): a meta aumenta.
    PesoAbaixoDoEsperado
}
