using Microsoft.EntityFrameworkCore;

namespace Ronu.Api.Models;

/// <summary>
/// Uma refeição da dieta marcada como feita ("comi esta refeição do plano"), num dia do calendário. Diz só isso: a
/// falta da marcação não diz se a pessoa pulou, comeu outra coisa ou esqueceu de marcar. Desmarcar apaga a linha.
///
/// Sem chave estrangeira para a DietaIA, de propósito, como a MetaDieta: a regra das 3 dietas apaga a DietaIA, e a
/// marcação precisa continuar valendo para a janela da meta adaptativa. Apagar a conta apaga as marcações (cascata e,
/// por garantia, ExclusaoConta). Guardadas por 35 dias (LimpezaRetencao.PrazoRefeicoesFeitas).
/// </summary>
// O índice único barra o toque duplo de dois aparelhos (como o (DietaIAId, Chave) da lista de compras). Ele começa por
// (UsuarioId, Data), então também serve à leitura por período: um índice só nessas duas colunas seria repetido.
[Index(nameof(UsuarioId), nameof(Data), nameof(DietaIAId), nameof(RefeicaoIndice), IsUnique = true)]
public class RefeicaoFeita
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    // A dieta em que a refeição foi marcada. Sem navegação nem FK (ver o resumo da classe).
    public int DietaIAId { get; set; }

    // O dia do calendário no fuso da UF da pessoa (FusoHorarioEstado.Hoje), não só o dia da semana: a mesma dieta vale
    // várias semanas, e a segunda da semana 2 não pode cair em cima da segunda da semana 1.
    public DateOnly Data { get; set; }

    // A posição da refeição no dia da dieta (Dias[d].Refeicoes[i]). A troca de alimento não muda a posição.
    public int RefeicaoIndice { get; set; }

    // Foto das kcal da refeição (Macros.Calorias) no momento da marcação: a troca muda as kcal depois, e o JSON some
    // quando a dieta sai do histórico. Lida da dieta no servidor, nunca do cliente.
    [Precision(7, 1)]
    public decimal Kcal { get; set; }

    // Foto de quantas refeições o dia tinha nessa dieta: é o que diz se o dia foi marcado inteiro.
    public int RefeicoesNoDia { get; set; }

    public DateTime MarcadaEm { get; set; }
}
