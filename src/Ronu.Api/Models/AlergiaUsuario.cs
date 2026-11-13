using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Ronu.Api.Models;

/// <summary>
/// Uma alergia marcada pela pessoa (TACO 2c): um chip, uma linha. Dado de saúde (LGPD, art. 11): só é gravada com o
/// aceite vigente do consentimento "alergias", e tirar o último chip apaga todas as linhas (o aceite fica, revogado).
///
/// Uma linha por chip, e não um campo de flags no Usuario, por causa das duas abas: marcar Soja numa e Leite na outra
/// são duas gravações que não se sobrescrevem. O índice único segura o mesmo chip marcado duas vezes ao mesmo tempo.
/// </summary>
[Index(nameof(UsuarioId), nameof(Codigo), IsUnique = true)]
public class AlergiaUsuario
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    // Um dos códigos de CodigosAlergia ("leite", "soja"...): o texto do chip mora no frontend, e o código não muda
    // se o rótulo mudar.
    [MaxLength(20)]
    public required string Codigo { get; set; }

    public DateTime MarcadoEm { get; set; }
}
