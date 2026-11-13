using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.Models;

/// <summary>
/// Plano família: um grupo de até 6 pessoas que divide o almoço e o jantar da semana. A casa em si não guarda
/// ninguém: quem está nela fica em MembroCasa, onde a coluna da pessoa se chama UsuarioId como em toda tabela da conta
/// (a exclusão de conta e o teste por reflexão acham as tabelas por esse nome).
/// </summary>
public class Casa
{
    public int Id { get; set; }

    // Os mesmos 3 valores do perfil (RegrasOrcamento). Nulo = não informado. Da casa, e não de cada membro, porque
    // é uma compra só.
    [MaxLength(20)]
    public string? OrcamentoSemanal { get; set; }

    public DateTime CriadaEm { get; set; }
}
