namespace Ronu.Api.Services.IA;

/// <summary>
/// A IA não respondeu dentro do limite de cada tentativa, ou respondeu (HTTP
/// 200) mas a resposta não serve para montar a dieta: bloqueada (sem
/// candidates), cortada ou interrompida (finishReason diferente de STOP), JSON
/// inválido, ou dias fora do esperado (diferente de 7, repetidos ou com nome
/// desconhecido). O GeradorDietaGemini tenta de novo uma vez antes de lançá-la;
/// o DietasController a transforma no 503 amigável.
/// </summary>
public class RespostaIaInvalidaException : Exception
{
    public RespostaIaInvalidaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
