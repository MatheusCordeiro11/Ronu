using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Meta calórica adaptativa: calcula um ajuste percentual sobre a meta da
/// fórmula a partir do histórico real de peso e aderência — se o peso não está
/// variando como o objetivo pede, a meta é corrigida aos poucos. Camada aditiva:
/// sem dado suficiente o ajuste é 0% e a meta fica exatamente a da fórmula.
/// Puro (sem banco, sem relógio do sistema, sem IA): recebe tudo por parâmetro.
/// </summary>
public interface ICalculadoraAjusteAdaptativo
{
    /// <param name="historico">Registros de peso do usuário (qualquer ordem).</param>
    /// <param name="objetivoAtual">"perder peso", "ganhar peso" ou "manter peso".</param>
    /// <param name="metaBaseMediaDiaria">Média das 7 metas diárias da fórmula, em kcal.</param>
    /// <param name="agoraUtc">Momento do cálculo (define a janela dos últimos 28 dias).</param>
    AjusteAdaptativoDto Calcular(
        IReadOnlyList<RegistroPesoAderenciaDto> historico,
        string objetivoAtual,
        decimal metaBaseMediaDiaria,
        DateTime agoraUtc);
}
