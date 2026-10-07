using System.Net;

namespace Ronu.Api.Services.Email;

/// <summary>
/// Textos dos emails, em texto puro e em HTML simples. O nome da pessoa e o
/// link são escapados no HTML.
/// </summary>
public static class ModelosEmail
{
    public static MensagemEmail LinkRedefinicaoSenha(string para, string nome, string link, int minutosValidade)
    {
        const string assunto = "Redefinição de senha do Ronu";

        var texto = $"""
            Olá, {nome}.

            Recebemos um pedido para redefinir a senha da sua conta no Ronu. Para criar uma senha nova, abra o link abaixo. Ele vale por {minutosValidade} minutos e só pode ser usado uma vez.

            {link}

            Se você não pediu isso, pode ignorar este email: sua senha continua a mesma.

            Equipe Ronu
            """;

        var nomeHtml = WebUtility.HtmlEncode(nome);
        var linkHtml = WebUtility.HtmlEncode(link);
        var html = $"""
            <p>Olá, {nomeHtml}.</p>
            <p>Recebemos um pedido para redefinir a senha da sua conta no Ronu. Para criar uma senha nova, abra o link abaixo. Ele vale por {minutosValidade} minutos e só pode ser usado uma vez.</p>
            <p><a href="{linkHtml}">Criar uma senha nova</a></p>
            <p>Se o botão não funcionar, copie este endereço no navegador:<br>{linkHtml}</p>
            <p>Se você não pediu isso, pode ignorar este email: sua senha continua a mesma.</p>
            <p>Equipe Ronu</p>
            """;

        return new MensagemEmail(para, nome, assunto, texto, html);
    }

    public static MensagemEmail AvisoContaGoogle(string para, string nome)
    {
        const string assunto = "Redefinição de senha do Ronu";

        var texto = $"""
            Olá, {nome}.

            Recebemos um pedido para redefinir a senha da sua conta no Ronu, mas ela usa o login com Google e não tem senha própria. Para entrar, use o botão "Entrar com Google" na tela de login.

            Se você não pediu isso, pode ignorar este email.

            Equipe Ronu
            """;

        var nomeHtml = WebUtility.HtmlEncode(nome);
        var html = $"""
            <p>Olá, {nomeHtml}.</p>
            <p>Recebemos um pedido para redefinir a senha da sua conta no Ronu, mas ela usa o login com Google e não tem senha própria. Para entrar, use o botão "Entrar com Google" na tela de login.</p>
            <p>Se você não pediu isso, pode ignorar este email.</p>
            <p>Equipe Ronu</p>
            """;

        return new MensagemEmail(para, nome, assunto, texto, html);
    }
}
