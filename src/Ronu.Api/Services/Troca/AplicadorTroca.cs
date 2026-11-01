using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.Troca;

/// <summary>
/// Grava na dieta (o DTO que vai para DietaIA.ConteudoJson) uma troca, um desfazer ou uma volta ao original
/// (trocar alimento, etapa 2). Só mexe nos itens trocados e soma de novo a refeição e o dia deles: os outros
/// alimentos não são reajustados, e a meta do dia não muda.
///
/// A troca é recalculada aqui pela CalculadoraTroca, com a lista fechada atual da pessoa: o cliente só diz qual
/// substituto quer, e um que não passa nas regras (porção, 5%, evitar) é recusado, mesmo que a tela o tenha
/// mostrado antes de uma mudança nas preferências.
/// </summary>
public static class AplicadorTroca
{
    private static readonly IReadOnlyDictionary<string, AlimentoBase> PorId = BaseNutricional.Itens.ToDictionary(i => i.Id);

    /// <summary>
    /// Troca o item da posição (ou, na semana, todas as vezes em que o mesmo item aparece) pelo substituto.
    /// Devolve nulo, sem mexer na dieta, quando o substituto não está entre as opções disponíveis do escopo.
    /// </summary>
    /// <param name="novaRevisao">A revisão que a dieta terá depois desta gravação (para o "Desfazer").</param>
    public static OpcaoTroca? Trocar(
        DietaSemanalDto dieta, PosicaoItem posicao, string alimentoIdNovo, EscopoTroca escopo, ListaAlimentos lista, int novaRevisao)
    {
        var opcoes = CalculadoraTroca.Calcular(dieta.Dias, posicao, lista);
        var doEscopo = escopo == EscopoTroca.Semana ? opcoes.Semana : opcoes.Refeicao;
        var opcao = doEscopo.DasPreferencias.Concat(doEscopo.Parecidos).Concat(doEscopo.VerMais)
            .FirstOrDefault(o => o.AlimentoId == alimentoIdNovo);
        if (opcao is null)
        {
            return null;
        }

        var substituto = PorId[alimentoIdNovo];
        var anteriores = new List<ItemAnteriorDto>();
        foreach (var o in opcao.Ocorrencias)
        {
            var atual = CalculadoraTroca.Encontrar(dieta.Dias, o.Posicao)!;
            anteriores.Add(Anterior(o.Posicao, atual));
            Substituir(dieta, o.Posicao, new AlimentoDto
            {
                Nome = o.Nome,
                Quantidade = o.Quantidade,
                Unidade = substituto.Medida,
                AlimentoId = substituto.Id,
                Macros = Macros(substituto, o.Quantidade),
                // O primeiro original fica: trocar de novo um item já trocado não o substitui.
                Original = atual.Original ?? Copia(atual)
            });
        }

        Somar(dieta, opcao.Ocorrencias.Select(o => o.Posicao));
        dieta.UltimaTroca = new UltimaTrocaDto { Revisao = novaRevisao, Itens = anteriores };
        return opcao;
    }

    /// <summary>
    /// Volta ao primeiro original o item da posição (ou, na semana, todos os que estão com o mesmo alimento e
    /// vieram do mesmo original). Devolve quantos itens voltaram; 0 quando o item da posição não foi trocado.
    /// </summary>
    public static int VoltarAoOriginal(DietaSemanalDto dieta, PosicaoItem posicao, EscopoTroca escopo, int novaRevisao)
    {
        var item = CalculadoraTroca.Encontrar(dieta.Dias, posicao);
        if (item?.Original is null)
        {
            return 0;
        }

        var posicoes = escopo == EscopoTroca.Refeicao
            ? [posicao]
            : Posicoes(dieta).Where(p =>
            {
                var outro = CalculadoraTroca.Encontrar(dieta.Dias, p)!;
                return outro.AlimentoId == item.AlimentoId && outro.Original?.AlimentoId == item.Original.AlimentoId;
            }).ToList();

        var anteriores = new List<ItemAnteriorDto>();
        foreach (var p in posicoes)
        {
            var atual = CalculadoraTroca.Encontrar(dieta.Dias, p)!;
            anteriores.Add(Anterior(p, atual));
            Substituir(dieta, p, Copia(atual.Original!));
        }

        Somar(dieta, posicoes);
        dieta.UltimaTroca = new UltimaTrocaDto { Revisao = novaRevisao, Itens = anteriores };
        return posicoes.Count;
    }

    /// <summary>
    /// Desfaz a última gravação (troca ou volta ao original), se ela ainda é a última: a revisão da dieta precisa
    /// ser a que ela gravou. Devolve false, sem mexer na dieta, quando não há o que desfazer.
    /// </summary>
    public static bool Desfazer(DietaSemanalDto dieta, int revisaoAtual)
    {
        var ultima = dieta.UltimaTroca;
        if (ultima is null || ultima.Revisao != revisaoAtual)
        {
            return false;
        }

        var posicoes = ultima.Itens.Select(i => new PosicaoItem(i.Dia, i.Refeicao, i.Item)).ToList();
        if (posicoes.Any(p => CalculadoraTroca.Encontrar(dieta.Dias, p) is null))
        {
            return false;
        }

        foreach (var i in ultima.Itens)
        {
            Substituir(dieta, new PosicaoItem(i.Dia, i.Refeicao, i.Item), Copia(i.Anterior, comOriginal: true));
        }

        Somar(dieta, posicoes);
        dieta.UltimaTroca = null;
        return true;
    }

    private static ItemAnteriorDto Anterior(PosicaoItem p, AlimentoDto atual) =>
        new() { Dia = p.Dia, Refeicao = p.Refeicao, Item = p.Item, Anterior = Copia(atual, comOriginal: true) };

    private static void Substituir(DietaSemanalDto dieta, PosicaoItem p, AlimentoDto novo) =>
        dieta.Dias[p.Dia].Refeicoes[p.Refeicao].Alimentos[p.Item] = novo;

    // Refeição e dia afetados somados de novo a partir dos itens, como no CalculoPelaBase.
    private static void Somar(DietaSemanalDto dieta, IEnumerable<PosicaoItem> posicoes)
    {
        foreach (var dia in posicoes.Select(p => p.Dia).Distinct())
        {
            foreach (var refeicao in posicoes.Where(p => p.Dia == dia).Select(p => p.Refeicao).Distinct())
            {
                var r = dieta.Dias[dia].Refeicoes[refeicao];
                r.Macros = CalculoPelaBase.Somar(r.Alimentos.Select(a => a.Macros!));
            }

            dieta.Dias[dia].TotalDoDia = CalculoPelaBase.Somar(dieta.Dias[dia].Refeicoes.Select(r => r.Macros));
        }
    }

    private static MacrosDto Macros(AlimentoBase item, decimal quantidade)
    {
        var fator = quantidade / 100m;
        return new MacrosDto
        {
            Calorias = item.Kcal * fator,
            ProteinasG = item.ProteinaG * fator,
            CarboidratosG = item.CarboidratoG * fator,
            GordurasG = item.GorduraG * fator
        };
    }

    private static AlimentoDto Copia(AlimentoDto a, bool comOriginal = false) => new()
    {
        Nome = a.Nome,
        Quantidade = a.Quantidade,
        Unidade = a.Unidade,
        AlimentoId = a.AlimentoId,
        Macros = a.Macros is null ? null : new MacrosDto
        {
            Calorias = a.Macros.Calorias, ProteinasG = a.Macros.ProteinasG,
            CarboidratosG = a.Macros.CarboidratosG, GordurasG = a.Macros.GordurasG
        },
        Original = comOriginal && a.Original is not null ? Copia(a.Original) : null
    };

    private static IEnumerable<PosicaoItem> Posicoes(DietaSemanalDto dieta) =>
        dieta.Dias.SelectMany((dia, d) => dia.Refeicoes.SelectMany((refeicao, r) =>
            refeicao.Alimentos.Select((_, i) => new PosicaoItem(d, r, i))));
}

public enum EscopoTroca
{
    Refeicao,
    Semana
}
