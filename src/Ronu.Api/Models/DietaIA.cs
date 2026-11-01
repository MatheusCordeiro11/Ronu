using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.Models;

/// <summary>
/// Dieta gerada por IA para um usuário, salva em um determinado momento.
/// O conteúdo é armazenado como JSON bruto para manter flexibilidade no formato
/// retornado pela IA, sem precisar de uma tabela relacional rígida para cada campo.
/// </summary>
public class DietaIA
{
    public int Id { get; set; }
    public DateTime DataGeracao { get; set; }
    public required string ConteudoJson { get; set; }

    // Sobe a cada troca de alimento gravada (trocar, desfazer, voltar ao original). Token de concorrência: o
    // UPDATE só passa se a revisão no banco ainda é a lida, então duas abas trocando ao mesmo tempo não
    // sobrescrevem uma à outra (a segunda recebe 409). O cliente manda a revisão que viu, pelo mesmo motivo.
    [ConcurrencyCheck]
    public int Revisao { get; set; }

    public int UsuarioId { get; set; }

    // "= null!" em vez de "required": o EF Core só precisa do UsuarioId (FK) para
    // salvar o registro. O objeto Usuario só vem preenchido quando a consulta usa .Include().
    public Usuario Usuario { get; set; } = null!;
}