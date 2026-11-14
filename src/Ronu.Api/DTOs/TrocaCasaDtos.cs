using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// POST /api/casa/semana/trocas (dono): troca um item de uma refeição da casa, para todos. O item é a posição na
/// refeição da casa (chaveCasa + item), a mesma na semana da casa e na porção de cada um.
/// </summary>
public class TrocaCasaRequest
{
    // A semana e a revisão que a tela mostrava: outra semana gerada, ou outra troca gravada no meio, dá 409.
    [Required(ErrorMessage = "Informe a semana.")]
    public int? SemanaId { get; set; }

    [Required(ErrorMessage = "Informe a revisão da semana.")]
    public int? Revisao { get; set; }

    [Required(ErrorMessage = "Informe a refeição.")]
    [MaxLength(20, ErrorMessage = "Refeição inválida.")]
    public string? ChaveCasa { get; set; }

    [Required(ErrorMessage = "Informe o alimento.")]
    public int? Item { get; set; }

    [Required(ErrorMessage = "Informe o alimento atual.")]
    [MaxLength(64, ErrorMessage = "Alimento inválido.")]
    public string? AlimentoIdAtual { get; set; }

    [Required(ErrorMessage = "Informe o substituto.")]
    [MaxLength(64, ErrorMessage = "Alimento inválido.")]
    public string? AlimentoIdNovo { get; set; }

    // "refeicao" ou "semana" (todas as vezes em que o item aparece no almoço e no jantar da casa).
    [Required(ErrorMessage = "Informe o escopo.")]
    public string? Escopo { get; set; }
}

/// <summary>
/// Os substitutos de um item da casa (GET /api/casa/semana/trocas/opcoes, dono). Mesmo formato das opções da troca
/// individual, com duas diferenças: as quantidades, as kcal e a prévia são só as da porção do dono (nada da porção de
/// outro membro), e o "fora" ganha "naoServeParaACasa" (o substituto deixaria a porção de alguém fora da margem, sem
/// dizer de quem). O que saiu pelo "não como" e pelas alergias de todos vem numa contagem só, sem separar alergia.
/// </summary>
public class OpcoesTrocaCasaResponse
{
    public required int SemanaId { get; set; }
    public required int Revisao { get; set; }
    public required string ChaveCasa { get; set; }
    public required int Item { get; set; }
    public required ItemTrocaDto Original { get; set; }
    public required string Nutriente { get; set; }
    public required int OcorrenciasNaSemana { get; set; }
    public required int ForaPelasRestricoesDaCasa { get; set; }
    public required EscopoTrocaDto Refeicao { get; set; }
    public required EscopoTrocaDto Semana { get; set; }
}
