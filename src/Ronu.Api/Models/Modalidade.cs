namespace Ronu.Api.Models;

public class Modalidade
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public decimal MetReferencia { get; set; }
    public ICollection<UsuarioModalidade> UsuarioModalidades { get; set; } = new List<UsuarioModalidade>();
}