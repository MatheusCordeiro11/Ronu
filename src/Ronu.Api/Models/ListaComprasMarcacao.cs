using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Ronu.Api.Models;

/// <summary>
/// Um item da lista de compras marcado como "já tenho", salvo na conta para aparecer em qualquer
/// aparelho. Vale para uma dieta só: dieta nova começa sem marcações, e quando a dieta sai do
/// histórico (limite de 3) as marcações dela saem junto (exclusão em cascata e, por garantia, apagadas
/// explicitamente no RepositorioDietaIA).
/// </summary>
[Index(nameof(DietaIAId), nameof(Chave), IsUnique = true)]
[Index(nameof(UsuarioId))]
public class ListaComprasMarcacao
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int DietaIAId { get; set; }
    public DietaIA DietaIA { get; set; } = null!;

    // ItemCompra.Chave da ConversaoCompras ("arroz-branco", "ovos").
    [MaxLength(64)]
    public required string Chave { get; set; }

    public DateTime MarcadoEm { get; set; }
}
