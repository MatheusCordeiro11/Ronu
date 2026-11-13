using System.ComponentModel.DataAnnotations;
using Ronu.Api.Validacao;

namespace Ronu.Api.DTOs;

/// <summary>
/// Dados pessoais usados para calcular a taxa metabólica basal (Mifflin-St
/// Jeor) e a idade do usuário. Sexo deve ser exatamente "Masculino" ou
/// "Feminino" — esse valor é comparado por igualdade de string no cálculo
/// de TMB (GeradorDietaGemini), então qualquer variação de grafia faz o
/// sistema calcular com a fórmula errada silenciosamente.
/// </summary>
public class PerfilRequest
{
    // Em centímetros. Mesma faixa da simulação (RegrasDadosCorporais).
    [Range(100.0, 250.0, ErrorMessage = RegrasDadosCorporais.MensagemAltura)]
    public required decimal Altura { get; set; }

    [Required(ErrorMessage = "Informe seu sexo.")]
    public required string Sexo { get; set; }

    public required DateOnly DataNascimento { get; set; }

    // Lido e ignorado (LGPD base, etapa 3 dos horários): a rotina em texto livre saiu da tela e do prompt, e a API
    // não grava mais o campo. Ele continua aqui, sem validação de tamanho, só para um frontend antigo em cache que
    // ainda o mande não receber 400 nem apagar o texto antigo sem querer. O texto antigo sai pelo
    // DELETE /api/perfil/rotina ou pela limpeza dos prazos de guarda (LimpezaRetencao).
    public string? RotinaDiaria { get; set; }
    public string? OrcamentoSemanal { get; set; }

    // "sentado", "em_pe", "andando" ou "bracal". Diferente dos outros campos,
    // ausente ou vazio NÃO apaga: mantém o valor salvo (não há opção de voltar
    // a "sem resposta", e outras telas reenviam o perfil sem este campo).
    public string? NivelAtividade { get; set; }

    // "HH:mm". Como o NivelAtividade, ausente ou nulo mantém o salvo; vazio apaga (RegrasHorario).
    public string? HorarioAcordar { get; set; }
    public string? HorarioDormir { get; set; }
}
