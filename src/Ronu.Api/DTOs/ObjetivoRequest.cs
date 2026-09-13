namespace Ronu.Api.DTOs;

/// <summary>
/// Dados enviados pelo cliente para registrar um novo objetivo/peso do usuário logado.
/// Não inclui o UsuarioId: ele é obtido do token JWT no controller, nunca do corpo da requisição.
/// </summary>
public class ObjetivoRequest
{
    public required decimal Peso { get; set; }
    public required string Objetivo { get; set; }
}