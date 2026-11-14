using System.Globalization;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Teto da castanha-do-pará no cardápio gerado, por causa do selênio. Roda no C#, depois das validações da resposta e
/// antes do ajuste de quantidades (que não mexe nela: não é ajustável), e o ajuste compensa as kcal e a gordura com os
/// outros itens. O prompt não fala disso: decisão de 2026-11-13, para não mudar a geração sem uma rodada de medição.
///
/// Por quê (valores com fonte):
/// - A castanha-do-pará tem 1.917 µg de selênio em 100 g (USDA FoodData Central, SR Legacy, fdcId 170569, NDB 12078;
///   15 amostras, de 136 a 2.740 µg). A TACO, fonte dos macros dela na base, não mede selênio.
/// - O selênio varia muito com o solo: a mediana por estado vai de 2,07 mg/kg (MT) a 68,15 mg/kg (AM), ou seja,
///   de 207 a 6.815 µg em 100 g (Silva Junior et al., Chemosphere 188:650–658, 2017, doi:10.1016/j.chemosphere.2017.08.158).
/// - Limite superior tolerável (UL), de todas as fontes: 400 µg/dia a partir dos 14 anos (IOM, DRI de 2000) e
///   255 µg/dia para adultos (EFSA, 2023, doi:10.2903/j.efsa.2023.7704, que cita quem come castanha-do-pará com
///   regularidade entre os que podem passar dele).
/// - Nas respostas reais medidas (docs/medicoes, g8b a g10d), ela apareceu em 48% das dietas, com 10 a 30 g por dia
///   (mediana 15 g, ~288 µg pela USDA) e em até 3 dias da semana.
///
/// Calibração do projeto (não é valor de tabela): até 8 g por dia, ou seja, 2 unidades de 4 g (POF/IBGE 6600701),
/// ~153 µg pela USDA; e em até 2 dias da semana. Nenhum teto em gramas é seguro para castanha de qualquer origem
/// (8 g do Amazonas dão ~545 µg), e a base não sabe a origem: o teto reduz a exposição, não a zera.
/// </summary>
public static class LimiteCastanhaDoPara
{
    public const string Id = "castanha-do-para";

    /// <summary>Gramas por dia (2 unidades de 4 g da POF). Calibração do projeto.</summary>
    public const decimal GramasPorDia = 8m;

    /// <summary>Dias da semana com castanha-do-pará. Calibração do projeto.</summary>
    public const int DiasPorSemana = 2;

    /// <summary>
    /// Corta a castanha-do-pará de cada dia para no máximo <see cref="GramasPorDia"/> (mantém a ordem das refeições:
    /// a primeira ocorrência fica, o que passa do teto sai) e tira das que passam de <see cref="DiasPorSemana"/> dias,
    /// na ordem da semana (ficam os primeiros dias em que ela aparece). Uma refeição que fica vazia sai do dia, como na
    /// correção da cafeína e da cerveja. Devolve o que mudou, para o log; as kcal saem do cálculo pela base, depois.
    /// </summary>
    public static List<string> Aplicar(List<DiaDietaDto> dias)
    {
        var mudancas = new List<string>();
        var diasComCastanha = 0;

        foreach (var dia in dias)
        {
            var ocorrencias = dia.Refeicoes.SelectMany(r => r.Alimentos.Where(a => a.AlimentoId == Id).Select(a => (Refeicao: r, Alimento: a))).ToList();
            if (ocorrencias.Count == 0)
            {
                continue;
            }

            diasComCastanha++;
            var antes = ocorrencias.Sum(o => o.Alimento.Quantidade);
            var restante = diasComCastanha <= DiasPorSemana ? GramasPorDia : 0m;
            var refeicoesVazias = new List<string>();

            foreach (var (refeicao, alimento) in ocorrencias)
            {
                var fica = Math.Min(alimento.Quantidade, restante);
                restante -= fica;
                if (fica == 0)
                {
                    refeicao.Alimentos.Remove(alimento);
                    if (refeicao.Alimentos.Count == 0)
                    {
                        refeicoesVazias.Add(refeicao.Nome);
                    }
                }
                else
                {
                    alimento.Quantidade = fica;
                }
            }

            dia.Refeicoes.RemoveAll(r => r.Alimentos.Count == 0);

            var depois = Math.Min(antes, diasComCastanha <= DiasPorSemana ? GramasPorDia : 0m);
            if (depois != antes)
            {
                var motivo = diasComCastanha <= DiasPorSemana ? "teto do dia" : $"passou de {DiasPorSemana} dias na semana";
                var vazias = refeicoesVazias.Count == 0 ? "" : $"; refeição sem outro item saiu: {string.Join(", ", refeicoesVazias)}";
                mudancas.Add(string.Create(CultureInfo.InvariantCulture, $"{dia.DiaSemana}: {antes:0.#} g -> {depois:0.#} g ({motivo}{vazias})"));
            }
        }

        return mudancas;
    }
}
