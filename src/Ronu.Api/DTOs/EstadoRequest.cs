using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

public class EstadoRequest
{
    [Required(ErrorMessage = "Informe seu estado.")]
    public required string Estado { get; set; }
}
