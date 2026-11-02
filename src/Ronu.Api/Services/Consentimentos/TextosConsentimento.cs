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
            // art. 11, I); os termos são uma frase à parte, na tela. Em 2026-11-02 o Ronu ficou no plano gratuito do
            // Gemini, e a caixa passou a dizer o que esse plano permite ao Google (termos da Gemini API, "Unpaid Services").
            // Ainda em 2026-11-02, com o prompt sem dado pessoal, a caixa diz o que vai e o que não vai para a IA, em 4
            // linhas curtas (separadas por \n; as telas mostram as quebras com white-space: pre-line).
            [1] = new Texto(
                "Autorizo o Ronu a usar meus dados de saúde.",
                "O Ronu usa seu peso, altura, idade, sexo, objetivo e treinos para calcular sua meta de cada dia e mostrar "
                + "seu progresso.\n"
                + "Para montar o cardápio, a IA do Google (Gemini) recebe só a meta de cada dia, se o dia é de déficit, "
                + "manutenção ou superávit, os dias e horários de treino, seus horários de acordar e dormir, seu estado, seu "
                + "orçamento e os alimentos que você gosta ou evita. Seu nome, e-mail, peso, altura, idade e sexo não vão para "
                + "a IA.\n"
                + "O Ronu usa o plano gratuito do Gemini: o Google pode usar o que recebe para melhorar os produtos dele, e "
                + "revisores humanos podem ler esse conteúdo. O Ronu não envia nada que identifique você.\n"
                + "Seus dados ficam guardados no Chile (Microsoft Azure); o que vai para a IA passa pelos EUA (Google). Você "
                + "pode apagar tudo a qualquer momento no Perfil. Antes de apagar, você pode baixar seus dados no Perfil.")
        }
    };

    public static bool TipoValido(string? tipo) => tipo is not null && Textos.ContainsKey(tipo);

    public static int VersaoVigente(string tipo) => Textos[tipo].Keys.Max();

    public static Texto TextoVigente(string tipo) => Textos[tipo][VersaoVigente(tipo)];
}
