using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

public class UsuarioModalidadeRequest
{
    public required int ModalidadeId { get; set; }

    // 1=Segunda ... 7=Domingo (ISO 8601). A validação de faixa (1-7) e de
    // duplicatas é feita no Controller, não aqui, porque DataAnnotations não
    // cobre bem "cada elemento de um array" sem um atributo customizado.
    [Required(ErrorMessage = "Selecione pelo menos um dia da semana.")]
    [MinLength(1, ErrorMessage = "Selecione pelo menos um dia da semana.")]
    public required int[] DiasSemana { get; set; }

    [Range(0.25, 5, ErrorMessage = "A duração deve estar entre 15 minutos (0,25h) e 5 horas.")]
    public required decimal DuracaoMediaHoras { get; set; }

    // "HH:mm", a hora de início em todos os dias. Ao atualizar um vínculo, ausente ou nulo mantém o salvo e vazio
    // apaga (RegrasHorario): a tela de treino que já existe não manda este campo.
    public string? HorarioInicio { get; set; }

    // "Ritmo da aula": "tecnica", "normal" ou "sparring" (IntensidadeTreino). Vínculo novo sem o campo nasce
    // "normal"; atualização sem o campo (ou nulo) mantém o salvo, porque as telas de antes não o mandam. Vazio ou
    // fora dos três dá 400; diferente de "normal" numa modalidade sem blocos também.
    public string? Intensidade { get; set; }
}
