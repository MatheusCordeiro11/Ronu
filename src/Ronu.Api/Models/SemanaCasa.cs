using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.Models;

/// <summary>
/// Plano família, etapa 4: o almoço e o jantar da semana de uma casa, numa porção de referência (o que o Gemini
/// devolveu, sem a quantidade de ninguém). A porção de cada pessoa fica só na DietaIA dela (Origem = casa), calculada
/// em C# a partir desta. Não tem UsuarioId de propósito: é da casa, não de quem gerou, e não guarda nenhum dado de
/// membro (nem meta, nem porção, nem restrição).
///
/// A casa guarda a semana atual e a anterior (GeracaoSemanaCasa.SemanasGuardadas): a anterior serve ao "Desfazer" da
/// troca e ao aviso de entrada no meio da semana (etapa 5).
/// </summary>
public class SemanaCasa
{
    public int Id { get; set; }

    public int CasaId { get; set; }
    public Casa Casa { get; set; } = null!;

    public DateTime GeradaEm { get; set; }

    // Sobe a cada troca na semana da casa (etapa 5). Token de concorrência, como a DietaIA.Revisao.
    [ConcurrencyCheck]
    public int Revisao { get; set; }

    // O SemanaCasaDto serializado: os 7 dias com o almoço e o jantar de referência e o aviso de lista pobre.
    public required string ConteudoJson { get; set; }

    // BaseNutricional.VersaoBase da lista usada: com outra versão, os AlimentoId podem não existir mais, e a geração
    // individual de quem está na casa não usa esta semana.
    public int VersaoBase { get; set; }

    // Quando alguém da casa teve uma refeição desta semana trocada por uma individual (algo que a pessoa não come ou
    // tem alergia, marcado depois da semana gerada). Só o dono vê o aviso, sem dizer quem.
    public DateTime? RefeicaoIndividualEm { get; set; }
}
