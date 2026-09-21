using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

public class UsuarioModalidadeRequest
{
    public required int ModalidadeId { get; set; }
    public required int FrequenciaSemanal { get; set; }

    [Range(0.25, 5, ErrorMessage = "A duração deve estar entre 15 minutos (0,25h) e 5 horas.")]
    public required decimal DuracaoMediaHoras { get; set; }
}
