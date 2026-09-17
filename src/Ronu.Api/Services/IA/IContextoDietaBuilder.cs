using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Abstrai a busca e montagem dos dados de um usuário necessários para gerar
/// uma dieta via IA. Isola o acesso ao banco de dados do DietasController,
/// permitindo testar a lógica do controller com uma implementação falsa
/// (mock), sem depender do PostgreSQL rodando.
/// </summary>
public interface IContextoDietaBuilder
{
    Task<ContextoDietaDto> ConstruirAsync(int usuarioId);
}
