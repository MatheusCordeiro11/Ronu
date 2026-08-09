namespace Ronu.Api.Models;

public class UsuarioModalidade
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int ModalidadeId { get; set; }
    public Modalidade Modalidade { get; set; } = null!;
    public int FrequenciaSemanal { get; set; }
}