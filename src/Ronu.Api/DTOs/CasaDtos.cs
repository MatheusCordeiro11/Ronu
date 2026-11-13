using System.ComponentModel.DataAnnotations;

namespace Ronu.Api.DTOs;

/// <summary>POST /api/casa: a versão do texto da casa que a tela mostrou e o orçamento (opcional).</summary>
public class CriarCasaRequest
{
    // Opcional no binding: a ausência vira o 409 com o motivo, para a tela abrir a caixa (e não um 400 genérico).
    public int? VersaoConsentimento { get; set; }

    public string? Orcamento { get; set; }
}

/// <summary>PUT /api/casa/orcamento. Nulo ou vazio = sem orçamento.</summary>
public class OrcamentoCasaRequest
{
    public string? Orcamento { get; set; }
}

/// <summary>POST /api/casa/convites/verificar.</summary>
public class VerificarConviteRequest
{
    [Required(ErrorMessage = "Informe o convite.")]
    public string? Token { get; set; }
}

/// <summary>POST /api/casa/convites/aceitar.</summary>
public class AceitarConviteRequest
{
    [Required(ErrorMessage = "Informe o convite.")]
    public string? Token { get; set; }

    public int? VersaoConsentimento { get; set; }
}

/// <summary>POST /api/casa/transferencia.</summary>
public class TransferenciaCasaRequest
{
    [Required(ErrorMessage = "Escolha para quem passar a casa.")]
    public int? MembroId { get; set; }
}

/// <summary>
/// A casa vista por quem está nela. Sobre os outros, só o primeiro nome e o papel: nenhum e-mail, sobrenome, idade,
/// peso, altura, sexo, objetivo, meta ou treino (um teste serializa a resposta e procura essas palavras).
/// </summary>
public class CasaResponse
{
    public bool SouDono { get; set; }
    public required List<MembroCasaResponse> Membros { get; set; }
    public string? Orcamento { get; set; }

    // Só para o dono; nulo para os membros.
    public List<ConviteAbertoResponse>? ConvitesAbertos { get; set; }
}

public class MembroCasaResponse
{
    public int MembroId { get; set; }
    public required string PrimeiroNome { get; set; }
    public bool Dono { get; set; }
    public bool SouEu { get; set; }
    public bool TransferenciaPendente { get; set; }
}

public class ConviteAbertoResponse
{
    public int Id { get; set; }
    public DateTime ExpiraEm { get; set; }
}

/// <summary>POST /api/casa/convites: o link aparece só aqui, uma vez (o banco guarda só o hash do token).</summary>
public class ConviteCriadoResponse
{
    public required string Link { get; set; }
    public DateTime ExpiraEm { get; set; }
}

/// <summary>POST /api/casa/convites/verificar, quando o convite serve.</summary>
public class ConviteVerificadoResponse
{
    public required string PrimeiroNomeDono { get; set; }

    // A contagem, não os nomes: quem ainda não entrou não vê quem está na casa.
    public int Membros { get; set; }

    public required ConsentimentoTextoResponse Consentimento { get; set; }
}
