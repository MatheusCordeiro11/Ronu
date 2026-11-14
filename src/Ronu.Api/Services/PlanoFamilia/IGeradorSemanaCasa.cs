using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// A geração da semana da casa (plano família, etapa 4): uma chamada ao Gemini por semana, só com a lista da casa, as
/// regras do cardápio e o orçamento da casa. Implementada pelo GeradorDietaGemini (arquivo GeradorDietaGemini.Casa.cs),
/// que já tem a chamada, a validação e as regras que valem para as duas gerações.
/// </summary>
public interface IGeradorSemanaCasa
{
    /// <param name="listaDaCasa">A interseção das listas de todos (ListaDaCasa), já sem o "não como" e as alergias.</param>
    /// <param name="orcamento">O orçamento da casa (os 3 valores do perfil), ou nulo.</param>
    /// <param name="ufDoDono">Só para a regra do feijão preto no Sul; a UF não vai escrita no prompt.</param>
    Task<SemanaCasaDto> GerarSemanaCasaAsync(ListaAlimentos listaDaCasa, string? orcamento, string? ufDoDono);

    /// <summary>
    /// As metas de cada dia da pessoa, como na geração individual (fórmula, meta adaptativa e piso), sem chamar o
    /// Gemini: a semana da casa cria uma dieta nova para cada membro, com a MetaDieta dela.
    /// </summary>
    MetasDaSemana CalcularMetas(ContextoDietaDto contexto);
}
