using System.Globalization;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;

namespace Ronu.Api.Services.ListaCompras;

/// <summary>A lista montada, sem o id e a data da dieta, que o controller acrescenta.</summary>
public record ListaComprasMontada(bool Disponivel, string? Motivo, bool AvisoOleo, List<SecaoListaComprasDto> Secoes);

/// <summary>
/// Monta a lista de compras da semana: soma os 7 dias da dieta por item de compra (ConversaoCompras),
/// converte cada item da dieta no que se compra e arredonda para cima. Função pura, sem banco: o
/// controller entrega a dieta e a UF.
/// </summary>
public static class MontadorListaCompras
{
    public const string MotivoDietaAntiga = "dieta-antiga";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly (SecaoMercado Secao, string Chave, string Nome)[] Secoes =
    [
        (SecaoMercado.Hortifruti, "hortifruti", "Hortifrúti"),
        (SecaoMercado.AcougueEPeixaria, "acougue-e-peixaria", "Açougue e peixaria"),
        (SecaoMercado.Padaria, "padaria", "Padaria"),
        (SecaoMercado.FriosLaticiniosEOvos, "frios-laticinios-e-ovos", "Frios, laticínios e ovos"),
        (SecaoMercado.Mercearia, "mercearia", "Mercearia"),
        (SecaoMercado.Bebidas, "bebidas", "Bebidas"),
        (SecaoMercado.Congelados, "congelados", "Congelados"),
    ];

    public static ListaComprasMontada Montar(DietaSemanalDto dieta, string? uf)
    {
        var alimentos = dieta.Dias.SelectMany(d => d.Refeicoes).SelectMany(r => r.Alimentos).ToList();

        // Dieta anterior à lista fechada (ou com item que a base não tem mais): sem lista, para não
        // adivinhar o item pelo nome.
        if (alimentos.Any(a => a.AlimentoId is null || !ConversaoCompras.PorAlimento.ContainsKey(a.AlimentoId)))
        {
            return new ListaComprasMontada(false, MotivoDietaAntiga, false, []);
        }

        // Soma da semana por item da base, na medida da dieta (g ou ml).
        var porAlimento = alimentos
            .Where(a => a.Quantidade > 0)
            .GroupBy(a => a.AlimentoId!)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Quantidade));

        var conversoes = porAlimento.Keys.Select(id => ConversaoCompras.PorAlimento[id]).ToList();
        var ordem = ConversaoCompras.Itens.Select((item, indice) => (item.Chave, indice)).ToDictionary(x => x.Chave, x => x.indice);

        var itens = conversoes
            .GroupBy(c => c.ChaveCompra)
            .Select(g => MontarItem(ConversaoCompras.Itens.Single(i => i.Chave == g.Key), g.ToList(), porAlimento, uf))
            .ToList();

        var secoes = Secoes
            .Select(s => new SecaoListaComprasDto
            {
                Chave = s.Chave,
                Nome = s.Nome,
                Itens = itens.Where(i => i.Secao == s.Secao).OrderBy(i => ordem[i.Dto.Chave]).Select(i => i.Dto).ToList()
            })
            .Where(s => s.Itens.Count > 0)
            .ToList();

        return new ListaComprasMontada(true, null, conversoes.Any(c => c.LevaOleoNoPreparo), secoes);
    }

    private static (SecaoMercado Secao, ItemListaComprasDto Dto) MontarItem(
        ItemCompra compra, List<ConversaoItem> conversoes, Dictionary<string, decimal> porAlimento, string? uf)
    {
        var origens = conversoes
            .OrderBy(c => c.AlimentoId, StringComparer.Ordinal)
            .Select(c =>
            {
                var item = BaseNutricional.Itens.Single(i => i.Id == c.AlimentoId);
                var quantidade = porAlimento[c.AlimentoId];
                return (Conversao: c, Dto: new OrigemItemListaComprasDto
                {
                    AlimentoId = c.AlimentoId,
                    Nome = item.Nome,
                    QuantidadeNaDieta = quantidade,
                    Medida = item.Medida,
                    Fator = Math.Round(c.Fator, 6),
                    Convertido = Math.Round(quantidade * c.Fator, 2),
                    Passos = c.Passos.Select(p => DescreverPasso(p, compra)).ToList()
                }, Exato: quantidade * c.Fator);
            })
            .ToList();

        var exato = origens.Sum(o => o.Exato);
        var quantidade = Arredondar(exato, compra.Unidade);

        return (compra.Secao, new ItemListaComprasDto
        {
            Chave = compra.Chave,
            Nome = NomeNaUf(compra, uf),
            Quantidade = quantidade,
            Unidade = SiglaUnidade(compra.Unidade),
            Texto = Texto(quantidade, compra),
            Pronto = compra.Pronto,
            Nota = compra.NotaNaLinha,
            Detalhe = new DetalheItemListaComprasDto
            {
                QuantidadeExata = Math.Round(exato, 2),
                Origens = origens.Select(o => o.Dto).ToList(),
                Observacoes = conversoes.Select(c => c.Observacao).OfType<string>().Distinct().ToList()
            }
        });
    }

    /// <summary>
    /// Sempre para cima. Contáveis: a próxima unidade inteira (pelo menos 1). Gramas e mililitros: o
    /// próximo múltiplo de 10 até 100, e de 50 acima de 100, nunca zero. Até 2026-10-26 os passos de 10
    /// iam só até 50, e 50,03 g de salmão viravam 100 g, o dobro; o usuário pediu até 100.
    /// </summary>
    public static decimal Arredondar(decimal exato, UnidadeCompra unidade)
    {
        // A divisão em decimal deixa resto na 28ª casa (525 ÷ 75 = 6,99…98, e 3 × (1 ÷ 3) passa de 1): sem
        // este arredondamento, um teto pegaria a unidade de cima por um erro de máquina.
        var q = Math.Round(exato, 6);

        return unidade switch
        {
            UnidadeCompra.Unidades or UnidadeCompra.Fatias or UnidadeCompra.Saches => Math.Max(1m, Math.Ceiling(q)),
            _ when q > 100m => Math.Ceiling(q / 50m) * 50m,
            _ => Math.Max(10m, Math.Ceiling(q / 10m) * 10m)
        };
    }

    /// <summary>"7 bananas", "1 banana", "6 fatias", "550 g", "1,25 kg", "350 ml", "1,05 L".</summary>
    public static string Texto(decimal quantidade, ItemCompra compra) => compra.Unidade switch
    {
        UnidadeCompra.Gramas => quantidade >= 1000m ? $"{Numero(quantidade / 1000m)} kg" : $"{Numero(quantidade)} g",
        UnidadeCompra.Mililitros => quantidade >= 1000m ? $"{Numero(quantidade / 1000m)} L" : $"{Numero(quantidade)} ml",
        _ => $"{Numero(quantidade)} {(quantidade == 1m ? compra.UnidadeSingular : compra.UnidadePlural)}"
    };

    private static string Numero(decimal valor) => valor.ToString("#,0.##", PtBr);

    private static string SiglaUnidade(UnidadeCompra unidade) => unidade switch
    {
        UnidadeCompra.Gramas => "g",
        UnidadeCompra.Mililitros => "ml",
        UnidadeCompra.Unidades => "unidades",
        UnidadeCompra.Fatias => "fatias",
        _ => "saches"
    };

    private static string NomeNaUf(ItemCompra compra, string? uf)
    {
        if (string.IsNullOrWhiteSpace(uf))
        {
            return compra.Nome;
        }

        var sigla = uf.Trim().ToUpperInvariant();
        return compra.Apelidos.FirstOrDefault(a => a.Ufs.Contains(sigla))?.Nome ?? compra.Nome;
    }

    // ---------- A conta de cada passo, para o "Ver detalhe" ----------

    private static string Fator(decimal valor) => valor.ToString("0.0000", PtBr);

    private static string Valor(decimal valor) => valor.ToString("0.##", PtBr);

    private static string Referencia(ILinhaComposicao linha) => linha switch
    {
        LinhaTaco taco => $"TACO {taco.Numero} ({taco.Nome}, p. {taco.Pagina})",
        LinhaUsda usda => $"USDA SR Legacy {usda.Ndb}, fdcId {usda.FdcId} ({usda.Nome})",
        _ => linha.Codigo
    };

    private static decimal ValorDoNutriente(ILinhaComposicao linha, NutrienteDoPar nutriente) => nutriente switch
    {
        NutrienteDoPar.Carboidrato => linha.Carboidrato,
        NutrienteDoPar.Proteina => linha.Proteina,
        _ => 100m - linha.Umidade
    };

    private static PassoListaComprasDto DescreverPasso(PassoConversao passo, ItemCompra compra) => passo switch
    {
        PassoParCruCozido par => new PassoListaComprasDto
        {
            Tipo = "par-cru-cozido",
            Fator = Math.Round(par.Fator, 6),
            Descricao = $"Cozido para cru {NomeNutriente(par.Nutriente)}: {Referencia(par.Cozido)}: "
                + $"{Valor(ValorDoNutriente(par.Cozido, par.Nutriente))} ÷ {Referencia(par.Cru)}: "
                + $"{Valor(ValorDoNutriente(par.Cru, par.Nutriente))} = {Fator(par.FatorDaFonte)}"
                + (par.TetoAplicado ? ". Acima de 1: a lista usa 1 (calibração do projeto)." : ".")
        },
        PassoDescarte descarte => new PassoListaComprasDto
        {
            Tipo = "descarte",
            Fator = Math.Round(descarte.Fator, 6),
            Descricao = $"Descarte do USDA SR Legacy {descarte.Fonte.Ndb}, fdcId {descarte.Fonte.FdcId} ({descarte.Fonte.Nome}): "
                + $"{descarte.Fonte.DescartePct}% ({descarte.Fonte.DescarteDescricao}). Peso comprado = peso comestível × {Fator(descarte.Fator)}."
        },
        PassoUnidade unidade => new PassoListaComprasDto
        {
            Tipo = "unidade",
            Fator = Math.Round(unidade.Fator, 6),
            Descricao = $"1 {compra.UnidadeSingular} = {Valor(unidade.Gramas)} g ({unidade.Fonte})."
        },
        PassoReceita receita => new PassoListaComprasDto
        {
            Tipo = "receita",
            Fator = Math.Round(receita.Fator, 6),
            Descricao = $"Receita da TACO {receita.TrechoIngrediente.Codigo} (p. {receita.TrechoIngrediente.Pagina}): "
                + $"\"{receita.TrechoIngrediente.Texto}\" \"{receita.TrechoAgua.Texto}\" = "
                + $"{Valor(receita.Quantidade)} {(compra.Unidade == UnidadeCompra.Saches ? "sachês" : "g")} para {Numero(receita.MlDeAgua)} ml de água."
        },
        PassoCalibracao calibracao => new PassoListaComprasDto
        {
            Tipo = "calibracao",
            Fator = calibracao.Valor,
            Descricao = calibracao.Motivo
        },
        PassoFeculaDaTapioca tapioca => new PassoListaComprasDto
        {
            Tipo = "derivacao",
            Fator = Math.Round(tapioca.Fator, 6),
            Descricao = $"Derivação da tapioca (TACO {tapioca.Tapioca.Numero}, {tapioca.Manteiga.Numero} e {tapioca.Fecula.Numero}): "
                + $"sem a manteiga, a tapioca tem {(100m * tapioca.MateriaSecaSemManteiga).ToString("0.0", PtBr)}% de matéria seca, "
                + $"contra {Valor(100m - tapioca.Fecula.Umidade)}% da fécula: {Fator(tapioca.Fator)} g de fécula por g de tapioca."
        },
        _ => throw new InvalidOperationException($"Passo sem descrição: {passo.GetType().Name}")
    };

    private static string NomeNutriente(NutrienteDoPar nutriente) => nutriente switch
    {
        NutrienteDoPar.Carboidrato => "pelo carboidrato",
        NutrienteDoPar.Proteina => "pela proteína",
        _ => "pela matéria seca (100 − umidade)"
    };
}
