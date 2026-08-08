namespace Ronu.Api.Models;

public class UsuarioModalidade
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public required Usuario Usuario { get; set; }
    public int ModalidadeId { get; set; }
    public required Modalidade Modalidade { get; set; }
    public int FrequenciaSemanal { get; set; }
}