namespace Ronu.Api.DTOs;

public class UsuarioModalidadeResponse
{
    public int Id { get; set; }
    public required ModalidadeResponse Modalidade { get; set; }
    public int FrequenciaSemanal { get; set; }
}