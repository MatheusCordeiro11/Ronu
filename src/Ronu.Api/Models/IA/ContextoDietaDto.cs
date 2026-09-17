namespace Ronu.Api.Models.IA;

/// <summary>
/// Representa os dados do usuário necessários para a IA gerar uma dieta
/// semanal. Contém apenas o que é relevante para montar o prompt — nenhum
/// dado de autenticação, auditoria ou identificação de banco, seguindo o
/// princípio de minimização de dados.
/// </summary>
public class ContextoDietaDto
{
    public required decimal Altura { get; set; }
    public required string Sexo { get; set; }
    public required int Idade { get; set; }
    public required decimal Peso { get; set; }
    public required string Objetivo { get; set; }
    public required List<ModalidadeContextoDto> Modalidades { get; set; }
    public required List<PreferenciaContextoDto> Preferencias { get; set; }
}
