using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

public class UsuarioModalidadeRequest
{
    public required int ModalidadeId { get; set; }

    // 1=Segunda ... 7=Domingo (ISO 8601). A validação de faixa (1-7) e de
    // duplicatas é feita no Controller, não aqui, porque DataAnnotations não
    // cobre bem "cada elemento de um array" sem um atributo customizado.
    [MinLength(1, ErrorMessage = "Selecione pelo menos um dia da semana.")]
    public required int[] DiasSemana { get; set; }

    [Range(0.25, 5, ErrorMessage = "A duração deve estar entre 15 minutos (0,25h) e 5 horas.")]
    public required decimal DuracaoMediaHoras { get; set; }
}
