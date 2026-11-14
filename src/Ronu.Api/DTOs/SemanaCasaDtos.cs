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

    // Etapa 5, só para o dono e só quando há o que desfazer: a última troca na casa ainda é a última gravação.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? PodeDesfazer { get; set; }

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
                Almoco = Refeicao(d, almoco: true, souDono),
                Jantar = Refeicao(d, almoco: false, souDono)
            }).ToList(),
            AvisoListaPobre = souDono ? conteudo.AvisoListaPobre : null,
            RefeicaoIndividual = souDono ? semana.RefeicaoIndividualEm is not null : null,
            PodeDesfazer = souDono && conteudo.UltimaTroca is { } ultima && ultima.Revisao == semana.Revisao ? true : null
        };
    }

    private static RefeicaoSemanaCasaResponse Refeicao(DiaDietaDto dia, bool almoco, bool souDono)
    {
        var refeicao = dia.Refeicoes.First(r => r.ChaveCasa!.EndsWith(almoco ? "-almoco" : "-jantar", StringComparison.Ordinal));
        var trocados = refeicao.Alimentos.Select((a, i) => (a, i)).Where(x => x.a.Original is not null).Select(x => x.i).ToList();
        return new RefeicaoSemanaCasaResponse
        {
            ChaveCasa = refeicao.ChaveCasa!,
            Horario = refeicao.Horario,
            Alimentos = refeicao.Alimentos.Select(a => a.Nome).ToList(),
            Trocados = souDono && trocados.Count > 0 ? trocados : null
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

    // Etapa 5, só para o dono: as posições dos itens trocados na casa (para a marca "trocado" e o "Voltar ao
    // original"). Fora do JSON quando nada foi trocado e para os membros.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? Trocados { get; set; }
}
