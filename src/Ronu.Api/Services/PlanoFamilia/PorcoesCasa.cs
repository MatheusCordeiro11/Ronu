using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// A porção de cada pessoa nas refeições da casa (plano família, etapa 4, decisão D5): a mesma panela, quantidades
/// diferentes. Sem IA: a semana da casa vem numa porção de referência, e cada pessoa recebe esse prato escalado para
/// a fatia da meta dela e depois ajustado nos macros.
///
/// Em dois passos, porque o AjusteQuantidades sozinho move cada item só entre 50% e 150%: no exemplo do passo 0 (3
/// perfis fictícios), um perfil que precisava de 2 vezes a referência ficava 37,7% abaixo da meta do almoço.
/// (a) escala o prato inteiro para as kcal do alvo, na mesma proporção entre os itens, até a porção máxima da base;
/// (b) roda o AjusteQuantidades só nessa refeição, contra o alvo de kcal e de cada macro.
/// </summary>
public static class PorcoesCasa
{
    // Calibração do projeto, não valor de tabela: a fatia média do almoço e do jantar na meta do dia, medida no g9c
    // (49 dias de 7 perfis fictícios, dietas individuais geradas pelo Gemini). O resto da meta fica para o café da
    // manhã e os lanches de cada um.
    public const decimal FatiaAlmoco = 0.307m;
    public const decimal FatiaJantar = 0.232m;

    // Calibração do projeto (decisão D5 do passo 0), não valor de tabela: a porção de referência que vai ao Gemini.
    // O número só dá ao Gemini a escala de um prato comum; a porção de cada um sai depois, em C#. Nenhuma meta de
    // membro vai ao Gemini: só estes dois números fixos.
    public const decimal ReferenciaAlmocoKcal = 650m;
    public const decimal ReferenciaJantarKcal = 550m;

    public const string NomeAlmoco = "Almoço";
    public const string NomeJantar = "Jantar";

    private static readonly string[] PrefixosDias = ["seg", "ter", "qua", "qui", "sex", "sab", "dom"];

    private static readonly IReadOnlyDictionary<string, AlimentoBase> PorId = BaseNutricional.Itens.ToDictionary(i => i.Id);

    /// <summary>"seg-almoco", "dom-jantar": o vínculo entre a refeição da dieta de cada membro e a da semana da casa.</summary>
    public static string Chave(int indiceDia, bool almoco) => $"{PrefixosDias[indiceDia]}-{(almoco ? "almoco" : "jantar")}";

    public static bool EAlmoco(string chaveCasa) => chaveCasa.EndsWith("-almoco", StringComparison.Ordinal);

    public static decimal Fatia(string chaveCasa) => EAlmoco(chaveCasa) ? FatiaAlmoco : FatiaJantar;

    /// <summary>O alvo da refeição da casa para uma pessoa: a fatia da meta do dia dela, em kcal e em cada macro.</summary>
    public static MacrosDto Alvo(MacrosDto metaDoDia, string chaveCasa) => Multiplicar(metaDoDia, Fatia(chaveCasa));

    /// <summary>
    /// Se a refeição da casa serve para a pessoa: todo item está na lista dela (sem o "não como" e as alergias dela).
    /// Fora da casa recém-gerada, isso só falha para quem marcou uma restrição depois da semana gerada (ou entrou no
    /// meio da semana, etapa 5): aí a refeição vira individual só para ela.
    /// </summary>
    public static bool Serve(RefeicaoDto referencia, ListaAlimentos listaDaPessoa)
    {
        var naLista = listaDaPessoa.Itens.Select(i => i.Item.Id).ToHashSet();
        return referencia.Alimentos.All(a => a.AlimentoId is not null && naLista.Contains(a.AlimentoId));
    }

    /// <summary>As chaves das refeições da casa que não servem para a pessoa ("ter-jantar").</summary>
    public static List<string> RefeicoesQueNaoServem(SemanaCasaDto semana, ListaAlimentos listaDaPessoa) => semana.Dias
        .SelectMany(d => d.Refeicoes)
        .Where(r => !Serve(r, listaDaPessoa))
        .Select(r => r.ChaveCasa!)
        .ToList();

    /// <summary>
    /// A porção da pessoa para uma refeição da casa: uma cópia da referência, escalada e ajustada para o alvo, com a
    /// contagem no nome dos contáveis. A referência não muda.
    /// </summary>
    public static RefeicaoDto Porcao(RefeicaoDto referencia, MacrosDto metaDoDia)
    {
        var refeicao = new RefeicaoDto
        {
            Nome = referencia.Nome,
            Horario = referencia.Horario,
            DaCasa = true,
            ChaveCasa = referencia.ChaveCasa,
            Macros = Zero(),
            Alimentos = referencia.Alimentos.Select(a => new AlimentoDto
            {
                Nome = a.Nome, Quantidade = a.Quantidade, Unidade = a.Unidade, AlimentoId = a.AlimentoId
            }).ToList()
        };

        Ajustar([refeicao], Alvo(metaDoDia, referencia.ChaveCasa!));
        Rotular(refeicao);
        return refeicao;
    }

    /// <summary>
    /// Os dois passos (escala e AjusteQuantidades) sobre um conjunto de refeições contra um alvo, já com as kcal e os
    /// macros calculados pela base. Serve à porção da casa e ao café e lanches mantidos quando chega uma semana nova
    /// (decisão D6). Devolve o resultado do ajuste, para o log e os testes; nulo sem nenhum item.
    /// </summary>
    public static AjusteDoDia? Ajustar(List<RefeicaoDto> refeicoes, MacrosDto alvo)
    {
        var alimentos = refeicoes.SelectMany(r => r.Alimentos).ToList();
        if (alimentos.Count == 0)
        {
            return null;
        }

        var kcal = alimentos.Sum(a => PorId[a.AlimentoId!].Kcal * a.Quantidade / 100m);
        if (kcal > 0 && alvo.Calorias > 0)
        {
            var fator = alvo.Calorias / kcal;
            foreach (var a in alimentos)
            {
                // Pelo menos 1 g: nenhum item some do prato da casa, mesmo na porção pequena.
                a.Quantidade = Math.Clamp(Math.Round(a.Quantidade * fator), 1m, Math.Max(1m, PorId[a.AlimentoId!].PorcaoMaxima));
            }
        }

        var dia = new DiaDietaDto { DiaSemana = string.Empty, Refeicoes = refeicoes, TotalDoDia = Zero(), MetaCalculada = SemZeros(alvo) };
        var ajuste = AjusteQuantidades.Ajustar([dia])[0];
        CalculoPelaBase.Calcular([dia]);
        return ajuste;
    }

    /// <summary>
    /// As refeições da casa de uma pessoa na semana: a porção de cada refeição que serve para ela, as que viram
    /// individuais (por dia, o nome da refeição que ela monta sozinha) e a meta que sobra em cada dia para as refeições
    /// individuais. Os dias são casados pelo nome, como na geração individual.
    /// </summary>
    public static CasaDaPessoa Montar(SemanaCasaDto semana, IReadOnlyDictionary<string, ResultadoMacros> metasPorDia, ListaAlimentos listaDaPessoa)
    {
        var porDia = new Dictionary<string, List<RefeicaoDto>>(StringComparer.OrdinalIgnoreCase);
        var restantes = new Dictionary<string, ResultadoMacros>(StringComparer.OrdinalIgnoreCase);
        var individuais = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var dia in semana.Dias)
        {
            var nome = dia.DiaSemana.Trim();
            var meta = metasPorDia[nome];
            var daCasa = new List<RefeicaoDto>();
            var noLugar = new List<string>();
            foreach (var referencia in dia.Refeicoes)
            {
                if (Serve(referencia, listaDaPessoa))
                {
                    daCasa.Add(Porcao(referencia, meta.Macros));
                }
                else
                {
                    noLugar.Add(referencia.Nome);
                }
            }

            porDia[nome] = daCasa;
            individuais[nome] = noLugar;
            restantes[nome] = new ResultadoMacros(SemZeros(Subtrair(meta.Macros, CalculoPelaBase.Somar(daCasa.Select(r => r.Macros)))), meta.MetaElevadaPeloPiso);
        }

        return new CasaDaPessoa(porDia, restantes, individuais);
    }

    /// <summary>
    /// O dia completo: as refeições individuais e as da casa, na ordem do horário (as sem horário no fim), com a meta
    /// cheia do dia e os totais pela base. As individuais saem marcadas como não sendo da casa.
    /// </summary>
    public static void Juntar(DiaDietaDto dia, IEnumerable<RefeicaoDto> daCasa, ResultadoMacros metaDoDia)
    {
        foreach (var r in dia.Refeicoes)
        {
            r.DaCasa = false;
            r.ChaveCasa = null;
        }

        dia.Refeicoes = dia.Refeicoes.Concat(daCasa)
            .Select((r, i) => (r, i))
            .OrderBy(x => string.IsNullOrWhiteSpace(x.r.Horario) ? "99:99" : x.r.Horario.Trim())
            .ThenBy(x => x.i)
            .Select(x => x.r)
            .ToList();
        dia.MetaCalculada = metaDoDia.Macros;
        dia.MetaElevadaPeloPiso = metaDoDia.MetaElevadaPeloPiso;
        CalculoPelaBase.Calcular([dia]);
    }

    /// <summary>"Almoço" e "Jantar" pelo nome livre, como o resto do código reconhece as refeições ("almo", "jant").</summary>
    public static bool EAlmocoOuJantar(RefeicaoDto refeicao) =>
        refeicao.Nome.Contains("almo", StringComparison.OrdinalIgnoreCase) || refeicao.Nome.Contains("jant", StringComparison.OrdinalIgnoreCase);

    /// <summary>O nome do item sem a contagem ("Ovo de galinha cozido (2 unidades)" vira "Ovo de galinha cozido").</summary>
    public static string NomeSemContagem(AlimentoDto alimento)
    {
        if (alimento.AlimentoId is null || !PorId.TryGetValue(alimento.AlimentoId, out var item) || item.MedidaUnidade is null)
        {
            return alimento.Nome;
        }

        var corte = alimento.Nome.LastIndexOf(" (", StringComparison.Ordinal);
        return corte > 0 && alimento.Nome.EndsWith(')') ? alimento.Nome[..corte] : alimento.Nome;
    }

    private static void Rotular(RefeicaoDto refeicao)
    {
        foreach (var a in refeicao.Alimentos)
        {
            a.Nome = RotuloContavel.Nome(NomeSemContagem(a), PorId[a.AlimentoId!], a.Quantidade);
        }
    }

    /// <summary>Rotula de novo os contáveis de refeições cujas quantidades mudaram (o café e os lanches mantidos).</summary>
    public static void RotularDeNovo(IEnumerable<RefeicaoDto> refeicoes)
    {
        foreach (var r in refeicoes)
        {
            Rotular(r);
        }
    }

    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    private static MacrosDto Multiplicar(MacrosDto m, decimal f) => new()
    {
        Calorias = m.Calorias * f, ProteinasG = m.ProteinasG * f, CarboidratosG = m.CarboidratosG * f, GordurasG = m.GordurasG * f
    };

    private static MacrosDto Subtrair(MacrosDto a, MacrosDto b) => new()
    {
        Calorias = a.Calorias - b.Calorias, ProteinasG = a.ProteinasG - b.ProteinasG,
        CarboidratosG = a.CarboidratosG - b.CarboidratosG, GordurasG = a.GordurasG - b.GordurasG
    };

    // O ajuste divide pela meta de cada macro: um macro que a casa já cobriu inteiro (meta restante 0 ou negativa)
    // fica em 1, o que só diz "não precisa de mais".
    private static MacrosDto SemZeros(MacrosDto m) => new()
    {
        Calorias = Math.Max(m.Calorias, 1m), ProteinasG = Math.Max(m.ProteinasG, 1m),
        CarboidratosG = Math.Max(m.CarboidratosG, 1m), GordurasG = Math.Max(m.GordurasG, 1m)
    };
}

/// <param name="RefeicoesPorDia">As porções da pessoa nas refeições da casa que servem para ela, por nome do dia.</param>
/// <param name="MetasRestantes">A meta de cada dia menos as porções da casa: o alvo das refeições individuais.</param>
/// <param name="IndividuaisNoLugar">Por dia, os nomes das refeições da casa que não servem e que a pessoa monta sozinha.</param>
public record CasaDaPessoa(
    IReadOnlyDictionary<string, List<RefeicaoDto>> RefeicoesPorDia,
    IReadOnlyDictionary<string, ResultadoMacros> MetasRestantes,
    IReadOnlyDictionary<string, List<string>> IndividuaisNoLugar);
