using Ronu.Api.Services.IA;

namespace Ronu.Api.DTOs;

/// <summary>
/// Quanto da janela da meta adaptativa já está preenchido agora: pesagens que
/// contariam e quantos dias elas cobrem, pela mesma regra da geração da dieta
/// (CalculadoraAjusteAdaptativo.IndicesDaJanela). Vai no GET /objetivos/atual
/// para o card "Ajuste pelo progresso" do Progresso, que diz quanto falta.
/// </summary>
public class ProgressoAjusteDto
{
    public int Pesagens { get; set; }

    // Dias entre a primeira e a última pesagem que contam, para baixo.
    public int DiasCobertos { get; set; }

    // Se a próxima dieta já teria dados para ajustar a meta (o ajuste pode
    // sair 0% mesmo assim, se o progresso estiver no esperado).
    public bool Suficiente { get; set; }

    // Os mínimos e a janela vão juntos para o texto da tela não repetir números.
    public int MinimoPesagens => CalculadoraAjusteAdaptativo.MinimoPontos;
    public int MinimoDias => (int)CalculadoraAjusteAdaptativo.MinimoDiasIntervalo;
    public int JanelaDias => CalculadoraAjusteAdaptativo.JanelaDias;
}
