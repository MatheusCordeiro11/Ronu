using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Ronu.Api.Models;

/// <summary>
/// Plano família, etapa 6 (decisão D4): um item da lista de compras da casa marcado como "já tenho". É da casa, não de
/// quem marcou: qualquer membro marca e desmarca, e todos veem a mesma marca, porque é uma compra só. Por isso NÃO tem
/// UsuarioId, de propósito (como o PedidoCasa): o banco não guarda quem marcou. A visão "Só a minha parte" continua com
/// as marcações individuais (ListaComprasMarcacao).
///
/// Vale para uma semana da casa: semana nova começa sem marcações, e quando a semana sai (a casa guarda só 2) ou a
/// casa acaba, as marcações saem junto (cascata, e apagadas explicitamente na GeracaoSemanaCasa e na OperacoesCasa).
/// </summary>
[Index(nameof(SemanaCasaId), nameof(Chave), IsUnique = true)]
public class ListaCasaMarcacao
{
    public int Id { get; set; }

    public int CasaId { get; set; }
    public Casa Casa { get; set; } = null!;

    public int SemanaCasaId { get; set; }
    public SemanaCasa SemanaCasa { get; set; } = null!;

    // ItemCompra.Chave da ConversaoCompras ("arroz-branco", "ovos").
    [MaxLength(64)]
    public required string Chave { get; set; }

    // A quantidade da lista da casa (já arredondada, na unidade do item) quando o item foi marcado. A lista da casa muda
    // sem a semana mudar (alguém gera o café e os lanches, troca um item, entra ou sai), e a marca só continua valendo
    // enquanto a quantidade de agora não passar desta: a mesma regra da troca individual (MarcacoesAposTroca), em que o
    // "já tenho" cai quando o item passa a pedir mais. É o total da casa, o mesmo número que a lista mostra a todos.
    public decimal Quantidade { get; set; }

    public DateTime MarcadoEm { get; set; }
}
