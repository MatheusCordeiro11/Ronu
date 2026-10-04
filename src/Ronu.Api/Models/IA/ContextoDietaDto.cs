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
    // Peso de tendência (ContextoDietaBuilder.PesoDeTendencia), não a última pesagem.
    public required decimal Peso { get; set; }
    public required string Objetivo { get; set; }
    public required List<ModalidadeContextoDto> Modalidades { get; set; }
    public required List<PreferenciaContextoDto> Preferencias { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? RotinaDiaria { get; set; }
    public string? OrcamentoSemanal { get; set; }

    // Histórico de peso e aderência para a meta calórica adaptativa. Entra só
    // no cálculo das metas (GeradorDietaGemini), não no prompt.
    public List<RegistroPesoAderenciaDto> HistoricoPeso { get; set; } = new();

    // Metas de cada dieta já gerada (MetasDieta), para a meta adaptativa saber
    // o que a pessoa recebeu para comer em cada período. Também fora do prompt.
    public List<MetaDietaRegistroDto> HistoricoMetas { get; set; } = new();
}
