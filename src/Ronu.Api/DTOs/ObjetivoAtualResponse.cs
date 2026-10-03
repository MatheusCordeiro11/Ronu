namespace Ronu.Api.DTOs;

/// <summary>
/// Resposta do GET /objetivos/atual e do POST /objetivos: o objetivo (mesmos
/// campos de ObjetivoResponse, na raiz) mais dois sinais usados pelo
/// formulário de registrar peso (progresso.js). Só acrescenta campos, para
/// não quebrar quem já lê a resposta antiga.
/// </summary>
public class ObjetivoAtualResponse : ObjetivoResponse
{
    // Se o registro devolvido é o "de hoje" que o próximo POST vai
    // sobrescrever. Calculado só aqui, com a mesma regra de dia do upsert.
    public bool RegistradoHoje { get; set; }

    // Se o usuário já tem alguma dieta gerada. Sem dieta, a pergunta de
    // aderência fica escondida.
    public bool TemDieta { get; set; }
}
