namespace Ronu.Api.DTOs;

/// <summary>
/// Representação pública de uma Modalidade do catálogo, retornada pela API.
/// </summary>
public class ModalidadeResponse
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public decimal MetReferencia { get; set; }
}