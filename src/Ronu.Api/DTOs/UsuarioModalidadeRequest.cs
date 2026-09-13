namespace Ronu.Api.DTOs;

/// <summary>
/// Dados enviados pelo cliente para vincular uma modalidade existente ao usuário logado.
/// Não inclui o UsuarioId: ele é obtido do token JWT no controller, nunca do corpo da requisição.
/// </summary>
public class UsuarioModalidadeRequest
{
    public required int ModalidadeId { get; set; }
    public required int FrequenciaSemanal { get; set; }
}