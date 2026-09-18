using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Gerencia a persistência do histórico de dietas geradas por IA de um
/// usuário, mantendo sempre só as 3 mais recentes: ao salvar uma nova dieta,
/// se o usuário já tiver 3 ou mais, a mais antiga é removida (política FIFO).
/// </summary>
public interface IRepositorioDietaIA
{
    Task SalvarAsync(int usuarioId, DietaSemanalDto dieta);
}
