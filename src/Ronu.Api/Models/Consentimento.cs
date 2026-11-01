using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Ronu.Api.Models;

/// <summary>
/// Um aceite de um texto de consentimento (LGPD, art. 8º e art. 11, I), numa tabela só para todos os tipos:
/// "geral" (dado de saúde, obrigatório para usar o Ronu) e, depois, "alergias" e "casa". Cada aceite é uma linha
/// nova, então o histórico fica: aceitar a versão 2 não apaga o aceite da 1. O texto de cada versão mora no
/// código (TextosConsentimento), e o git prova o que ela dizia.
///
/// O aceite vale enquanto a versão é a vigente e RevogadoEm está nulo. Sem IP nem navegador de propósito: a linha
/// gravada a partir do token da própria conta já é a prova, e guardar mais seria juntar dado sem precisar.
/// </summary>
[Index(nameof(UsuarioId), nameof(Tipo))]
public class Consentimento
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    [MaxLength(20)]
    public required string Tipo { get; set; }

    public int Versao { get; set; }

    public DateTime AceitoEm { get; set; }

    // Nulo enquanto vale. O "geral" não é revogado por aqui: revogá-lo é apagar a conta (decisão de 2026-11-01).
    public DateTime? RevogadoEm { get; set; }
}
