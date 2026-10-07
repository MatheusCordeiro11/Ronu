using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ronu.Api.Services.Email;

/// <summary>
/// Envio por SMTP autenticado (Gmail, conta dedicada, senha de app), com o
/// MailKit — o System.Net.Mail.SmtpClient é desaconselhado pela Microsoft.
/// O remetente é a própria conta: o Gmail assina o email (DKIM do gmail.com),
/// o que passa no DMARC e evita a caixa de spam.
/// </summary>
public class EnviadorEmailSmtp : IEnviadorEmail
{
    private readonly EmailOptions _opcoes;

    // Só é registrado com EmailOptions.Configurado (Program.cs): usuário e
    // senha de app nunca são nulos aqui.
    private string Usuario => _opcoes.Usuario!;
    private string SenhaApp => _opcoes.SenhaApp!;

    public EnviadorEmailSmtp(EmailOptions opcoes)
    {
        _opcoes = opcoes;
    }

    public async Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancelamento)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_opcoes.NomeRemetente, Usuario));
        email.To.Add(new MailboxAddress(mensagem.NomePara, mensagem.Para));
        email.Subject = mensagem.Assunto;
        email.Body = new BodyBuilder { TextBody = mensagem.Texto, HtmlBody = mensagem.Html }.ToMessageBody();

        using var smtp = new SmtpClient { Timeout = 30_000 };
        await smtp.ConnectAsync(_opcoes.Host, _opcoes.Porta, SecureSocketOptions.StartTls, cancelamento);
        await smtp.AuthenticateAsync(Usuario, SenhaApp, cancelamento);
        await smtp.SendAsync(email, cancelamento);
        await smtp.DisconnectAsync(true, cancelamento);
    }
}
