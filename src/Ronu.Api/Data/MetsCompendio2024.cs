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

    // Catálogo ampliado (migration AmpliaCatalogoModalidades, valores conferidos
    // no site em 2026-10-19).
    //
    // Lutas sem código próprio de treino usam a mesma aula das artes marciais
    // (15425 + 15430): judô, karatê, kickboxing e taekwondo estão citados no
    // 15430; MMA, wrestling e capoeira não têm código de treino. Os códigos
    // específicos que existem descrevem luta, não treino, e ficam de fora pelo
    // critério: 15730 "Wrestling, competitive (one match = 5 minutes)" 6,0;
    // 15432 "Taekwondo, combat simulation" 14,3. O 15433 "Judo" 11,3 não diz o
    // tipo de treino, e o 15457 "Kickboxing" 7,3 ficaria diferente do Muay
    // Thai, de treino quase igual.

    // Vôlei: o recreativo (15710, 4,0) subestima quem treina. Média da quadra
    // competitiva e da praia, porque "Vôlei" cobre as duas.
    public static readonly Codigo VoleiQuadra = new("15711", "Volleyball, competitive, in gymnasium", 6.0m);
    public static readonly Codigo VoleiPraia = new("15725", "Volleyball, beach, in sand", 8.0m);
    public static readonly Codigo Futsal = new("15195", "Futsal", 7.8m);
    // O 15320 "Handball, general (Taylor Code 520)" 12,0 é o handebol americano
    // (de parede), outro esporte.
    public static readonly Codigo Handebol = new("15330", "Handball, team", 8.0m);
    public static readonly Codigo Tenis = new("15675", "Tennis, general, moderate effort", 6.8m);
    // Beach tennis não tem código: é jogado em dupla; a areia pesa, mas os
    // pontos são curtos.
    public static readonly Codigo TenisDuplas = new("15680", "Tennis, doubles (Taylor Code 430)", 6.0m);

    // Corrida no ritmo de quem não é atleta (o 12145 "Running, self-selected
    // pace" 10,5 é ritmo de corredor).
    public static readonly Codigo Corrida = new("12020", "Jogging, general, self-selected pace", 7.5m);
    public static readonly Codigo Ciclismo = new("01030", "Bicycling, 12-13.9 mph, leisure, moderate effort", 8.0m);
    public static readonly Codigo Caminhada =
        new("17200", "Walking, 3.5 to 3.9 mph, level, brisk, firm surface, walking for exercise", 4.8m);
    // Treino funcional não tem código: aula com um bloco de força (Musculacao)
    // e um de circuito, pela mesma premissa de tempo das lutas.
    public static readonly Codigo Circuito =
        new("02040", "Circuit training, including kettlebells, some aerobic movement with minimal rest", 7.5m);

    public static readonly Codigo Danca = new("03010", "Ballet, modern, or jazz general, rehearsal or class", 5.0m);
    public static readonly Codigo Yoga = new("02175", "Yoga, General", 2.3m);
    public static readonly Codigo Pilates = new("02105", "Pilates, general", 2.8m);

    /// <summary>MET de uma aula com um bloco leve e um intenso, pela premissa de tempo.</summary>
    public static decimal Aula(Codigo blocoLeve, Codigo blocoIntenso) =>
        Math.Round((1 - FracaoTempoBlocoIntenso) * blocoLeve.Met + FracaoTempoBlocoIntenso * blocoIntenso.Met, 2);

    /// <summary>Média simples de dois códigos (sem premissa de tempo).</summary>
    public static decimal Media(Codigo a, Codigo b) => Math.Round((a.Met + b.Met) / 2, 2);

    /// <summary>
    /// MET por nome de modalidade, como gravado em "Modalidades"."Nome".
    /// Com a premissa de 50/50: lutas 7,8 (boxe 6,8); treino funcional 5,5.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> PorModalidade => new Dictionary<string, decimal>
    {
        ["Jiu-jitsu"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Muay Thai"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Boxe"] = Aula(BoxeSacoDePancadas, BoxeSparring),
        ["Musculação"] = Musculacao.Met,
        ["Futebol"] = Futebol.Met,
        ["Basquete"] = Basquete.Met,
        ["Natação"] = Natacao.Met,

        ["Judô"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Karatê"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["MMA"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Wrestling"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Taekwondo"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Kickboxing"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Capoeira"] = Aula(ArtesMarciaisTecnica, ArtesMarciaisRola),
        ["Vôlei"] = Media(VoleiQuadra, VoleiPraia),
        ["Futsal"] = Futsal.Met,
        ["Handebol"] = Handebol.Met,
        ["Tênis"] = Tenis.Met,
        ["Beach tennis"] = TenisDuplas.Met,
        ["Corrida"] = Corrida.Met,
        ["Ciclismo"] = Ciclismo.Met,
        ["Caminhada"] = Caminhada.Met,
        ["Treino funcional"] = Aula(Musculacao, Circuito),
        ["Dança"] = Danca.Met,
        ["Yoga"] = Yoga.Met,
        ["Pilates"] = Pilates.Met
    };
}
