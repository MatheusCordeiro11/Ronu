using Ronu.Api.Data;

namespace Ronu.Api.Services.IA;

/// <summary>
/// "Ritmo da aula" de uma modalidade praticada (UsuarioModalidade.Intensidade):
/// quanto do tempo da aula vai para o bloco intenso (rola, sparring, circuito).
/// Só existe nas modalidades modeladas como aula em dois blocos
/// (MetsCompendio2024.BlocosPorModalidade; no banco, Modalidades com
/// MetBlocoLeve e MetBlocoIntenso preenchidos). Nas outras, o MET é o de
/// referência em qualquer nível.
///
/// As chaves são as mesmas em todas as modalidades com blocos, inclusive no
/// treino funcional ("tecnica" = mais força, "sparring" = mais circuito): os
/// rótulos da tela saem da categoria, no frontend.
///
/// PREMISSA DE PRODUTO (não vem do Compêndio, decisão de 2026-11-16): as
/// frações 0,3 e 0,7 são simétricas em torno do 50/50 de hoje
/// (MetsCompendio2024.FracaoTempoBlocoIntenso), que também é premissa e nunca
/// foi validada. "Normal" reproduz exatamente o MET de referência, então quem
/// não mexe no seletor tem a mesma meta de antes. A meta adaptativa corrige o
/// que sobrar, até ±5%.
/// </summary>
public static class IntensidadeTreino
{
    public const string Tecnica = "tecnica";
    public const string Normal = "normal";
    public const string Sparring = "sparring";

    /// <summary>O ritmo de quem não escolheu (vínculo novo sem o campo, linhas de antes da coluna).</summary>
    public const string Padrao = Normal;

    /// <summary>Fração do tempo da aula no bloco intenso, por nível.</summary>
    public static readonly IReadOnlyDictionary<string, decimal> FracaoBlocoIntenso = new Dictionary<string, decimal>
    {
        [Tecnica] = 0.3m,
        [Normal] = MetsCompendio2024.FracaoTempoBlocoIntenso,
        [Sparring] = 0.7m
    };

    /// <summary>Os níveis aceitos, na ordem da tela.</summary>
    public static readonly IReadOnlyList<string> Niveis = [Tecnica, Normal, Sparring];

    public const string MensagemNivelInvalido = "Ritmo da aula inválido: use \"tecnica\", \"normal\" ou \"sparring\".";
    public const string MensagemSemRitmo = "Esta modalidade não tem ritmo da aula.";

    /// <summary>
    /// Confere o nível pedido para uma modalidade. Nulo é válido (quem chama
    /// decide: vínculo novo nasce "normal", atualização mantém o salvo). Vazio
    /// ou fora dos três dá erro. Nível diferente de "normal" numa modalidade
    /// sem blocos dá erro: a tela nem mostra o seletor nela.
    /// </summary>
    public static string? Validar(string? intensidade, bool temBlocos)
    {
        if (intensidade is null)
        {
            return null;
        }

        if (!FracaoBlocoIntenso.ContainsKey(intensidade))
        {
            return MensagemNivelInvalido;
        }

        return !temBlocos && intensidade != Normal ? MensagemSemRitmo : null;
    }

    /// <summary>
    /// MET usado na conta do treino. Sem blocos, ou em "normal", é o MET de
    /// referência (o de antes do seletor). Com blocos, a composição dos dois
    /// pela fração do nível, arredondada como MetsCompendio2024.Aula. Nível
    /// desconhecido (não deve chegar aqui: a API valida) conta como "normal".
    /// </summary>
    public static decimal MetEfetivo(decimal metReferencia, decimal? metBlocoLeve, decimal? metBlocoIntenso, string? intensidade)
    {
        if (metBlocoLeve is not { } leve || metBlocoIntenso is not { } intenso
            || intensidade is null || intensidade == Normal
            || !FracaoBlocoIntenso.TryGetValue(intensidade, out var fracao))
        {
            return metReferencia;
        }

        return MetsCompendio2024.Aula(leve, intenso, fracao);
    }
}
