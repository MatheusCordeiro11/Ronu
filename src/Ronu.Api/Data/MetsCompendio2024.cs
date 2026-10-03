namespace Ronu.Api.Data;

/// <summary>
/// MET de referência de cada modalidade do catálogo, a partir do Compêndio de
/// Atividades Físicas 2024 para adultos (Herrmann et al., J Sport Health Sci
/// 2024; tabelas em https://pacompendium.com). Valores conferidos no site em
/// 2026-10-03. É a fonte dos valores gravados em "Modalidades"."MetReferencia"
/// pela migration AtualizaMetsCompendio2024 e do docs/seed-modalidades.sql.
///
/// Critério: o código que descreve uma sessão típica de treino de quem não é
/// atleta profissional — não uma luta nem uma competição.
///
/// O MET do Compêndio é bruto (inclui o repouso); o desconto de 1 MET é feito
/// na CalculadoraGastoCalorico, não aqui.
///
/// Para mudar um valor ou a premissa da aula: altere aqui e crie uma migration
/// NOVA que reaplique os valores (mesmo padrão da AtualizaMetsCompendio2024) —
/// a do banco de produção já rodou e não roda de novo.
/// </summary>
public static class MetsCompendio2024
{
    /// <summary>Um código do Compêndio 2024.</summary>
    public record Codigo(string Numero, string Descricao, decimal Met);

    // Artes marciais e boxe: o Compêndio não tem código para uma "aula" —
    // aquecimento e técnica de um lado, rola/sparring de outro. A aula é
    // modelada como a composição de dois códigos, pelo tempo de cada bloco.

    /// <summary>
    /// PREMISSA (não vem do Compêndio): fração do tempo da aula no bloco
    /// intenso (rola/sparring); o resto é aquecimento e técnica. 0,5 = metade
    /// do tempo em cada bloco.
    /// </summary>
    public const decimal FracaoTempoBlocoIntenso = 0.5m;

    public static readonly Codigo ArtesMarciaisTecnica =
        new("15425", "Martial Arts, different types, slower pace, novice performers, practice", 5.3m);
    public static readonly Codigo ArtesMarciaisRola =
        new("15430", "Martial Arts, different types, moderate pace (e.g., judo, jujitsu, karate, kick boxing, tae kwon do, tai-bo, Muay Thai boxing)", 10.3m);

    public static readonly Codigo BoxeSacoDePancadas = new("15110", "Boxing, punching bag", 5.8m);
    public static readonly Codigo BoxeSparring = new("15120", "Boxing, sparring", 7.8m);

    public static readonly Codigo Musculacao =
        new("02054", "Resistance (weight) training, multiple exercises, 8-15 reps at varied resistance", 3.5m);
    public static readonly Codigo Futebol = new("15610", "Soccer, casual, general (Taylor Code 540)", 7.0m);
    public static readonly Codigo Basquete = new("15055", "Basketball, general", 7.5m);
    public static readonly Codigo Natacao = new("18240", "Swimming laps, freestyle, slow, recreational", 5.8m);

    /// <summary>MET de uma aula com um bloco leve e um intenso, pela premissa de tempo.</summary>
    public static decimal Aula(Codigo blocoLeve, Codigo blocoIntenso) =>
        Math.Round((1 - FracaoTempoBlocoIntenso) * blocoLeve.Met + FracaoTempoBlocoIntenso * blocoIntenso.Met, 2);

    /// <summary>
    /// MET por nome de modalidade, como gravado em "Modalidades"."Nome".
    /// Com a premissa de 50/50: jiu-jitsu e muay thai 7,8; boxe 6,8.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> PorModalidade => new Dictionary<string, decimal>
    {
        ["Jiu-jitsu"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Muay Thai"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Boxe"] = Aula(BoxeSacoDePancadas, BoxeSparring),
        ["Musculação"] = Musculacao.Met,
        ["Futebol"] = Futebol.Met,
        ["Basquete"] = Basquete.Met,
        ["Natação"] = Natacao.Met
    };
}
