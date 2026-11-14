using System.Diagnostics;
using System.Globalization;
using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.PlanoFamilia;

namespace Ronu.Api.Services.IA;

/// <summary>As metas de uma pessoa na semana, como a geração individual as calcula, e o que a MetaDieta grava delas.</summary>
public record MetasDaSemana(
    Dictionary<string, ResultadoMacros> MetasPorDia,
    Dictionary<string, ComposicaoMetaDto> ComposicaoPorDia,
    AjusteAdaptativoDto Ajuste,
    decimal[] ManutencaoPorDia,
    decimal FatorAtividade);

/// <summary>
/// Plano família, etapa 4: a semana da casa e as refeições individuais de quem está numa casa. Num arquivo à parte
/// do GeradorDietaGemini para a geração individual continuar lida de uma vez; usa a mesma chamada, a mesma lista
/// fechada e as mesmas conferências.
///
/// O que vai ao Gemini na semana da casa: a lista da casa (já sem o "não como" e as alergias de todos), o peso de uma
/// unidade dos contáveis, os itens leves pelo nome da lista, as regras do cardápio, o orçamento da casa e a porção de
/// referência fixa (PorcoesCasa). Nada de nenhum membro: nem meta, peso, idade, sexo, objetivo, treino, UF, nome, nem
/// o texto que alguém escreveu nas preferências (um teste inspeciona o prompt).
/// </summary>
public partial class GeradorDietaGemini : IGeradorSemanaCasa
{
    /// <summary>
    /// No máximo 2 misturas por refeição da casa (decisão do plano família). Mistura = item dos grupos de carnes, aves,
    /// peixes e ovos; o feijão e as outras leguminosas não contam (decisão D1: é a mistura do dia a dia, não o prato
    /// principal).
    /// </summary>
    public const int MaximoMisturasPorRefeicao = 2;

    // Na casa, o café da manhã e dois lanches no mínimo: almoço e jantar já vêm da casa. Calibração do projeto (as 5
    // refeições da regra 15 menos as 2 da casa).
    public const int RefeicoesMinimasCasa = 3;

    public static bool EMistura(AlimentoBase item) => item.Grupo is GrupoDeTroca.CarnesEPeixes or GrupoDeTroca.Ovos;

    private static readonly IReadOnlyDictionary<string, AlimentoBase> PorIdDaBase = BaseNutricional.Itens.ToDictionary(i => i.Id);

    public MetasDaSemana CalcularMetas(ContextoDietaDto contexto)
    {
        var caloriasBasePorDia = CalcularCaloriasBasePorDia(contexto, _calculadora);
        var manutencaoPorDia = CalculadoraManutencao.ManutencaoPorDia(contexto, _calculadora);
        var metaBaseMedia = caloriasBasePorDia.Values.Average();
        var fatorAtividade = CalculadoraManutencao.FatorDoNivel(contexto.NivelAtividade);
        var ajuste = _calculadoraAjuste.Calcular(
            contexto.HistoricoPeso, contexto.HistoricoMetas, contexto.Objetivo, metaBaseMedia, manutencaoPorDia,
            DateTime.UtcNow, fatorAtividade);
        RegistrarAjusteAdaptativo(ajuste, metaBaseMedia);
        var metasPorDia = MontarMetasPorDia(caloriasBasePorDia, ajuste.Percentual, contexto.Peso, contexto.Altura);
        var composicaoPorDia = MontarComposicaoPorDia(contexto, _calculadora, ajuste.Percentual, metasPorDia);
        return new MetasDaSemana(metasPorDia, composicaoPorDia, ajuste, manutencaoPorDia, fatorAtividade);
    }

    // ---------- Refeições individuais de quem está numa casa ----------

    // "café da manhã e lanches", ou "café da manhã, lanches e almoço" quando o almoço da casa não serve para a pessoa.
    private static string RefeicoesAMontar(CasaDaPessoa casa, string nomeDia)
    {
        var noLugar = casa.IndividuaisNoLugar.TryGetValue(nomeDia, out var nomes) ? nomes : [];
        return noLugar.Count == 0
            ? "café da manhã e lanches"
            : $"café da manhã, lanches e {string.Join(" e ", noLugar.Select(n => n.ToLowerInvariant()))}";
    }

    /// <summary>
    /// Um almoço ou jantar na resposta só pode estar no dia em que a refeição da casa não serve para a pessoa: nos
    /// outros, a refeição da casa entra depois, e o dia ficaria com dois almoços. Na primeira tentativa, a resposta é
    /// inválida; na última, a refeição sobrando sai.
    /// </summary>
    private void ConferirRefeicoesDaCasa(List<DiaDietaDto> dias, CasaDaPessoa casa, bool ultimaTentativa)
    {
        var sobrando = new List<string>();
        foreach (var dia in dias)
        {
            var noLugar = casa.IndividuaisNoLugar.TryGetValue(dia.DiaSemana.Trim(), out var nomes) ? nomes : [];
            bool Permitida(RefeicaoDto r) =>
                !PorcoesCasa.EAlmocoOuJantar(r)
                || noLugar.Any(n => r.Nome.Contains(n[..4], StringComparison.OrdinalIgnoreCase));

            foreach (var refeicao in dia.Refeicoes.Where(r => !Permitida(r)).ToList())
            {
                sobrando.Add($"{dia.DiaSemana.Trim()} {refeicao.Nome}");
                if (ultimaTentativa)
                {
                    dia.Refeicoes.Remove(refeicao);
                }
            }
        }

        if (sobrando.Count == 0)
        {
            return;
        }

        if (!ultimaTentativa)
        {
            throw new RespostaIaInvalidaException($"Almoço ou jantar na resposta num dia em que a refeição é da casa: {string.Join(", ", sobrando)}.");
        }

        _logger.LogWarning("Almoço ou jantar da resposta tirado na última tentativa (a refeição é da casa): {Refeicoes}", string.Join(", ", sobrando));
    }

    private static void JuntarRefeicoesDaCasa(List<DiaDietaDto> dias, CasaDaPessoa casa, Dictionary<string, ResultadoMacros> metasPorDia)
    {
        foreach (var dia in dias)
        {
            var nome = dia.DiaSemana.Trim();
            PorcoesCasa.Juntar(dia, casa.RefeicoesPorDia.TryGetValue(nome, out var daCasa) ? daCasa : [], metasPorDia[nome]);
        }
    }

    // ---------- Semana da casa ----------

    public async Task<SemanaCasaDto> GerarSemanaCasaAsync(ListaAlimentos listaDaCasa, string? orcamento, string? ufDoDono)
    {
        var prompt = MontarPromptCasa(listaDaCasa, orcamento, ufDoDono);
        var schema = MontarSchema(listaDaCasa, [PorcoesCasa.NomeAlmoco, PorcoesCasa.NomeJantar]);
        var corpoRequisicao = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json", responseSchema = schema }
        };

        var cronometro = Stopwatch.StartNew();
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                var ultima = tentativa >= MaximoTentativas;
                var (respostaIa, uso, versaoModelo) = await ChamarGeminiAsync(corpoRequisicao);
                var dias = MontarDiasCasa(respostaIa);
                NormalizadorUnidades.Normalizar(dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos));
                AplicarLista(dias, listaDaCasa);

                // A alergia nunca é relaxada: o enum já não deixa o Gemini escolher um item fora da lista da casa, e
                // esta conferência é a segunda trava. Mesmo na última tentativa, vira 503, nunca uma refeição com o
                // alérgeno de alguém.
                var comAlergeno = dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos)
                    .Where(a => RegrasAlergias.Tira(PorIdDaBase[a.AlimentoId!], listaDaCasa.Alergias))
                    .Select(a => a.AlimentoId!)
                    .Distinct()
                    .ToList();
                if (comAlergeno.Count > 0)
                {
                    throw new RespostaIaInvalidaException($"Semana da casa com item fora das alergias da casa: {string.Join(", ", comAlergeno)}.");
                }

                var misturas = RefeicoesComMisturaDemais(dias);
                if (misturas.Count > 0)
                {
                    if (!ultima)
                    {
                        throw new RespostaIaInvalidaException($"Refeições da casa com mais de {MaximoMisturasPorRefeicao} misturas: {string.Join(", ", misturas)}.");
                    }
                    _logger.LogWarning("Misturas acima de {Maximo} cortadas na última tentativa: {Cortadas}",
                        MaximoMisturasPorRefeicao, string.Join("; ", CortarMisturas(dias)));
                }

                var levesAcima = LevesAcimaDoLimite(dias, listaDaCasa);
                if (levesAcima.Count > 0 && !ultima)
                {
                    throw new RespostaIaInvalidaException(
                        $"Grupos do \"prefiro evitar\" da casa acima de {MaximoPorSemanaEvitar} vezes na semana: {string.Join("; ", levesAcima.Select(g => $"[{string.Join(", ", g.Grupo.Order(StringComparer.Ordinal))}] x{g.Posicoes.Count}"))}.");
                }

                var oleosTirados = TirarOleoDosRefogados(dias);
                CalculoPelaBase.Calcular(dias);
                // A referência não tem meta de ninguém: a meta de cada dia é o próprio total, só para a regra dos 5%
                // da troca dos leves (CalculadoraTroca), que compara o dia antes e depois.
                foreach (var dia in dias)
                {
                    dia.MetaCalculada = CalculoPelaBase.Somar([dia.TotalDoDia]);
                }
                if (levesAcima.Count > 0)
                {
                    TrocarLevesExcedentes(dias, listaDaCasa);
                }

                // A referência guarda o nome da lista (sem a contagem): o rótulo sai na porção de cada um.
                var nomePorId = listaDaCasa.Itens.ToDictionary(i => i.Item.Id, i => i.Nome);
                foreach (var a in dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos))
                {
                    a.Nome = nomePorId[a.AlimentoId!];
                }
                CalculoPelaBase.Calcular(dias);

                // Só ids da base, kcal da referência e contagens: nada de pessoa nenhuma.
                _logger.LogInformation(
                    "Semana da casa gerada pelo Gemini ({VersaoModelo}) em {TempoMs} ms ({Tentativas} tentativa(s)); tokens: entrada {TokensEntrada}, resposta {TokensResposta}, total {TokensTotal}. Lista da casa: {Itens} itens, {Leves} leves; azeite/óleo tirado de refogado: {Oleos}. Referência por refeição (kcal): {Kcal}",
                    versaoModelo ?? "não informada", cronometro.ElapsedMilliseconds, tentativa, uso.Entrada, uso.Resposta, uso.Total,
                    listaDaCasa.Itens.Count, listaDaCasa.ItensLeves.Count, oleosTirados.Count,
                    string.Join("; ", dias.SelectMany(d => d.Refeicoes).Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.ChaveCasa} {r.Macros.Calorias:F0}"))));

                return new SemanaCasaDto
                {
                    Dias = dias,
                    VersaoBase = BaseNutricional.VersaoBase,
                    AvisoListaPobre = AvisoListaPobre(listaDaCasa.Minimos)
                };
            }
            catch (RespostaIaInvalidaException ex) when (tentativa < MaximoTentativas)
            {
                _logger.LogWarning(ex, "Resposta inválida do Gemini para a semana da casa na tentativa {Tentativa} ({TempoMs} ms até aqui); tentando de novo",
                    tentativa, cronometro.ElapsedMilliseconds);
            }
        }
    }

    // Os 7 dias, na ordem segunda ... domingo, cada um com exatamente um almoço e um jantar (o enum garante os nomes;
    // a quantidade de refeições, não).
    private static List<DiaDietaDto> MontarDiasCasa(RespostaDiasIaDto respostaIa)
    {
        if (respostaIa.Dias.Count != NomesDias.Length)
        {
            throw new RespostaIaInvalidaException($"Semana da casa com {respostaIa.Dias.Count} dias, em vez de {NomesDias.Length}.");
        }

        var porIndice = new DiaDietaDto?[NomesDias.Length];
        foreach (var d in respostaIa.Dias)
        {
            var indice = Array.FindIndex(NomesDias, n => string.Equals(n, d.DiaSemana.Trim(), StringComparison.OrdinalIgnoreCase));
            if (indice < 0)
            {
                throw new RespostaIaInvalidaException($"Dia '{d.DiaSemana}' da semana da casa não corresponde a nenhum dos 7 dias esperados.");
            }
            if (porIndice[indice] is not null)
            {
                throw new RespostaIaInvalidaException($"Dia '{d.DiaSemana}' veio repetido na semana da casa.");
            }

            var almocos = d.Refeicoes.Where(r => string.Equals(r.Nome.Trim(), PorcoesCasa.NomeAlmoco, StringComparison.OrdinalIgnoreCase)).ToList();
            var jantares = d.Refeicoes.Where(r => string.Equals(r.Nome.Trim(), PorcoesCasa.NomeJantar, StringComparison.OrdinalIgnoreCase)).ToList();
            if (almocos.Count != 1 || jantares.Count != 1 || d.Refeicoes.Count != 2)
            {
                throw new RespostaIaInvalidaException($"{NomesDias[indice]} da semana da casa sem exatamente um almoço e um jantar.");
            }
            if (almocos[0].Alimentos.Count == 0 || jantares[0].Alimentos.Count == 0)
            {
                throw new RespostaIaInvalidaException($"{NomesDias[indice]} da semana da casa com refeição vazia.");
            }

            RefeicaoDto DaCasa(RefeicaoDto r, bool almoco) => new()
            {
                Nome = almoco ? PorcoesCasa.NomeAlmoco : PorcoesCasa.NomeJantar,
                Alimentos = r.Alimentos,
                Macros = r.Macros,
                Horario = r.Horario,
                DaCasa = true,
                ChaveCasa = PorcoesCasa.Chave(indice, almoco)
            };

            porIndice[indice] = new DiaDietaDto
            {
                DiaSemana = NomesDias[indice],
                Refeicoes = [DaCasa(almocos[0], true), DaCasa(jantares[0], false)],
                TotalDoDia = d.TotalDoDia,
                MetaCalculada = d.TotalDoDia
            };
        }

        return porIndice.Select(d => d!).ToList();
    }

    // "Segunda-feira Almoço (3)" para cada refeição com mais misturas que o máximo.
    internal static List<string> RefeicoesComMisturaDemais(List<DiaDietaDto> dias) => dias
        .SelectMany(d => d.Refeicoes.Select(r => (d.DiaSemana, r)))
        .Select(x => (x.DiaSemana, x.r.Nome, Misturas: x.r.Alimentos.Count(a => EMistura(PorIdDaBase[a.AlimentoId!]))))
        .Where(x => x.Misturas > MaximoMisturasPorRefeicao)
        .Select(x => $"{x.DiaSemana} {x.Nome} ({x.Misturas})")
        .ToList();

    /// <summary>Na última tentativa, ficam as 2 misturas de maior quantidade de cada refeição; as outras saem.</summary>
    internal static List<string> CortarMisturas(List<DiaDietaDto> dias)
    {
        var cortadas = new List<string>();
        foreach (var dia in dias)
        {
            foreach (var refeicao in dia.Refeicoes)
            {
                var excedentes = refeicao.Alimentos
                    .Where(a => EMistura(PorIdDaBase[a.AlimentoId!]))
                    .OrderByDescending(a => a.Quantidade)
                    .Skip(MaximoMisturasPorRefeicao)
                    .ToList();
                foreach (var a in excedentes)
                {
                    refeicao.Alimentos.Remove(a);
                    cortadas.Add($"{dia.DiaSemana} {refeicao.Nome}: {a.AlimentoId}");
                }
            }
        }
        return cortadas;
    }

    private static string MontarPromptCasa(ListaAlimentos lista, string? orcamento, string? uf)
    {
        // Sem dado de nenhuma pessoa (plano gratuito do Gemini, decisão de 2026-11-02, e a privacidade da casa): só a
        // lista já cruzada, as regras e o orçamento da casa. A porção de cada um sai depois, em C#.
        var listaTexto = string.Join("\n", GruposNoPrompt
            .Select(g => (g.Rotulo, Nomes: lista.Itens.Where(i => i.Item.Grupo == g.Grupo).Select(i => i.Nome).ToList()))
            .Where(g => g.Nomes.Count > 0)
            .Select(g => $"- {g.Rotulo}: {string.Join("; ", g.Nomes)}"));

        var pesosTexto = string.Join("; ", lista.Itens
            .Where(i => i.Item.MedidaUnidade is not null)
            .Select(i => $"{i.Nome}: 1 {i.Item.MedidaUnidade!.Nome} = {i.Item.MedidaUnidade.Gramas:0} g"));

        // A UF não vai escrita: só decide qual das duas frases da regra do feijão vai.
        var regraFeijao = UfsFeijaoPreto.Contains(uf?.Trim().ToUpperInvariant())
            ? "Os nomes da lista já seguem o costume da região. Nesta região, o feijão mais comum é o preto: prefira \"Feijão preto cozido\" a \"Feijão cozido\"."
            : "Os nomes da lista já seguem o costume da região: use-os como estão, sem trocar por variações regionais.";

        var refogadosNaLista = lista.Itens.Where(i => Refogados.Contains(i.Item.Id)).Select(i => $"\"{i.Nome}\"").ToList();
        var regraRefogado = refogadosNaLista.Count == 0
            ? string.Empty
            : $" {string.Join(" e ", refogadosNaLista)} já incluem o óleo do preparo: na refeição em que um deles aparecer, não coloque azeite nem óleo.";

        // Um grupo por chip, só com os nomes da lista, como no prompt individual (o C# confere o mesmo limite).
        var levesGrupos = lista.GruposLeves
            .Select(grupo => string.Join("; ", lista.Itens.Where(i => grupo.Contains(i.Item.Id)).Select(i => i.Nome)))
            .ToList();
        var levesTexto = levesGrupos.Count > 0
            ? $"\n\nAlimentos da lista que alguém da casa prefere evitar, em grupos (um por linha): use pouco, no máximo {MaximoPorSemanaEvitar} vezes na semana em cada grupo, somando todos os alimentos do grupo:{string.Concat(levesGrupos.Select(g => $"\n- {g}"))}"
            : string.Empty;

        var orcamentoTexto = string.IsNullOrEmpty(orcamento)
            ? string.Empty
            : $"\n\nOrçamento semanal da casa para alimentação: {orcamento}";

        var regraGordura = ListaTemFonteDeGordura(lista)
            ? "\n13. Use as fontes de gordura da lista (óleos e gorduras, como o azeite, e oleaginosas e sementes) no almoço e no jantar, em quantidade de uma refeição comum."
            : string.Empty;

        return $"""
            Você é um nutricionista. Monte o almoço e o jantar de uma semana (7 dias) para uma casa: as mesmas refeições servem a todos os moradores, e a quantidade de cada pessoa é calculada depois, à parte.

            Monte cada refeição numa porção de referência: cerca de {PorcoesCasa.ReferenciaAlmocoKcal:F0} kcal no almoço e cerca de {PorcoesCasa.ReferenciaJantarKcal:F0} kcal no jantar, com proteína, carboidrato e hortaliça no prato.

            Lista de alimentos permitidos. No campo "nome" de cada alimento, use EXATAMENTE um destes nomes, sem acrescentar nada, e nenhum alimento fora desta lista:
            {listaTexto}

            Peso de uma unidade dos itens contáveis: {(string.IsNullOrEmpty(pesosTexto) ? "nenhum item contável na lista" : pesosTexto)}{levesTexto}{orcamentoTexto}

            Regras obrigatórias:
            1. Cada dia tem exatamente duas refeições, com os nomes "Almoço" e "Jantar".
            2. No máximo {MaximoMisturasPorRefeicao} misturas por refeição (carnes, aves, peixes ou ovos). Feijão e outras leguminosas não contam como mistura.
            3. Varie a mistura principal entre os dias da semana: não repita a mesma em dias seguidos.
            4. Não repita a mesma refeição (mesmos alimentos) em dois dias seguidos.
            5. Retorne quantidades realistas e mensuráveis para cada alimento. A quantidade é o peso do alimento PRONTO PARA COMER (ex.: arroz já cozido, frango já grelhado), nunca o peso cru antes do preparo.
            6. Use EXATAMENTE os nomes dos dias (Segunda-feira, Terça-feira, Quarta-feira, Quinta-feira, Sexta-feira, Sábado, Domingo) no campo diaSemana, um de cada.
            7. Adeque as refeições ao hábito alimentar brasileiro, com combinações que uma família brasileira realmente come no almoço e no jantar. Não inclua bebida alcoólica nem bebida com cafeína.
            8. {regraFeijao}
            9. A quantidade é sempre em gramas ("g") ou, para as bebidas, em mililitros ("ml"), nunca em unidades, fatias ou colheres. Para os itens contáveis, use múltiplos exatos do peso de uma unidade listado acima.
            10. Preencha o campo "horario" de cada refeição no formato HH:mm, com horários típicos do brasileiro (almoço entre 12h e 13h30, jantar entre 19h e 21h).
            11. Se o orçamento semanal informado for "economico", priorize proteínas e ingredientes de menor custo (ex: ovo, frango, peixes populares como tilápia, feijão), evitando itens caros como salmão, camarão ou carnes nobres, sem comprometer a qualidade nutricional. Se for "moderado" ou não informado, use bom senso de custo-benefício. Se for "sem_restricao", não considere custo na escolha dos alimentos.
            12. Cada item é um alimento da lista, e preparações com mais de um ingrediente viram itens separados na mesma refeição: uma salada vira um item por ingrediente.{regraRefogado}{regraGordura}
            """;
    }
}
