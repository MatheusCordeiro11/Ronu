using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;

namespace Ronu.Api.Services.Consentimentos;

/// <summary>
/// Quando um aceite vale e se a API exige o consentimento geral.
/// </summary>
public static class RegrasConsentimento
{
    public const string ChaveExigir = "Consentimento:Exigir";

    public const string MensagemSemAceite = "Para continuar, autorize o uso dos seus dados de saúde.";
    public const string MotivoSemAceite = "consentimento";
    public const string MensagemVersaoMudou = "O texto da autorização mudou. Leia de novo e aceite a versão nova.";
    public const string MensagemCadastroSemAceite = "Para criar a conta, autorize o uso dos seus dados de saúde.";

    // Ligado quando a chave falta: esquecer a configuração não pode desligar o consentimento. Só se desliga com
    // "false" explícito no App Service, durante a publicação (passo a passo em docs/privacidade.md).
    public static bool Exigir(IConfiguration configuracao) => configuracao.GetValue<bool?>(ChaveExigir) ?? true;

    // O aceite vale se é da versão vigente e não foi revogado. Uma versão nova invalida todos os aceites anteriores.
    public static Task<bool> TemAceiteVigenteAsync(ApplicationDbContext contexto, int usuarioId, string tipo)
    {
        var vigente = TextosConsentimento.VersaoVigente(tipo);
        return contexto.Consentimentos.AnyAsync(c =>
            c.UsuarioId == usuarioId && c.Tipo == tipo && c.Versao == vigente && c.RevogadoEm == null);
    }
}
