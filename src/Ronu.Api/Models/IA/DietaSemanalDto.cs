using System.Text.Json.Serialization;

namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa a dieta semanal gerada pela IA. Não tem mais uma meta única
/// para a semana toda — cada DiaDietaDto carrega sua própria MetaCalculada,
/// já que dias de treino têm meta diferente de dias de descanso.
/// </summary>
public class DietaSemanalDto
{
    public required List<DiaDietaDto> Dias { get; set; }

    // Como a meta adaptativa atuou nesta dieta (registro histórico, para o
    // dashboard). Nulo em dietas geradas antes da meta adaptativa existir.
    public AjusteAdaptativoDto? AjusteAdaptativo { get; set; }

    // Versão da base nutricional da lista usada (BaseNutricional.VersaoBase),
    // para uma mudança de valores na base não reescrever o passado. Nulo nas
    // dietas geradas antes da lista fechada (fase 2b).
    public int? VersaoBase { get; set; }

    // Lista abaixo do mínimo por papel no prato (MinimosPorPapel), mas sem papel
    // vazio: a dieta é gerada e o aviso fica gravado para o dashboard (fase 2b-3).
    // Nulo quando a lista atende aos mínimos e nas dietas antigas.
    public AvisoListaPobreDto? AvisoListaPobre { get; set; }

    // Manutenção da fórmula de cada dia (índice 0 = segunda), só para o
    // RepositorioDietaIA gravar em MetaDieta. Fora do JSON salvo e da resposta
    // da API: a tabela MetasDieta é o lugar dela.
    [JsonIgnore]
    public decimal[]? ManutencaoPorDia { get; set; }

    // Fator de atividade fora do treino usado nesta dieta, também só para o
    // RepositorioDietaIA gravar em MetaDieta.
    [JsonIgnore]
    public decimal? FatorAtividade { get; set; }

    // Cerveja fora das preferências tirada na última tentativa
    // (ValidadorBebidaAlcoolica.TirarNaoPermitidas), só para o DietasController
    // registrar no log com o id do usuário. Fora do JSON salvo e da resposta.
    [JsonIgnore]
    public List<Ronu.Api.Services.IA.BebidaRemovida> BebidasRemovidas { get; set; } = [];
}
