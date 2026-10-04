using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Meta calórica adaptativa: calcula um ajuste percentual sobre a meta da
/// fórmula a partir do histórico real de peso e aderência e das metas que a
/// pessoa recebeu em cada período — se o peso não está variando como a
/// fórmula previa, a meta é corrigida aos poucos. Camada aditiva: sem dado
/// suficiente o ajuste é 0% e a meta fica exatamente a da fórmula.
/// Puro (sem banco, sem relógio do sistema, sem IA): recebe tudo por parâmetro.
/// </summary>
public interface ICalculadoraAjusteAdaptativo
{
    /// <param name="historico">Registros de peso do usuário (qualquer ordem).</param>
    /// <param name="metasDietas">Metas de cada dieta gerada (MetasDieta, qualquer ordem).</param>
    /// <param name="objetivoAtual">"perder peso", "ganhar peso" ou "manter peso".</param>
    /// <param name="metaBaseMediaDiaria">Média das 7 metas diárias da fórmula atual (sem o ajuste adaptativo), em kcal.</param>
    /// <param name="manutencaoAtualPorDia">Manutenção da fórmula atual por dia (índice 0 = segunda), para os períodos sem manutenção comparável.</param>
    /// <param name="agoraUtc">Momento do cálculo (define a janela dos últimos 28 dias).</param>
    AjusteAdaptativoDto Calcular(
        IReadOnlyList<RegistroPesoAderenciaDto> historico,
        IReadOnlyList<MetaDietaRegistroDto> metasDietas,
        string objetivoAtual,
        decimal metaBaseMediaDiaria,
        IReadOnlyList<decimal> manutencaoAtualPorDia,
        DateTime agoraUtc);
}
