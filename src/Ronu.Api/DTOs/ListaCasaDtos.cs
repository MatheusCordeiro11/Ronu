using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// Plano família, etapa 6: a lista de compras da casa (GET /api/casa/lista-compras). A semana inteira de todos os
/// membros (o almoço e o jantar da casa, com a porção de cada um, e o café e os lanches de cada um), somada por alimento
/// numa lista só, para uma compra só. Os itens têm o mesmo formato da lista individual (SecaoListaComprasDto), mas cada
/// número é o TOTAL da casa: nada diz de quem é um item, e nenhuma porção ou item de um membro aparece separado.
///
/// Não leva peso, meta, kcal, macros, objetivo, nome nem e-mail de ninguém. A marca de alergia de cada item (TemAlergeno,
/// Alergias) usa só as alergias de quem está vendo (decisão da etapa 6): a de outro membro seria dado de saúde dele.
/// </summary>
public class ListaCasaResponse
{
    // A semana da casa de onde a lista saiu: o PUT do "já tenho" manda de volta, e a marcação vale só nesta semana.
    public required int SemanaId { get; set; }
    public required DateTime GeradaEm { get; set; }

    // "Lista de compras da casa · semana de dd/mm", com a data da semana no fuso da UF de quem vê. É o título do
    // "Compartilhar", que leva só isto e os itens (nome e quantidade): nada de alergia nem de nomes.
    public required string TituloCompartilhar { get; set; }

    public required int VersaoConversao { get; set; }

    // Como na lista individual: falso quando alguma dieta somada é anterior à lista fechada (Motivo = "dieta-antiga").
    public required bool Disponivel { get; set; }
    public string? Motivo { get; set; }
    public bool AvisoOleo { get; set; }

    // Falso quando a dieta atual de alguém da casa ainda não é da semana atual (por exemplo, um perfil incompleto, que
    // fica sem dieta nova): a parte dessa pessoa fica de fora. Não diz quem, nem quantos.
    public required bool Completa { get; set; }

    public List<SecaoListaComprasDto> Secoes { get; set; } = [];
}

/// <summary>
/// PUT /api/casa/lista-compras/itens/{chave}: marca ou desmarca um item como "já tenho" para a casa toda. Sem UsuarioId
/// (sai do token, e nem é gravado: a marca é da casa).
/// </summary>
public class MarcacaoListaCasaRequest
{
    // A semana da lista que estava aberta. Semana nova começa sem marcações: a marca de uma lista velha dá 409.
    [Required(ErrorMessage = "Informe a semana da lista.")]
    public int? SemanaId { get; set; }

    [Required(ErrorMessage = "Informe se a casa já tem o item.")]
    public bool? Tenho { get; set; }

    // Obrigatória para marcar: a quantidade que a tela mostrava. Se a lista da casa passou a pedir mais desde que foi
    // aberta (alguém gerou o café e os lanches, por exemplo), a API responde 409 em vez de marcar o que a pessoa não viu.
    [Range(0, 1_000_000, ErrorMessage = "Quantidade inválida.")]
    public decimal? Quantidade { get; set; }
}
