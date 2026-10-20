using Ronu.Api.Services.IA;

namespace Ronu.Api.Validacao;

/// <summary>
/// Regras únicas dos dados que entram na fórmula da meta, aplicadas no
/// PUT /api/perfil, no POST /api/objetivos e no POST /api/metas/simulacao.
/// Antes as faixas existiam só no frontend, e a API gravava qualquer valor:
/// um sexo diferente de "Masculino" exato caía na fórmula feminina sem aviso,
/// e um objetivo desconhecido virava "manter peso" no cálculo.
/// As faixas de altura e peso também estão nos atributos [Range] dos DTOs
/// (PerfilRequest, ObjetivoRequest, SimulacaoMetaRequest), com as mesmas
/// mensagens daqui.
/// </summary>
public static class RegrasDadosCorporais
{
    // Os valores que a fórmula entende (comparação exata, como no cálculo).
    public static readonly IReadOnlySet<string> Sexos = new HashSet<string> { "Masculino", "Feminino" };
    public static readonly IReadOnlySet<string> Objetivos = new HashSet<string> { "perder peso", "manter peso", "ganhar peso" };

    // Níveis de atividade fora do treino (Usuario.NivelAtividade). Uma fonte
    // só: as chaves da tabela de fatores da fórmula.
    public static readonly IReadOnlySet<string> NiveisAtividade =
        new HashSet<string>(CalculadoraManutencao.FatoresPorNivel.Keys);

    // Decisão de 2026-10-17: o Ronu é para adultos. A fórmula de Mifflin-St
    // Jeor foi validada em adultos, e dieta para menor de idade pede
    // acompanhamento profissional.
    public const int IdadeMinima = 18;

    // A mesma faixa que o onboarding e o perfil já aplicavam no campo de data.
    public const int IdadeMaxima = 100;

    public const string MensagemSexo = "Sexo inválido.";
    public const string MensagemObjetivo = "Objetivo inválido.";
    public const string MensagemNivelAtividade = "Nível de atividade inválido.";
    public const string MensagemAltura = "Informe uma altura entre 1,00 e 2,50 m.";
    public const string MensagemPeso = "Informe um peso entre 20 e 300 kg.";
    public const string MensagemNascimentoFuturo = "A data de nascimento não pode ser no futuro.";
    public const string MensagemIdadeMinima = "O Ronu é para maiores de 18 anos.";
    public const string MensagemIdadeMaxima = "Informe uma data de nascimento de até 100 anos atrás.";

    /// <summary>
    /// Nulo se a data vale; senão, a mensagem do 400. "Hoje" é o dia UTC, a
    /// mesma regra do cálculo da idade na dieta (ContextoDietaBuilder.Idade):
    /// quem faz 18 anos hoje já pode.
    /// </summary>
    public static string? ValidarNascimento(DateOnly dataNascimento, DateOnly hoje)
    {
        if (dataNascimento > hoje)
        {
            return MensagemNascimentoFuturo;
        }

        // Pela mesma conta da idade da dieta, para os dois nunca discordarem
        // (no 29 de fevereiro, a comparação de datas e a idade divergiriam).
        if (ContextoDietaBuilder.Idade(dataNascimento, hoje) < IdadeMinima)
        {
            return MensagemIdadeMinima;
        }

        if (dataNascimento < hoje.AddYears(-IdadeMaxima))
        {
            return MensagemIdadeMaxima;
        }

        return null;
    }
}
