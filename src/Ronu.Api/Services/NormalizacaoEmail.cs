namespace Ronu.Api.Services;

/// <summary>
/// Forma única de guardar e comparar emails: sem espaços nas pontas e em
/// minúsculas. Sem ela, "Fulano@Gmail.com" e "fulano@gmail.com" viravam
/// contas diferentes, e o login falhava se a pessoa digitasse com outra caixa.
/// Vale para o cadastro, o login, o login com Google e a redefinição de senha;
/// o índice único de "Usuarios"."Email" garante que não haja duas contas.
/// </summary>
public static class NormalizacaoEmail
{
    public static string Normalizar(string email) => email.Trim().ToLowerInvariant();
}
