namespace Ronu.Api.DTOs;

/// <summary>
/// DELETE /api/conta: a confirmação forte. Conta com senha manda a senha; conta só com Google, que não tem senha,
/// digita a frase (ExclusaoContaRequest.FraseConfirmacao). Os dois são opcionais aqui porque qual vale depende da conta.
/// </summary>
public class ExclusaoContaRequest
{
    public const string FraseConfirmacao = "apagar minha conta";

    public string? Senha { get; set; }
    public string? Confirmacao { get; set; }
}
