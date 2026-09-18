namespace Ronu.Api.DTOs;

/// <summary>
/// Dados pessoais do usuário. Os campos são opcionais aqui (diferente de
/// PerfilRequest) porque, antes do usuário completar o perfil, eles ainda
/// não existem no banco — o frontend usa essa nulidade para decidir se
/// deve pedir esses dados.
/// </summary>
public class PerfilResponse
{
    public decimal? Altura { get; set; }
    public string? Sexo { get; set; }
    public DateOnly? DataNascimento { get; set; }
}
