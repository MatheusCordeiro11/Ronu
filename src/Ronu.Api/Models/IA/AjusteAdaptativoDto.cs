using System.Text.Json.Serialization;

namespace Ronu.Api.Models.IA;

/// <summary>
/// Resultado da meta calórica adaptativa para uma dieta gerada: o percentual
/// aplicado sobre a meta da fórmula (Mifflin-St Jeor + treino + objetivo) e o
/// motivo. Guardado junto da dieta (DietaSemanalDto) como registro de como ela
/// foi feita — nunca reaproveitado como estado no próximo cálculo, que é
/// sempre refeito do zero a partir do histórico de peso e das metas das dietas.
/// </summary>
public class AjusteAdaptativoDto
{
    // Fração sobre a meta da fórmula: -0.03 = -3%. 0 = sem ajuste (meta igual
    // à da fórmula).
    public required decimal Percentual { get; set; }

    public required MotivoAjusteAdaptativo Motivo { get; set; }

    public bool Aplicado => Percentual != 0;

    // Diagnóstico (dashboard/depuração). Nulos quando não houve dado suficiente
    // para calcular o ritmo — e também quando a meta prescrita na janela
    // diferiu da meta atual em mais de 1%: aí o "Saiba mais" atribuiria à
    // aderência o que é efeito da meta antiga, e o dashboard cai no texto
    // genérico (que continua verdadeiro).
    public decimal? RitmoRealKgSemana { get; set; }
    public decimal? RitmoEsperadoKgSemana { get; set; }

    // Registros de peso que entraram no cálculo (janela, objetivo atual, com
    // dieta, sem os dias "nao_seguiu").
    public int PontosUsados { get; set; }

    // Só para o log da geração: fora do JSON salvo e da API. Nulos quando o
    // cálculo parou antes (histórico insuficiente).
    [JsonIgnore] public decimal? FatorBruto { get; set; }
    [JsonIgnore] public decimal? ErroFormulaKcalDia { get; set; }
    [JsonIgnore] public decimal? IngestaoMediaDiaria { get; set; }
    [JsonIgnore] public decimal? ManutencaoMediaDiaria { get; set; }
    [JsonIgnore] public decimal? MetaPrescritaMediaDiaria { get; set; }
    [JsonIgnore] public int? DiasComDieta { get; set; }
}
