using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Ronu.Api.Validacao;

/// <summary>
/// Regras da senha, as mesmas no cadastro e na redefinição: mínimo de 8
/// caracteres e máximo de 64. O máximo existe por causa do BCrypt, que só
/// considera os primeiros 72 bytes: acima disso, o resto da senha seria
/// ignorado sem aviso. Por isso o limite também confere os bytes (acentos e
/// símbolos ocupam mais de 1 byte).
/// </summary>
public static class RegrasSenha
{
    public const int TamanhoMinimo = 8;
    public const int TamanhoMaximo = 64;
    public const int BytesMaximosBcrypt = 72;

    public const string MensagemCurta = "A senha deve ter pelo menos 8 caracteres.";
    public const string MensagemLonga = "A senha deve ter no máximo 64 caracteres (ou menos, se tiver muitos acentos ou símbolos).";
}

/// <summary>Senha com no máximo 64 caracteres e 72 bytes (RegrasSenha).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SenhaNaoLongaDemaisAttribute : ValidationAttribute
{
    public SenhaNaoLongaDemaisAttribute()
    {
        ErrorMessage = RegrasSenha.MensagemLonga;
    }

    public override bool IsValid(object? value) =>
        value is not string senha
        || (senha.Length <= RegrasSenha.TamanhoMaximo && Encoding.UTF8.GetByteCount(senha) <= RegrasSenha.BytesMaximosBcrypt);
}
