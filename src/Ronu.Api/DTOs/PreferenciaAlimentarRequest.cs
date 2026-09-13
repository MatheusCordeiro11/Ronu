namespace Ronu.Api.DTOs;

/// <summary>
/// Dados enviados pelo cliente para registrar uma preferência alimentar do usuário logado.
/// Não inclui o UsuarioId: ele é obtido do token JWT no controller, nunca do corpo da requisição.
/// </summary>
public class PreferenciaAlimentarRequest
{
    public required string Alimento { get; set; }
    public required string Tipo { get; set; }
}