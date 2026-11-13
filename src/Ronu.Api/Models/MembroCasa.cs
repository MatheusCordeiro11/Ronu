using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.Models;

/// <summary>
/// Uma pessoa numa casa. Uma casa por pessoa (índice único em UsuarioId) e um dono por casa (índice único parcial em
/// CasaId onde Papel = dono): os dois índices, e não só a checagem no controller, barram dois pedidos ao mesmo tempo.
/// </summary>
public class MembroCasa
{
    public const string PapelDono = "dono";
    public const string PapelMembro = "membro";

    public int Id { get; set; }

    public int CasaId { get; set; }
    public Casa Casa { get; set; } = null!;

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    [MaxLength(10)]
    public required string Papel { get; set; }

    // Ordena a passagem automática da casa quando o dono apaga a conta (vai para o mais antigo).
    public DateTime EntrouEm { get; set; }

    // Preenchido na linha do membro a quem o dono ofereceu a casa, até ele aceitar ou recusar. No máximo um por casa.
    public DateTime? TransferenciaOferecidaEm { get; set; }
}
