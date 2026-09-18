namespace Ronu.Api.Services.IA;

/// <summary>
/// Encapsula a chave de API da Gemini para permitir injeção de dependência
/// tipada (string solta não é registrável diretamente no container de DI).
/// </summary>
public class GeminiOptions
{
    public required string ApiKey { get; set; }
}
