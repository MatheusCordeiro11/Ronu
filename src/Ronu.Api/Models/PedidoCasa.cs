using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.Models;

/// <summary>
/// Plano família, etapa 5: um aviso para o dono na aba Casa. Três tipos:
/// - "nova_semana" e "troca": um membro pediu (Pedir nova semana / Pedir troca de um item de uma refeição da casa);
/// - "aviso_entrada": alguém entrou no meio da semana e uma ou mais refeições da casa viraram individuais para essa
///   pessoa (algo que ela não come ou tem alergia).
///
/// NÃO tem UsuarioId, nem nenhum marcador derivado de quem pediu (hash, horário de sessão, nada): decisão do plano
/// ("o pedido não diz quem pediu"), que vale também no banco. O abuso é contido sem guardar o autor: o limite de
/// requisições por usuário fica só na memória (LimitesCasa.PoliticaPedido), o pedido igual a um aberto não vira linha
/// nova (RegrasPedidosCasa) e a casa tem um teto de pedidos abertos.
///
/// O pedido não guarda histórico: dispensar, atender ou uma semana nova apagam a linha (minimização, LGPD).
/// </summary>
public class PedidoCasa
{
    public const string TipoNovaSemana = "nova_semana";
    public const string TipoTroca = "troca";
    public const string TipoAvisoEntrada = "aviso_entrada";

    public int Id { get; set; }

    public int CasaId { get; set; }
    public Casa Casa { get; set; } = null!;

    // A semana da casa a que o pedido se refere (nulo: a casa ainda não tinha semana). Sem chave estrangeira, como a
    // DietaIA.SemanaCasaId: a semana nova apaga os pedidos da anterior explicitamente (GeracaoSemanaCasa).
    public int? SemanaCasaId { get; set; }

    [MaxLength(20)]
    public required string Tipo { get; set; }

    // "qui-jantar" na troca; no aviso de entrada, as refeições que viraram individuais, separadas por vírgula
    // ("seg-almoco,qui-jantar"). Nulo no pedido de semana nova.
    [MaxLength(200)]
    public string? ChavesCasa { get; set; }

    // Só na troca: a posição do item na refeição da casa e o alimento que estava lá quando o pedido foi feito.
    public int? Item { get; set; }

    [MaxLength(64)]
    public string? AlimentoId { get; set; }

    public DateTime CriadoEm { get; set; }
}
