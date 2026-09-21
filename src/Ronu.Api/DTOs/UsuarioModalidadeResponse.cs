namespace Ronu.Api.DTOs;

/// <summary>
/// Representação pública de uma UsuarioModalidade, retornada pela API, já com
/// os dados da modalidade vinculada embutidos (sem precisar de outra chamada).
/// </summary>
public class UsuarioModalidadeResponse
{
    public int Id { get; set; }
    public required ModalidadeResponse Modalidade { get; set; }
    public required int[] DiasSemana { get; set; }
    public decimal DuracaoMediaHoras { get; set; }

    // Calculada a partir de DiasSemana.Length, nunca armazenada separadamente
    // — evita o risco de "quantos dias" e "quais dias" discordarem entre si.
    public int FrequenciaSemanal => DiasSemana.Length;
}
