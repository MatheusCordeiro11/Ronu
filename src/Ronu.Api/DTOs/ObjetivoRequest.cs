using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// Dados enviados pelo cliente para registrar um novo objetivo/peso do usuário logado.
/// Não inclui o UsuarioId: ele é obtido do token JWT no controller, nunca do corpo da requisição.
/// </summary>
public class ObjetivoRequest
{
    public required decimal Peso { get; set; }

    [Required(ErrorMessage = "Informe seu objetivo.")]
    public required string Objetivo { get; set; }

    // Opcional: "seguiu", "comeu_mais", "comeu_menos" ou "nao_seguiu".
    public string? Aderencia { get; set; }
}
