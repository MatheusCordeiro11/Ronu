using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>
/// POST /api/consentimentos: o tipo e a versão do texto que a tela mostrou. Sem data nem usuário: a data é a do
/// servidor, e o usuário vem do token.
/// </summary>
public class ConsentimentoRequest
{
    [Required(ErrorMessage = "Informe o tipo de autorização.")]
    public required string Tipo { get; set; }

    [Required(ErrorMessage = "Informe a versão do texto aceito.")]
    public int? Versao { get; set; }
}

/// <summary>
/// O aceite do consentimento geral que vem junto do cadastro por email, gravado com a conta.
/// </summary>
public class ConsentimentoCadastroRequest
{
    [Required(ErrorMessage = "Informe a versão do texto aceito.")]
    public int? Versao { get; set; }
}

/// <summary>
/// GET /api/consentimentos/{tipo}: o texto vigente para a tela mostrar e o último aceite da pessoa.
/// </summary>
public class ConsentimentoResponse
{
    public required string Tipo { get; set; }
    public int VersaoVigente { get; set; }
    public required string Titulo { get; set; }
    public required string Texto { get; set; }

    // O aceite mais recente ainda não revogado, de qualquer versão; nulo se nunca aceitou.
    public AceiteResponse? Aceito { get; set; }

    // Verdadeiro sem aceite da versão vigente. Responde mesmo com Consentimento:Exigir desligado: o interruptor só
    // decide se a API barra, não o que a pessoa já aceitou.
    public bool PrecisaAceitar { get; set; }
}

/// <summary>
/// GET /api/consentimentos/vigente/{tipo}, sem login: só o texto vigente, para a caixa do cadastro.
/// </summary>
public class ConsentimentoTextoResponse
{
    public required string Tipo { get; set; }
    public int Versao { get; set; }
    public required string Titulo { get; set; }
    public required string Texto { get; set; }
}

public class AceiteResponse
{
    public int Versao { get; set; }
    public DateTime AceitoEm { get; set; }
}
