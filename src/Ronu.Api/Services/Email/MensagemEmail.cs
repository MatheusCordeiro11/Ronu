namespace Ronu.Api.Services.Email;

/// <summary>Um email pronto para envio, em texto puro e em HTML.</summary>
public record MensagemEmail(string Para, string NomePara, string Assunto, string Texto, string Html);
