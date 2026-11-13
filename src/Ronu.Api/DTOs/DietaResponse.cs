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

    // TACO 2c: os alimentos da dieta que têm uma alergia marcada pela pessoa (marcada depois de a dieta ser gerada:
    // marcar um chip não muda a dieta já salva), para o aviso do painel. Só na dieta atual (GET /dietas/atual e as
    // respostas da troca): lista vazia quando não há nenhum; nulo no histórico e no POST /dietas/gerar.
    public List<AlergiaNaDietaDto>? AlergiasNaDieta { get; set; }
}

public class AlergiaNaDietaDto
{
    public required string AlimentoId { get; set; }

    // O nome do item na base (sem a contagem): a tela marca cada refeição pelo AlimentoId.
    public required string Nome { get; set; }

    // Os códigos dos chips marcados que tiram este alimento, na ordem da tela ("leite", "gluten").
    public required List<string> Alergias { get; set; }

    // Quantas vezes ele aparece na semana.
    public required int Vezes { get; set; }
}
