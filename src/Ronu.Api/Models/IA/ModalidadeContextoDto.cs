using Ronu.Api.Services.IA;

namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa a modalidade de exercício praticada pelo usuário, contendo
/// os dados necessários para o cálculo de gasto calórico e planejamento da dieta.
/// </summary>
public class ModalidadeContextoDto
{
    public required string Nome { get; set; }

    // MET usado na conta do treino: o de referência da modalidade ajustado pelo ritmo da aula
    // (IntensidadeTreino.MetEfetivo). Igual ao MetReferencia do catálogo em "normal" e nas modalidades sem blocos.
    public required decimal MetEfetivo { get; set; }

    // "Ritmo da aula" que deu o MetEfetivo ("tecnica", "normal" ou "sparring"), para rastreio: vai para a
    // composição salva da meta (TreinoComposicaoDto.Intensidade).
    public string Intensidade { get; set; } = IntensidadeTreino.Padrao;

    public required decimal DuracaoHoras { get; set; }

    // 1=Segunda ... 7=Domingo (ISO 8601). Usado para calcular uma meta
    // calórica diferente por dia da semana, em vez de uma meta única semanal.
    public required int[] DiasSemana { get; set; }

    // Hora de início (UsuarioModalidade.HorarioInicio): o prompt diz a hora do treino de cada dia, sem o nome da
    // modalidade. Nula = não informada.
    public TimeOnly? HorarioInicio { get; set; }
}
