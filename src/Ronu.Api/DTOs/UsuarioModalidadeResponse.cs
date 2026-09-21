namespace Ronu.Api.DTOs;

/// <summary>
/// Representação pública de uma UsuarioModalidade, retornada pela API, já com
/// os dados da modalidade vinculada embutidos (sem precisar de outra chamada).
/// </summary>
public class UsuarioModalidadeResponse
{
    public int Id { get; set; }
    public required ModalidadeResponse Modalidade { get; set; }
    public int FrequenciaSemanal { get; set; }
    public decimal DuracaoMediaHoras { get; set; }
}
