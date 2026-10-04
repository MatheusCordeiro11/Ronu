using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

public class GoogleLoginRequest
{
    [Required(ErrorMessage = "Não foi possível entrar com o Google. Tente novamente.")]
    public required string IdToken { get; set; }
}
