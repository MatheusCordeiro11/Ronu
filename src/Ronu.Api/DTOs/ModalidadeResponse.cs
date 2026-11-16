namespace Ronu.Api.DTOs;

/// <summary>
/// Representação pública de uma Modalidade do catálogo, retornada pela API.
/// </summary>
public class ModalidadeResponse
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    // MET do ritmo "normal" (o de catálogo, o mesmo de antes do seletor).
    public decimal MetReferencia { get; set; }
    public required string Categoria { get; set; }

    // METs dos dois blocos da aula; nulos = sem o seletor "Ritmo da aula". O frontend escolhe os rótulos pela
    // categoria (Lutas: técnica/sparring; Treino funcional: força/circuito).
    public decimal? MetBlocoLeve { get; set; }
    public decimal? MetBlocoIntenso { get; set; }
}