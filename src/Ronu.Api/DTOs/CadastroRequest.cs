using System.ComponentModel.DataAnnotations;

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
    // MinLength valida o tamanho automaticamente (ApiController já habilita
    // validação de ModelState) — se a senha vier curta, a API devolve 400
    // sozinha, sem precisar de checagem manual no AuthController.
    [MinLength(8, ErrorMessage = "A senha deve ter pelo menos 8 caracteres.")]
    public required string Senha { get; set; }

    public required string Estado { get; init; }
}
