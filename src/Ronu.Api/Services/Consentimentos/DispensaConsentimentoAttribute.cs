namespace Ronu.Api.Services.Consentimentos;

/// <summary>
/// Rota autenticada que funciona sem o consentimento geral: a própria rota do aceite e, na etapa 2, apagar a conta e
/// baixar os dados, que são direitos de quem não aceitou também (art. 18). Toda outra rota com [Authorize] passa
/// pelo FiltroConsentimento. O teste FiltroConsentimentoTests confere a lista inteira de quem usa este atributo.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DispensaConsentimentoAttribute : Attribute;
