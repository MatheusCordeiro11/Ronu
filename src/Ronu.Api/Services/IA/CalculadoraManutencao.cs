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
    /// 1 = até 2026-10-03 (TMB + MET bruto, ±15%); 2 = revisão de 2026-10-03;
    /// 3 = nível de atividade do dia a dia (fator por resposta, em vez de ×1,4 fixo).
    /// </summary>
    public const int VersaoFormula = 3;

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
    ///
    /// Os fatores representam SÓ a atividade fora do exercício: o treino entra
    /// à parte (gasto líquido da modalidade, CalculadoraGastoCalorico), então
    /// um fator que incluísse exercício contaria o treino duas vezes.
    ///
    /// Fontes e raciocínio (decisão de 2026-10-19):
    /// - FAO/WHO/UNU, Human Energy Requirements (2001, publ. 2004), Tabela 5.3:
    ///   sedentário ou leve = PAL 1,40–1,69; ativo ou moderadamente ativo =
    ///   1,70–1,99 (exemplos: pedreiro e operário de obra); vigoroso =
    ///   2,00–2,40. Os níveis da FAO descrevem o estilo de vida inteiro e
    ///   INCLUEM o exercício de lazer (o texto cita que 1 h de exercício
    ///   leva um sedentário de ~1,55 para ~1,75). Por isso usamos os pisos
    ///   das faixas, nunca o meio ou o topo.
    /// - IOM, Dietary Reference Intakes (2005): "sedentário" = PAL 1,0–1,39,
    ///   só atividades do dia a dia, sem exercício. É a âncora do nível mais
    ///   baixo, a única fonte sem exercício embutido.
    /// - A TMB do Ronu é a de Mifflin-St Jeor, um pouco acima da basal da FAO:
    ///   outro motivo para ficar nos pisos.
    ///
    /// Os valores:
    /// - sentado 1,30: entre o sedentário do IOM (até 1,39) e o piso da FAO (1,40).
    /// - em pé 1,45: meio da faixa leve da FAO (1,40–1,69).
    /// - andando 1,60: alto da faixa leve da FAO (até 1,69); equivale ao "pouco
    ///   ativo" do IOM (1,40–1,59, com 30–60 min de caminhada por dia).
    /// - braçal 1,75: piso da faixa "ativa" da FAO, a de pedreiro e operário de obra.
    /// O encaixe de "em pé" e "andando" na escala é JULGAMENTO nosso: as fontes
    /// não classificam essas ocupações com esses nomes. A meta adaptativa
    /// corrige o que sobrar.
    ///
    /// Sem resposta (nível nulo): 1,4, o valor único de antes desta pergunta
    /// (piso da faixa sedentário ou leve da FAO). A meta de ninguém muda sem a
    /// pessoa responder.
    /// </summary>
    public const decimal FatorPadrao = 1.4m;

    public static readonly IReadOnlyDictionary<string, decimal> FatoresPorNivel = new Dictionary<string, decimal>
    {
        ["sentado"] = 1.30m,
        ["em_pe"] = 1.45m,
        ["andando"] = 1.60m,
        ["bracal"] = 1.75m
    };

    /// <summary>Fator do nível; nível nulo (sem resposta) usa o padrão de antes.</summary>
    public static decimal FatorDoNivel(string? nivel) =>
        nivel is not null && FatoresPorNivel.TryGetValue(nivel, out var fator) ? fator : FatorPadrao;

    /// <summary>
    /// Gasto do dia sem o objetivo: TMB × fator de atividade fora do treino +
    /// gasto líquido (acima do repouso) do treino daquele dia. A primeira
    /// parcela é a mesma todos os dias — só o treino varia.
    /// </summary>
    public static decimal Manutencao(decimal tmb, decimal gastoTreinoDia, decimal fatorAtividade) =>
        tmb * fatorAtividade + gastoTreinoDia;

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
    public static decimal AplicarObjetivo(decimal manutencao, string objetivo, decimal pesoKg) =>
        manutencao + AjusteDoObjetivo(manutencao, objetivo, pesoKg).Kcal;

    /// <summary>
    /// A parcela do objetivo sozinha (negativa = déficit) e se o teto do
    /// déficit cortou o ritmo naquele dia.
    /// </summary>
    public static (decimal Kcal, bool TetoAplicado) AjusteDoObjetivo(decimal manutencao, string objetivo, decimal pesoKg)
    {
        var ajuste = RitmoObjetivo.KcalPorDia(objetivo, pesoKg);
        var deficitMaximo = -DeficitMaximoFracaoManutencao * manutencao;

        return (Math.Max(ajuste, deficitMaximo), deficitMaximo > ajuste);
    }

    /// <summary>
    /// As parcelas da meta da fórmula de cada dia da semana (índice 0 =
    /// segunda ... 6 = domingo). Fonte única das contas: a meta da dieta
    /// (CaloriasBasePorDia), a manutenção da MetaDieta (ManutencaoPorDia), a
    /// composição mostrada no dashboard e a simulação (POST /api/metas/simulacao)
    /// saem daqui, então os números nunca divergem entre elas. Valores exatos,
    /// sem arredondar (ArredondamentoPartes cuida do que vai para a tela).
    /// </summary>
    public static ComposicaoFormulaDia[] ComposicaoPorDia(ContextoDietaDto contexto, ICalculadoraGastoCalorico calculadora)
    {
        var tmb = Tmb(contexto.Sexo, contexto.Peso, contexto.Altura, contexto.Idade);
        var fator = FatorDoNivel(contexto.NivelAtividade);

        // UsuarioModalidade.DiasSemana usa 1 = segunda ... 7 = domingo (ISO 8601).
        return Enumerable.Range(1, 7).Select(dia =>
        {
            var treinos = contexto.Modalidades
                .Select((modalidade, indice) => (modalidade, indice))
                .Where(x => x.modalidade.DiasSemana.Contains(dia))
                .Select(x => new TreinoDoDia(
                    x.indice, x.modalidade.Nome, x.modalidade.DuracaoHoras,
                    calculadora.CalcularGastoSessao(x.modalidade.MetReferencia, contexto.Peso, x.modalidade.DuracaoHoras)))
                .ToList();

            var manutencao = Manutencao(tmb, treinos.Sum(t => t.Kcal), fator);
            var (objetivo, tetoAplicado) = AjusteDoObjetivo(manutencao, contexto.Objetivo, contexto.Peso);

            return new ComposicaoFormulaDia(
                dia, tmb, tmb * (fator - 1), treinos, manutencao, objetivo, tetoAplicado,
                contexto.NivelAtividade, fator);
        }).ToArray();
    }

    /// <summary>
    /// Manutenção (sem o objetivo) de cada dia da semana, índice 0 = segunda
    /// ... 6 = domingo — o formato guardado em MetaDieta.ManutencoesPorDia.
    /// </summary>
    public static decimal[] ManutencaoPorDia(ContextoDietaDto contexto, ICalculadoraGastoCalorico calculadora) =>
        ComposicaoPorDia(contexto, calculadora).Select(c => c.Manutencao).ToArray();

    /// <summary>
    /// Calorias da fórmula de cada dia da semana (1 = segunda ... 7 = domingo,
    /// ISO 8601, como em UsuarioModalidade.DiasSemana).
    /// </summary>
    public static IReadOnlyDictionary<int, decimal> CaloriasBasePorDia(
        ContextoDietaDto contexto, ICalculadoraGastoCalorico calculadora) =>
        ComposicaoPorDia(contexto, calculadora).ToDictionary(c => c.DiaSemana, c => c.MetaFormula);
}

/// <summary>
/// Uma sessão de treino de um dia: o índice da modalidade em
/// ContextoDietaDto.Modalidades (para quem precisa voltar ao Id) e o gasto
/// líquido da sessão.
/// </summary>
public sealed record TreinoDoDia(int IndiceModalidade, string Nome, decimal Horas, decimal Kcal);

/// <summary>
/// As parcelas da meta da fórmula de um dia (1 = segunda ... 7 = domingo):
/// gasto em repouso (TMB) + atividade fora do treino (TMB × (fator − 1)) + treinos
/// = manutenção; manutenção + objetivo = meta da fórmula, antes da meta
/// adaptativa e do piso de carboidrato.
/// </summary>
public sealed record ComposicaoFormulaDia(
    int DiaSemana,
    decimal GastoRepouso,
    decimal AtividadeForaDoTreino,
    IReadOnlyList<TreinoDoDia> Treinos,
    decimal Manutencao,
    decimal Objetivo,
    bool TetoDeficitAplicado,
    string? NivelAtividade,
    decimal FatorAtividade)
{
    public decimal MetaFormula => Manutencao + Objetivo;
}
