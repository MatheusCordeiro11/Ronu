namespace Ronu.Api.DTOs;

/// <summary>
/// Dados enviados pelo cliente para criar uma nova conta de usuário.
/// Não inclui altura, data de nascimento ou sexo de propósito: esses dados
/// só são coletados depois, na etapa de onboarding.
/// </summary>
public class CadastroRequest
{
    public required string Nome { get; set; }
    public required string Email { get; set; }

    // Senha em texto puro recebida do cliente; é convertida em hash (BCrypt) no
    // controller antes de ser persistida, nunca é salva como está.
    public required string Senha { get; set; }
}