using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// Troca um item da dieta atual (POST /api/dietas/atual/trocas). Sem UsuarioId, quantidade nem kcal: o usuário
/// sai do token, e o servidor recalcula tudo e confere de novo as regras.
/// </summary>
public class TrocaRequest
{
    // A dieta e a revisão que a tela mostrava: se outra dieta foi gerada, ou outra aba gravou uma troca, 409.
    [Required(ErrorMessage = "Informe a dieta.")]
    public int? DietaId { get; set; }

    [Required(ErrorMessage = "Informe a revisão da dieta.")]
    public int? Revisao { get; set; }

    [Required(ErrorMessage = "Informe o dia.")]
    public int? Dia { get; set; }

    [Required(ErrorMessage = "Informe a refeição.")]
    public int? Refeicao { get; set; }

    [Required(ErrorMessage = "Informe o alimento.")]
    public int? Item { get; set; }

    // O alimento que a tela mostrava na posição: confere que é o mesmo item, não só a mesma posição.
    [Required(ErrorMessage = "Informe o alimento atual.")]
    [MaxLength(64, ErrorMessage = "Alimento inválido.")]
    public string? AlimentoIdAtual { get; set; }

    [Required(ErrorMessage = "Informe o substituto.")]
    [MaxLength(64, ErrorMessage = "Alimento inválido.")]
    public string? AlimentoIdNovo { get; set; }

    // "refeicao" ou "semana".
    [Required(ErrorMessage = "Informe o escopo.")]
    public string? Escopo { get; set; }
}

/// <summary>
/// A dieta depois de uma troca, de um desfazer ou de uma volta ao original, e quantas marcações "já tenho" da
/// lista de compras caíram nessa gravação, para o aviso da troca dizer. Só a contagem: nenhum nome de alimento.
/// </summary>
public class TrocaResponse : DietaResponse
{
    public required int MarcacoesRemovidas { get; set; }
}

/// <summary>Os substitutos de um item (GET /api/dietas/atual/trocas/opcoes).</summary>
public class OpcoesTrocaResponse
{
    public required int DietaId { get; set; }
    public required int Revisao { get; set; }
    public required ItemTrocaDto Original { get; set; }

    // "proteina", "carboidrato", "gordura" ou "livre" (sem nutriente a manter: fica o peso).
    public required string Nutriente { get; set; }

    // Quantas vezes o mesmo alimento aparece na semana (o "Na semana toda").
    public required int OcorrenciasNaSemana { get; set; }

    // Quantos itens do grupo saíram pelo que a pessoa marcou para evitar.
    public required int ForaPeloEvitar { get; set; }

    public required EscopoTrocaDto Refeicao { get; set; }
    public required EscopoTrocaDto Semana { get; set; }
}

public class ItemTrocaDto
{
    public required string AlimentoId { get; set; }
    public required string Nome { get; set; }
    public required decimal Quantidade { get; set; }
    public required string Unidade { get; set; }
    public required decimal Kcal { get; set; }

    // Gramas do nutriente principal; nulo nos itens sem nutriente principal.
    public decimal? NutrienteG { get; set; }
}

public class EscopoTrocaDto
{
    public required List<OpcaoTrocaDto> DasPreferencias { get; set; }
    public required List<OpcaoTrocaDto> Parecidos { get; set; }
    public required List<OpcaoTrocaDto> VerMais { get; set; }

    // Quantas opções ficaram de fora por motivo ("porcaoMaxima", "foraDaMeta", "jaNaRefeicao",
    // "refogadoComOleo", "usoDiferente").
    public required Dictionary<string, int> Fora { get; set; }

    // Só quando nenhuma opção passou: o porquê, para a tela dizer.
    public string? MensagemSemOpcoes { get; set; }
}

/// <summary>
/// Um substituto. Quantidade, nome (com a contagem) e kcal são os do item tocado; na semana, cada outra vez
/// em que ele aparece tem a sua quantidade, e a soma das diferenças está em DifKcal.
/// </summary>
public class OpcaoTrocaDto
{
    public required string AlimentoId { get; set; }
    public required string Nome { get; set; }
    public required decimal Quantidade { get; set; }
    public required string Unidade { get; set; }
    public int? Contagem { get; set; }
    public required decimal Kcal { get; set; }
    public required decimal DifKcal { get; set; }
    public decimal? NutrienteG { get; set; }

    // Substituto de outro uso (macarrão no lugar do pão do café): a tela o identifica. Só aparece nos carboidratos;
    // nas gorduras, o outro uso fica de fora.
    public required bool OutroUso { get; set; }

    public required int Ocorrencias { get; set; }
    public required List<PreviaDiaDto> Dias { get; set; }
}

/// <summary>O total de kcal de um dia afetado, antes e depois, contra a meta do dia.</summary>
public class PreviaDiaDto
{
    public required int Dia { get; set; }
    public required string DiaSemana { get; set; }
    public required decimal TotalAntes { get; set; }
    public required decimal TotalDepois { get; set; }
    public required decimal Meta { get; set; }
    public required decimal DesvioPct { get; set; }
}
