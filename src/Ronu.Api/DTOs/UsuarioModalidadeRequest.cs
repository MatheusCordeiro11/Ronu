namespace Ronu.Api.DTOs;

public class UsuarioModalidadeRequest
{
    public required int ModalidadeId { get; set; }
    public required int FrequenciaSemanal { get; set; }
}