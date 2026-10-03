using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Divide a meta calórica de um dia em macronutrientes. Baseado em diretrizes
/// de nutrição esportiva (ACSM/ISSN), preenchendo por prioridade dentro das
/// calorias, nunca deixando sobrar carboidrato negativo ou baixo demais:
/// 1. Proteína 1,8 g/kg do peso de referência — sempre garantida (preserva
///    músculo num déficit).
/// 2. Carboidrato com piso de 100 g/dia — abaixo de ~50 g o corpo entra em
///    cetose, inadequado para luta (alta intensidade); 100 g mantém glicogênio
///    para o treino seguinte sem apagar o déficit (a RDA de 130 g anularia o
///    déficit de pessoas pequenas em dias de descanso).
/// 3. Gordura com alvo de 25% das calorias e mínimo de 0,6 g/kg do peso de
///    referência (o alvo nunca fica abaixo do mínimo) — quando falta caloria,
///    é ela que cede primeiro, até o mínimo. 25% fica perto do piso da faixa
///    de 20–35% da energia do posicionamento ACSM/AND/DC (Thomas, Erdman e
///    Burke, 2016), que desaconselha ficar abaixo de 20%: sobra espaço para o
///    carboidrato de quem treina em alta intensidade. Antes o alvo era fixo em
///    1,0 g/kg, e a gordura ia de 19% (dia de treino pesado) a 41% (descanso)
///    das calorias.
/// 4. Se nem proteína + piso de carboidrato + gordura mínima couberem, a meta
///    sobe para caber, e o dia é marcado (MetaElevadaPeloPiso).
///
/// Peso de referência = o menor entre o peso real e o peso de IMC 30 na mesma
/// altura: com obesidade, a necessidade de proteína acompanha a massa magra,
/// não o peso total. O corte em 30 (e não 25) é de propósito — lutadores
/// musculosos costumam ter IMC 26–29 sem excesso de gordura e não perdem
/// proteína com ele.
///
/// Causa da correção: antes, proteína e gordura usavam o peso total e o
/// carboidrato era só a sobra — em dias de descanso com déficit, para IMC
/// acima de ~33, ficava negativo (ex.: -32 g para uma mulher de 90 kg).
/// </summary>
public static class CalculadoraMacros
{
    public const decimal ProteinaGramasPorKg = 1.8m;
    public const decimal GorduraFracaoCalorias = 0.25m;
    public const decimal GorduraMinimaGramasPorKg = 0.6m;
    public const decimal CarboidratoMinimoGramas = 100m;
    public const decimal ImcMaximoReferencia = 30m;

    private const decimal CaloriasPorGramaProteina = 4m;
    private const decimal CaloriasPorGramaGordura = 9m;
    private const decimal CaloriasPorGramaCarboidrato = 4m;

    /// <param name="metaCalorias">Meta calórica do dia (já com objetivo e eventuais ajustes).</param>
    /// <param name="pesoKg">Peso corporal real.</param>
    /// <param name="alturaCm">Altura, para o peso de referência (IMC 30).</param>
    public static ResultadoMacros Calcular(decimal metaCalorias, decimal pesoKg, decimal alturaCm)
    {
        var alturaM = alturaCm / 100m;
        var pesoReferencia = Math.Min(pesoKg, ImcMaximoReferencia * alturaM * alturaM);

        var proteinaG = pesoReferencia * ProteinaGramasPorKg;
        var gorduraMinimaG = pesoReferencia * GorduraMinimaGramasPorKg;
        var gorduraAlvoG = Math.Max(metaCalorias * GorduraFracaoCalorias / CaloriasPorGramaGordura, gorduraMinimaG);

        var caloriasProteina = proteinaG * CaloriasPorGramaProteina;
        var caloriasPisoCarboidrato = CarboidratoMinimoGramas * CaloriasPorGramaCarboidrato;

        // Calorias sobrando para o piso de carboidrato e a gordura alvo: divisão
        // de sempre — carboidrato fica com o resto (e já fica acima do piso).
        if (metaCalorias >= caloriasProteina + caloriasPisoCarboidrato + gorduraAlvoG * CaloriasPorGramaGordura)
        {
            var carboidratoG = (metaCalorias - caloriasProteina - gorduraAlvoG * CaloriasPorGramaGordura) / CaloriasPorGramaCarboidrato;
            return new ResultadoMacros(Macros(metaCalorias, proteinaG, carboidratoG, gorduraAlvoG), MetaElevadaPeloPiso: false);
        }

        // Falta caloria: carboidrato fica no piso e a gordura cede, até o mínimo.
        var gorduraG = Math.Max(gorduraMinimaG, (metaCalorias - caloriasProteina - caloriasPisoCarboidrato) / CaloriasPorGramaGordura);
        var caloriasMinimas = caloriasProteina + caloriasPisoCarboidrato + gorduraG * CaloriasPorGramaGordura;

        // Nem o mínimo cabe: a meta sobe para acomodá-lo (casos raros e extremos).
        var metaElevada = caloriasMinimas > metaCalorias;
        var metaFinal = metaElevada ? caloriasMinimas : metaCalorias;

        return new ResultadoMacros(Macros(metaFinal, proteinaG, CarboidratoMinimoGramas, gorduraG), metaElevada);
    }

    private static MacrosDto Macros(decimal calorias, decimal proteinaG, decimal carboidratoG, decimal gorduraG) => new()
    {
        Calorias = calorias,
        ProteinasG = proteinaG,
        CarboidratosG = carboidratoG,
        GordurasG = gorduraG
    };
}

/// <summary>
/// Macros de um dia e se a meta calórica precisou subir para caber o mínimo
/// nutricional (proteína + 100 g de carboidrato + gordura mínima).
/// </summary>
public record ResultadoMacros(MacrosDto Macros, bool MetaElevadaPeloPiso);
