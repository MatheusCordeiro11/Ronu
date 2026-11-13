namespace Ronu.Api.Validacao;

/// <summary>
/// Os valores do orçamento semanal, os mesmos no perfil e na casa: a regra do prompt só entende estes três.
/// </summary>
public static class RegrasOrcamento
{
    // Valores fixos das pills da tela, sem normalização (não vêm de digitação livre).
    public static readonly IReadOnlySet<string> Valores = new HashSet<string> { "economico", "moderado", "sem_restricao" };

    public const string MensagemInvalido = "Orçamento semanal inválido.";

    // Nulo ou vazio = não informado.
    public static bool Valido(string? orcamento) => string.IsNullOrEmpty(orcamento) || Valores.Contains(orcamento);
}
