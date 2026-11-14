using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// POST /api/casa/pedidos (membro). Sem UsuarioId nem nada que identifique quem pede: o autor sai do token só para o
/// limite de requisições, que fica na memória.
/// </summary>
public class PedidoCasaRequest
{
    // "nova_semana" ou "troca".
    [Required(ErrorMessage = "Informe o tipo do pedido.")]
    public string? Tipo { get; set; }

    // A semana que a tela mostrava. Obrigatória na troca; no pedido de semana nova, opcional (a casa pode ainda não ter
    // semana). Diferente da atual: 409, a semana já mudou.
    public int? SemanaId { get; set; }

    // Só na troca: a refeição da casa ("qui-jantar") e a posição do item nela (a mesma na semana da casa e na porção
    // de cada um).
    [MaxLength(20, ErrorMessage = "Refeição inválida.")]
    public string? ChaveCasa { get; set; }

    public int? Item { get; set; }

    // Opcional: o alimento que a tela mostrava na posição. Quando vem, precisa ser o que está lá.
    [MaxLength(64, ErrorMessage = "Alimento inválido.")]
    public string? AlimentoId { get; set; }
}

/// <summary>
/// 202 do POST /api/casa/pedidos. A mesma resposta para um pedido novo e para um igual a um que já estava aberto: quem
/// pede não descobre se alguém da casa pediu o mesmo. A semana é a chave do "Pedido enviado" na tela.
/// </summary>
public class PedidoEnviadoResponse
{
    public int? SemanaId { get; set; }
}

/// <summary>
/// Um pedido ou aviso aberto, só para o dono (CasaResponse.Pedidos). Nunca leva quem pediu, nem a data e a hora (que
/// ajudariam a adivinhar).
/// </summary>
public class PedidoCasaResponse
{
    public int Id { get; set; }

    // "nova_semana", "troca" ou "aviso_entrada".
    public required string Tipo { get; set; }

    // A semana a que o pedido se refere (nula: a casa ainda não tinha semana).
    public int? SemanaId { get; set; }

    // Na troca, a refeição do item; no aviso de entrada, as refeições que viraram individuais para quem entrou.
    public required List<string> ChavesCasa { get; set; }

    // Só na troca: a posição do item, o id e o nome do alimento (para "Ver substitutos").
    public int? Item { get; set; }
    public string? AlimentoId { get; set; }
    public string? Alimento { get; set; }

    // A frase pronta ("Alguém pediu trocar ... no jantar de quarta.").
    public required string Texto { get; set; }
}
