using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Apoio dos testes do gerador: desde a TACO 2c, o gerador recusa um contexto sem lista (a lista vem do ponto único,
/// ListaDaPessoa, com as alergias). Os testes que montam o contexto à mão, sem banco, ganham aqui a lista da UF e das
/// preferências do próprio contexto, sem alergias, só durante a chamada.
/// </summary>
internal static class GeradorComLista
{
    public static async Task<DietaSemanalDto> GerarComListaAsync(this IGeradorDietaIA gerador, ContextoDietaDto contexto)
    {
        var montada = contexto.Lista is null;
        if (montada)
        {
            contexto.Lista = ListaAlimentosPessoa.Montar(contexto.Estado, contexto.Preferencias, Alergenos.Nenhum);
        }

        try
        {
            return await gerador.GerarDietaAsync(contexto);
        }
        finally
        {
            if (montada)
            {
                contexto.Lista = null;
            }
        }
    }
}
