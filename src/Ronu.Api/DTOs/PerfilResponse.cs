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
    public string? RotinaDiaria { get; set; }
    public string? OrcamentoSemanal { get; set; }

    // Nulo = a pessoa ainda não respondeu como é o dia fora do treino.
    public string? NivelAtividade { get; set; }

    // Dados da conta para o cabeçalho do Perfil (redesign de out/2026). Só
    // acrescentam campos: o frontend antigo ignora. Nunca a senha nem o hash,
    // só se existem.
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Sigla da UF; nulo (não vazio) em contas que ainda não informaram, como as
    // do login com Google, para o frontend não confundir vazio com preenchido.
    public string? Estado { get; set; }
    public bool TemSenha { get; set; }
    public bool TemGoogle { get; set; }
}
