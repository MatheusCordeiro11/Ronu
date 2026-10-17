namespace Ronu.Api.Models.IA;

/// <summary>
/// Como a meta de um dia da dieta foi montada, em kcal inteiras, para o card
/// "Como sua meta foi calculada" do dashboard. Gravado junto da dieta no
/// momento da geração: explica a meta que a pessoa recebeu, mesmo que o peso,
/// o treino ou o objetivo mudem depois. As partes somam exatamente Meta
/// (ComposicaoMeta.Montar). Nulo em dietas geradas antes deste campo.
/// </summary>
public class ComposicaoMetaDto
{
    // TMB (Mifflin-St Jeor).
    public int GastoRepouso { get; set; }

    // TMB × (1,4 − 1): o dia a dia fora do treino.
    public int AtividadeForaDoTreino { get; set; }

    // Sessões do dia (vazio em dia de descanso).
    public List<TreinoComposicaoDto> Treino { get; set; } = new();

    // Parcela do objetivo (negativa = déficit), já com o teto do déficit.
    public int Objetivo { get; set; }

    // Meta adaptativa: fração da meta da fórmula aplicada nesta dieta.
    public int AjusteProgresso { get; set; }

    // Quanto a meta subiu para caber o mínimo nutricional (MetaElevadaPeloPiso).
    public int PisoCarboidrato { get; set; }

    // A meta do dia arredondada: igual à MetaCalculada.Calorias mostrada.
    public int Meta { get; set; }
}

public class TreinoComposicaoDto
{
    public required string Nome { get; set; }
    public decimal Horas { get; set; }
    public int Kcal { get; set; }
}
