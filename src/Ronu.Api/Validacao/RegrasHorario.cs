using System.Globalization;

namespace Ronu.Api.Validacao;

/// <summary>
/// Horários estruturados (acordar, dormir, início do treino) no formato "HH:mm", aplicados no PUT /api/perfil e no
/// POST /api/usuarios/modalidades. Eles substituem a rotina em texto livre no prompt do Gemini: horário não é dado de
/// saúde, e texto livre pode trazer qualquer coisa.
///
/// Ausente ou nulo mantém o valor salvo, como o NivelAtividade: as telas que já existem reenviam o perfil e a
/// modalidade sem estes campos e não podem apagá-los sem querer. Texto vazio apaga.
/// </summary>
public static class RegrasHorario
{
    public const string MensagemFormato = "Horário inválido. Use o formato HH:mm, de 00:00 a 23:59.";

    public enum Acao { Manter, Apagar, Gravar }

    // Exatamente "HH:mm": "7:30" ou "07:30:00" são recusados, para a tela e o banco nunca discordarem do formato.
    public static (Acao Acao, TimeOnly? Valor, string? Erro) Interpretar(string? texto)
    {
        if (texto is null)
        {
            return (Acao.Manter, null, null);
        }

        if (texto.Length == 0)
        {
            return (Acao.Apagar, null, null);
        }

        return TimeOnly.TryParseExact(texto, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valor)
            ? (Acao.Gravar, valor, null)
            : (Acao.Manter, null, MensagemFormato);
    }

    public static TimeOnly? Aplicar(TimeOnly? atual, (Acao Acao, TimeOnly? Valor, string? Erro) interpretado) =>
        interpretado.Acao switch
        {
            Acao.Apagar => null,
            Acao.Gravar => interpretado.Valor,
            _ => atual
        };

    public static string? Formatar(TimeOnly? horario) =>
        horario?.ToString("HH:mm", CultureInfo.InvariantCulture);
}
