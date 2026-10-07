namespace Ronu.Api.Services.Email;

/// <summary>
/// Configuração do envio de email (seção "Email"). Usuario e SenhaApp vêm dos
/// User Secrets (local) ou das Application settings do App Service
/// (Email__Usuario, Email__SenhaApp) — nunca do appsettings nem de arquivo.
/// SMTP do Gmail com senha de app: porta 587 com STARTTLS (o Azure só bloqueia
/// a 25).
/// </summary>
public class EmailOptions
{
    public const string Secao = "Email";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Porta { get; set; } = 587;
    public string? Usuario { get; set; }
    public string? SenhaApp { get; set; }
    public string NomeRemetente { get; set; } = "Ronu";

    public bool Configurado => !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(SenhaApp);
}
