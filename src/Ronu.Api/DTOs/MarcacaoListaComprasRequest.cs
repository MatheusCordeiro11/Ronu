using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// Marca ou desmarca um item da lista de compras como "já tenho" (PUT
/// /api/dietas/atual/lista-compras/itens/{chave}). Sem UsuarioId: ele sai do token.
/// </summary>
public class MarcacaoListaComprasRequest
{
    // A dieta da lista que estava aberta na tela. Se outra dieta foi gerada depois (em outro
    // aparelho, por exemplo), a marcação não pode cair na dieta nova: a API responde 409.
    [Required(ErrorMessage = "Informe a dieta da lista.")]
    public int? DietaId { get; set; }

    [Required(ErrorMessage = "Informe se você já tem o item.")]
    public bool? Tenho { get; set; }
}
