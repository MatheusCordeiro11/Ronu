using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Abstrai a geração de uma dieta semanal via IA, permitindo trocar o
/// provedor (Gemini hoje, Anthropic ou outro no futuro) sem alterar
/// o DietasController nem nenhuma outra camada que dependa desta interface.
/// </summary>
public interface IGeradorDietaIA
{
    Task<DietaSemanalDto> GerarDietaAsync(ContextoDietaDto contexto);
}
