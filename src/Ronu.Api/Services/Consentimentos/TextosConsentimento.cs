namespace Ronu.Api.Services.Consentimentos;

/// <summary>
/// Os textos de consentimento, por tipo e versão. A tela mostra o texto que vem daqui (GET /api/consentimentos/{tipo})
/// e manda de volta a versão que mostrou; a API só aceita a vigente. Assim o texto lido sempre bate com a versão
/// gravada, e o histórico do git guarda o que cada versão dizia.
///
/// Mudar o que um texto já publicado diz pede uma versão nova, e todo mundo aceita de novo no próximo acesso. Erro de
/// digitação não pede. Antes da primeira publicação (a v1 ainda não foi para produção), a v1 pode mudar sem subir.
/// </summary>
public static class TextosConsentimento
{
    public const string TipoGeral = "geral";

    public record Texto(string Titulo, string Corpo);

    // Tipo → versão → texto. A vigente é a maior versão do tipo.
    private static readonly Dictionary<string, Dictionary<int, Texto>> Textos = new(StringComparer.Ordinal)
    {
        [TipoGeral] = new()
        {
            // Rascunho aprovado em 2026-11-01. A caixa do dado de saúde fica sozinha e desmarcada (art. 8º, §1º e
            // art. 11, I); os termos são uma frase à parte, na tela.
            [1] = new Texto(
                "Autorizo o Ronu a usar meus dados de saúde.",
                "O Ronu usa seu peso, altura, idade, sexo, objetivo, treinos e preferências alimentares para calcular sua meta, "
                + "montar o cardápio com a IA do Google (Gemini) e mostrar seu progresso. Seu nome e e-mail não vão para a IA. "
                + "Esses dados ficam guardados fora do Brasil, no Chile (Microsoft Azure) e nos EUA (Google). "
                + "Você pode apagar tudo a qualquer momento no Perfil. Antes de apagar, você pode baixar seus dados no Perfil.")
        }
    };

    public static bool TipoValido(string? tipo) => tipo is not null && Textos.ContainsKey(tipo);

    public static int VersaoVigente(string tipo) => Textos[tipo].Keys.Max();

    public static Texto TextoVigente(string tipo) => Textos[tipo][VersaoVigente(tipo)];
}
