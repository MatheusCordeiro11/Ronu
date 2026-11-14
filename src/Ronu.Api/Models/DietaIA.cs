using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.Models;

/// <summary>
/// Dieta gerada por IA para um usuário, salva em um determinado momento.
/// O conteúdo é armazenado como JSON bruto para manter flexibilidade no formato
/// retornado pela IA, sem precisar de uma tabela relacional rígida para cada campo.
/// </summary>
public class DietaIA
{
    // "pessoa": gerada pelo próprio usuário (POST /api/dietas/gerar). "casa": criada para cada membro quando o dono
    // gera a semana da casa (plano família, etapa 4). A diferença importa no limite de 3 gerações por hora: a dieta
    // que a casa cria para um membro não gasta a cota dele, só a do dono, que pediu.
    public const string OrigemPessoa = "pessoa";
    public const string OrigemCasa = "casa";

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

    [MaxLength(10)]
    public string Origem { get; set; } = OrigemPessoa;

    // A semana da casa de onde vieram o almoço e o jantar desta dieta; nulo fora da casa. Sem chave estrangeira, como
    // a MetaDieta.DietaIAId: a casa guarda só as 2 semanas mais recentes, e a dieta continua depois que a semana sai.
    public int? SemanaCasaId { get; set; }
}