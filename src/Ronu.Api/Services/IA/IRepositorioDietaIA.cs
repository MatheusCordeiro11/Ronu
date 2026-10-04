using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Gerencia a persistência do histórico de dietas geradas por IA de um
/// usuário, mantendo sempre só as 3 mais recentes: ao salvar uma nova dieta,
/// se o usuário já tiver 3 ou mais, a mais antiga é removida (política FIFO).
/// As metas de cada dieta vão também para a tabela MetasDieta, que guarda
/// todas (a meta adaptativa precisa das metas da janela inteira).
/// </summary>
public interface IRepositorioDietaIA
{
    Task SalvarAsync(int usuarioId, DietaSemanalDto dieta);
}
