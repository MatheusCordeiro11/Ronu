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

    // Id e revisão da DietaIA, que a troca de alimento manda de volta (409 se a dieta mudou). Nulos na
    // resposta do POST /dietas/gerar, que o dashboard não usa (ele recarrega o histórico).
    public int? DietaId { get; set; }
    public int? Revisao { get; set; }

    // Por que a dieta não tem troca de alimento ("dieta-anterior" ou "outra-versao-da-base"); nulo quando
    // tem. Só a dieta atual tem troca: no histórico, a tela não oferece mesmo quando é nulo.
    public string? SemTroca { get; set; }
}
