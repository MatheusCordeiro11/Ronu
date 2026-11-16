using System.ComponentModel.DataAnnotations;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Models;

/// <summary>
/// Tabela de associação entre Usuario e Modalidade (relação N:N).
/// </summary>
public class UsuarioModalidade
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int ModalidadeId { get; set; }
    public Modalidade Modalidade { get; set; } = null!;

    // Dias da semana em que a modalidade é praticada (1=Segunda ... 7=Domingo,
    // padrão ISO 8601). Não existe mais um campo separado de frequência — ela
    // é sempre calculada como DiasSemana.Length, para nunca haver
    // inconsistência entre "quantos dias" e "quais dias".
    public int[] DiasSemana { get; set; } = Array.Empty<int>();

    public decimal DuracaoMediaHoras { get; set; }

    // Hora de início do treino, a mesma em todos os DiasSemana (RegrasHorario). Nula = não informada.
    public TimeOnly? HorarioInicio { get; set; }

    // "Ritmo da aula": "tecnica", "normal" ou "sparring" (IntensidadeTreino). Só muda o MET nas modalidades com
    // blocos; nas outras fica sempre "normal". Default "normal" também no banco (ApplicationDbContext), para as
    // linhas de antes da coluna e para o insert de um backend que não conhece o campo.
    [MaxLength(16)]
    public string Intensidade { get; set; } = IntensidadeTreino.Padrao;
}
