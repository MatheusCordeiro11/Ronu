using System.Text.Json;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;

namespace Ronu.Api.DTOs;

/// <summary>
/// A semana da casa vista por quem está nela (GET e POST /api/casa/semana). Só os nomes dos alimentos, SEM quantidade
/// nem kcal: a porção de cada um está só na dieta dele (GET /api/dietas/atual), e a quantidade da referência diria a
/// escala da porção de todos. Nada de nenhum membro: nem porção, nem meta, nem restrição.
/// </summary>
public class SemanaCasaResponse
{
    public int SemanaId { get; set; }
    public int Revisao { get; set; }
    public DateTime GeradaEm { get; set; }
    public required List<DiaSemanaCasaResponse> Dias { get; set; }

    // Só para o dono (nulos para os membros): a lista da casa ficou abaixo do mínimo, e alguém teve uma refeição
    // trocada por uma individual (sem dizer quem nem qual).
    public AvisoListaPobreDto? AvisoListaPobre { get; set; }
    public bool? RefeicaoIndividual { get; set; }

    public static SemanaCasaResponse Montar(SemanaCasa semana, bool souDono)
    {
        var conteudo = JsonSerializer.Deserialize<SemanaCasaDto>(semana.ConteudoJson)!;
        return new SemanaCasaResponse
        {
            SemanaId = semana.Id,
            Revisao = semana.Revisao,
            GeradaEm = semana.GeradaEm,
            Dias = conteudo.Dias.Select(d => new DiaSemanaCasaResponse
            {
                DiaSemana = d.DiaSemana,
                Almoco = Refeicao(d, almoco: true),
                Jantar = Refeicao(d, almoco: false)
            }).ToList(),
            AvisoListaPobre = souDono ? conteudo.AvisoListaPobre : null,
            RefeicaoIndividual = souDono ? semana.RefeicaoIndividualEm is not null : null
        };
    }

    private static RefeicaoSemanaCasaResponse Refeicao(DiaDietaDto dia, bool almoco)
    {
        var refeicao = dia.Refeicoes.First(r => r.ChaveCasa!.EndsWith(almoco ? "-almoco" : "-jantar", StringComparison.Ordinal));
        return new RefeicaoSemanaCasaResponse
        {
            ChaveCasa = refeicao.ChaveCasa!,
            Horario = refeicao.Horario,
            Alimentos = refeicao.Alimentos.Select(a => a.Nome).ToList()
        };
    }
}

public class DiaSemanaCasaResponse
{
    public required string DiaSemana { get; set; }
    public required RefeicaoSemanaCasaResponse Almoco { get; set; }
    public required RefeicaoSemanaCasaResponse Jantar { get; set; }
}

public class RefeicaoSemanaCasaResponse
{
    // A mesma chave da refeição na dieta de cada um ("seg-almoco"), para a tela ligar as duas.
    public required string ChaveCasa { get; set; }
    public string? Horario { get; set; }

    // Os nomes da lista, sem a contagem dos contáveis (a contagem é da porção de cada um).
    public required List<string> Alimentos { get; set; }
}
