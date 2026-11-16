using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// Marca ou desmarca uma refeição como feita (PUT /api/dietas/atual/refeicoes-feitas/{data}/{refeicao}). Sem
/// UsuarioId: ele sai do token. As kcal e a quantidade de refeições do dia são lidas da dieta no servidor.
/// </summary>
public class MarcacaoRefeicaoRequest
{
    // A dieta que estava aberta na tela. Se outra dieta foi gerada depois (em outro aparelho, ou pela semana da casa),
    // a marcação não pode cair na dieta nova: a API responde 409.
    [Required(ErrorMessage = "Informe a dieta.")]
    public int? DietaId { get; set; }

    // Explícito (e não "alternar"): duas abas tocando ao mesmo tempo não desfazem uma à outra.
    [Required(ErrorMessage = "Informe se a refeição foi feita.")]
    public bool? Feita { get; set; }
}

/// <summary>
/// As refeições marcadas como feitas na dieta atual, só nas datas que dá para marcar (ontem e hoje), e o "hoje" da
/// pessoa no fuso da UF, para o painel abrir no dia certo.
/// </summary>
public class RefeicoesFeitasResponse
{
    public int DietaId { get; set; }

    // O dia de hoje no fuso da UF ("AAAA-MM-DD").
    public DateOnly Hoje { get; set; }

    // Da mais antiga para a mais nova; hoje sempre vem, mesmo sem nenhuma marcação.
    public List<DiaRefeicoesFeitasDto> Dias { get; set; } = [];
}

public class DiaRefeicoesFeitasDto
{
    public DateOnly Data { get; set; }

    // A posição deste dia em dieta.dias (casado pelo nome do dia da semana). Nulo se a dieta não tem esse dia.
    public int? IndiceDia { get; set; }

    // As posições das refeições marcadas (dieta.dias[indiceDia].refeicoes[i]), em ordem.
    public List<int> Marcadas { get; set; } = [];
}
