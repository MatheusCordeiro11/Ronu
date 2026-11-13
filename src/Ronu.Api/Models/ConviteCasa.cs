using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.Models;

/// <summary>
/// Um link de convite para a casa, de uso único e válido por 7 dias. Só o SHA-256 do token fica no banco: quem lê a
/// tabela não consegue montar um link que funcione. Gerar outro não invalida os anteriores (o dono pode convidar
/// duas pessoas ao mesmo tempo); ele vê os abertos e pode revogar.
/// </summary>
public class ConviteCasa
{
    public int Id { get; set; }

    public int CasaId { get; set; }
    public Casa Casa { get; set; } = null!;

    // Quem gerou (o dono na hora). Chama-se UsuarioId, e não DonoId, para a exclusão de conta e a exportação o acharem
    // como toda tabela da pessoa.
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    // SHA-256 do token, em hexadecimal.
    [MaxLength(64)]
    public required string TokenHash { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? UsadoEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
}
