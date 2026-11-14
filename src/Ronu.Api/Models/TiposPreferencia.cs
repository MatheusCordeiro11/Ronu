namespace Ronu.Api.Models;

/// <summary>
/// Os tipos de PreferenciaAlimentar (restrições em dois níveis, plano família etapa 3, decisão D2):
///
/// - "preferido": gosto. Peso a favor no prompt, sem garantia em C#.
/// - "nao_como": bloqueio. Sai da lista fechada e do enum do Gemini, da troca e conta no aviso de lista pobre.
///   É exatamente o "evitar" de antes da etapa 3: a migration PreferenciasEmDoisNiveis moveu todos os "evitar"
///   que existiam para cá, e assim nenhuma dieta individual mudou.
/// - "evitar": o nível leve ("Prefiro evitar"). O item continua na lista e no enum; o prompt pede para usar pouco
///   e o C# confere o limite por semana (GeradorDietaGemini.MaximoPorSemanaEvitar).
///
/// Sem normalização (como a aderência): vêm de opções fixas na tela, e qualquer outra grafia ("Evitar") ficaria
/// salva mas seria ignorada na dieta. Por isso o controller recusa o que não está em Todos.
/// </summary>
public static class TiposPreferencia
{
    public const string Preferido = "preferido";
    public const string NaoComo = "nao_como";
    public const string Evitar = "evitar";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.Ordinal) { Preferido, NaoComo, Evitar };
}
