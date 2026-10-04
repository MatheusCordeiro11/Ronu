using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Meta calórica da fórmula, por dia da semana, antes da meta adaptativa:
/// manutenção (TMB × atividade fora do treino + gasto líquido do treino
/// daquele dia) com o ajuste do objetivo.
/// Pura, sem banco nem IA — o GeradorDietaGemini só casa cada dia com o nome.
/// </summary>
public static class CalculadoraManutencao
{
    /// <summary>
    /// Versão da fórmula de manutenção, gravada em cada MetaDieta. A meta
    /// adaptativa só compara a manutenção de dietas da MESMA versão — de outra
    /// versão, ela mediria a mudança da fórmula, não a pessoa. Toda mudança
    /// nesta classe, na CalculadoraGastoCalorico, no RitmoObjetivo ou nos METs
    /// das modalidades exige incrementar (regra no CLAUDE.md).
    /// 1 = até 2026-10-03 (TMB + MET bruto, ±15%); 2 = revisão de 2026-10-03.
    /// </summary>
    public const int VersaoFormula = 2;

    /// <summary>
    /// Mifflin-St Jeor: fórmula de TMB mais precisa e validada
    /// cientificamente. Sexo é "Masculino" ou "Feminino" (PerfilRequest).
    /// </summary>
    public static decimal Tmb(string sexo, decimal pesoKg, decimal alturaCm, int idade) =>
        sexo == "Masculino"
            ? (10 * pesoKg) + (6.25m * alturaCm) - (5 * idade) + 5
            : (10 * pesoKg) + (6.25m * alturaCm) - (5 * idade) - 161;

    /// <summary>
    /// Atividade fora do treino (trabalho, deslocamento, tarefas do dia a dia
    /// e o efeito térmico dos alimentos), como múltiplo da TMB. A TMB sozinha
    /// é o gasto em repouso absoluto: sem este fator, "manter peso" num dia de
    /// descanso era a própria TMB.
    /// 1,4 = limite inferior da faixa "sedentário ou atividade leve" (PAL
    /// 1,40–1,69) da FAO/WHO/UNU, Human Energy Requirements (2001, publ.
    /// 2004). O limite inferior porque o PAL da FAO inclui o exercício, que
    /// aqui entra à parte (gasto líquido do treino, CalculadoraGastoCalorico).
    /// O mesmo valor para todos por enquanto: a pergunta do nível de
    /// atividade no perfil está em docs/lacunas-conhecidas.md.
    /// </summary>
    public const decimal FatorAtividadeForaDoTreino = 1.4m;

    /// <summary>
    /// Gasto do dia sem o objetivo: TMB × fator de atividade fora do treino +
    /// gasto líquido (acima do repouso) do treino daquele dia. A primeira
    /// parcela é a mesma todos os dias — só o treino varia.
    /// </summary>
    public static decimal Manutencao(decimal tmb, decimal gastoTreinoDia) =>
        tmb * FatorAtividadeForaDoTreino + gastoTreinoDia;

    /// <summary>
    /// Teto do déficit: no máximo 25% da manutenção do dia. É uma rede de
    /// segurança, NÃO um valor da literatura — o ritmo de 0,5%/semana já é
    /// conservador; o teto só impede que um dia isolado vire restrição
    /// agressiva quando a manutenção é baixa e o peso alto (ex.: mulher de
    /// 130 kg sem treino). 20–25% abaixo do gasto é a faixa que os textos de
    /// referência chamam de déficit "moderado". Ganhar peso não tem teto:
    /// +2,75 kcal/kg fica sempre abaixo de ~10% da manutenção.
    /// </summary>
    public const decimal DeficitMaximoFracaoManutencao = 0.25m;

    /// <summary>
    /// Déficit/superávit do objetivo em kcal fixas por dia, derivado do mesmo
    /// ritmo esperado da meta adaptativa (RitmoObjetivo) — o mesmo valor em
    /// todos os dias da semana, limitado pelo teto do déficit. Objetivo vem de
    /// um conjunto fixo de valores definidos no frontend (radio buttons), não
    /// texto livre — match direto seguro.
    /// </summary>
    public static decimal AplicarObjetivo(decimal manutencao, string objetivo, decimal pesoKg)
    {
        var ajuste = RitmoObjetivo.KcalPorDia(objetivo, pesoKg);
        var deficitMaximo = -DeficitMaximoFracaoManutencao * manutencao;

        return manutencao + Math.Max(ajuste, deficitMaximo);
    }

    /// <summary>
    /// Manutenção (sem o objetivo) de cada dia da semana, índice 0 = segunda
    /// ... 6 = domingo — o formato guardado em MetaDieta.ManutencoesPorDia.
    /// </summary>
    public static decimal[] ManutencaoPorDia(ContextoDietaDto contexto, ICalculadoraGastoCalorico calculadora)
    {
        var tmb = Tmb(contexto.Sexo, contexto.Peso, contexto.Altura, contexto.Idade);

        // UsuarioModalidade.DiasSemana usa 1 = segunda ... 7 = domingo (ISO 8601).
        return Enumerable.Range(1, 7).Select(dia =>
        {
            var gastoTreinoDia = contexto.Modalidades
                .Where(m => m.DiasSemana.Contains(dia))
                .Sum(m => calculadora.CalcularGastoSessao(m.MetReferencia, contexto.Peso, m.DuracaoHoras));

            return Manutencao(tmb, gastoTreinoDia);
        }).ToArray();
    }

    /// <summary>
    /// Calorias da fórmula de cada dia da semana (1 = segunda ... 7 = domingo,
    /// ISO 8601, como em UsuarioModalidade.DiasSemana).
    /// </summary>
    public static IReadOnlyDictionary<int, decimal> CaloriasBasePorDia(
        ContextoDietaDto contexto, ICalculadoraGastoCalorico calculadora)
    {
        var manutencao = ManutencaoPorDia(contexto, calculadora);

        return Enumerable.Range(1, 7).ToDictionary(
            dia => dia,
            dia => AplicarObjetivo(manutencao[dia - 1], contexto.Objetivo, contexto.Peso));
    }
}
