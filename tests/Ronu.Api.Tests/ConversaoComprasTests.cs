using System.Globalization;
using System.Text.RegularExpressions;
using Ronu.Api.Data;

namespace Ronu.Api.Tests;

/// <summary>
/// Lista de compras, etapa 1: de cada item da base para o que se compra. A conferência das linhas da
/// TACO e do USDA é contra os arquivos versionados de docs/compras/ (extraídos das fontes oficiais pelo
/// script de lá), e o lado cozido de cada par é conferido contra a própria BaseNutricional. Os fatores
/// esperados foram calculados à parte, em Python, sobre os mesmos CSVs.
/// </summary>
public class ConversaoComprasTests
{
    private static readonly IReadOnlyList<AlimentoBase> Base = BaseNutricional.Itens;
    private static readonly IReadOnlyDictionary<string, ConversaoItem> Conversoes = ConversaoCompras.PorAlimento;

    private static ItemCompra Compra(string chave) => ConversaoCompras.Itens.Single(i => i.Chave == chave);

    private static IEnumerable<T> Passos<T>(ConversaoItem conversao) => conversao.Passos.OfType<T>();

    // ---------- Cobertura ----------

    [Fact]
    public void Todo_Item_Da_Base_Tem_Conversao_E_Nao_Sobra_Conversao_De_Item_Que_Nao_Existe()
    {
        Assert.Equal(Base.Select(i => i.Id).Order(), Conversoes.Keys.Order());
    }

    [Fact]
    public void Toda_Conversao_Aponta_Para_Um_Item_De_Compra_E_Todo_Item_De_Compra_E_Usado()
    {
        var chaves = ConversaoCompras.Itens.Select(i => i.Chave).ToList();

        Assert.Equal(chaves.Count, chaves.Distinct().Count());
        Assert.All(Conversoes.Values, c => Assert.Contains(c.ChaveCompra, chaves));
        Assert.All(chaves, chave => Assert.Contains(Conversoes.Values, c => c.ChaveCompra == chave));
    }

    // A chave vai para a URL do PUT do "já tenho" (etapa 3), que só aceita este formato.
    [Fact]
    public void Chave_De_Compra_Tem_So_Minusculas_Numeros_E_Hifen_Ate_64_Caracteres()
    {
        Assert.All(ConversaoCompras.Itens, i => Assert.Matches("^[a-z0-9-]{1,64}$", i.Chave));
    }

    // ---------- Conferência contra as fontes ----------

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "compras", "taco-tabela1.csv")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new FileNotFoundException(
            "docs/compras/taco-tabela1.csv não encontrado a partir de " + AppContext.BaseDirectory);
    }

    // CSV com campos entre aspas (vírgula e "" dentro do campo), como o da POF.
    private static List<string> CamposDoCsv(string linha)
    {
        var campos = new List<string>();
        var atual = new System.Text.StringBuilder();
        var entreAspas = false;
        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];
            if (entreAspas && c == '"' && i + 1 < linha.Length && linha[i + 1] == '"') { atual.Append('"'); i++; }
            else if (c == '"') entreAspas = !entreAspas;
            else if (c == ',' && !entreAspas) { campos.Add(atual.ToString()); atual.Clear(); }
            else atual.Append(c);
        }
        campos.Add(atual.ToString());
        return campos;
    }

    private static List<List<string>> LinhasDoCsv(string arquivo) =>
        File.ReadLines(Path.Combine(RaizDoRepositorio(), "docs", "compras", arquivo)).Skip(1).Select(CamposDoCsv).ToList();

    // A TACO escreve "70,1"; "Tr" (traço) fica nulo.
    private static decimal? NumeroTaco(string texto) =>
        texto == "Tr" ? null : decimal.Parse(texto.Replace(',', '.'), CultureInfo.InvariantCulture);

    private static decimal Numero(string texto) => decimal.Parse(texto, CultureInfo.InvariantCulture);

    [Fact]
    public void Linhas_Da_Taco_Sao_As_Do_Csv_Com_Codigo_Nome_Pagina_E_Valores()
    {
        // Colunas: codigo, nome_na_fonte, pagina_pdf, umidade, proteina, lipideos, carboidrato
        var csv = LinhasDoCsv("taco-tabela1.csv");

        Assert.Equal(csv.Select(c => int.Parse(c[0], CultureInfo.InvariantCulture)).Order(), ConversaoCompras.Taco.Keys.Order());
        Assert.All(csv, c =>
        {
            var linha = ConversaoCompras.Taco[int.Parse(c[0], CultureInfo.InvariantCulture)];
            Assert.Equal(c[1], linha.Nome);
            Assert.Equal(int.Parse(c[2], CultureInfo.InvariantCulture), linha.Pagina);
            Assert.Equal(NumeroTaco(c[3]), linha.Umidade);
            Assert.Equal(NumeroTaco(c[4]), linha.Proteina);
            Assert.Equal(NumeroTaco(c[5]), linha.Lipideos);
            Assert.Equal(NumeroTaco(c[6]), linha.Carboidrato);
        });
    }

    [Fact]
    public void Linhas_Do_Usda_Sao_As_Do_Csv_Com_Ndb_FdcId_Valores_E_Descarte()
    {
        // Colunas: ndb, fdc_id, descricao, agua, proteina, carboidrato, descarte_pct, descarte_descricao, unidade, gramas_unidade
        var csv = LinhasDoCsv("usda-sr28.csv");

        Assert.Equal(csv.Select(c => c[0]).Order(), ConversaoCompras.Usda.Keys.Order());
        Assert.All(csv, c =>
        {
            var linha = ConversaoCompras.Usda[c[0]];
            Assert.Equal(c[1], linha.FdcId);
            Assert.Equal(c[2], linha.Nome);
            Assert.Equal(Numero(c[3]), linha.Umidade);
            Assert.Equal(Numero(c[4]), linha.Proteina);
            Assert.Equal(Numero(c[5]), linha.Carboidrato);
            Assert.Equal(int.Parse(c[6], CultureInfo.InvariantCulture), linha.DescartePct);
            Assert.Equal(c[7], linha.DescarteDescricao);
            Assert.Equal(c[8] == "" ? null : c[8], linha.Unidade);
            Assert.Equal(c[9] == "" ? null : Numero(c[9]), linha.GramasUnidade);
        });
    }

    [Fact]
    public void Trechos_Da_Taco_Sao_Os_Do_Csv_Com_Codigo_E_Pagina()
    {
        // Colunas: codigo, titulo, pagina_pdf, trecho
        var csv = LinhasDoCsv("taco-trechos.csv")
            .Select(c => new TrechoTaco(int.Parse(c[0], CultureInfo.InvariantCulture), int.Parse(c[2], CultureInfo.InvariantCulture), c[3]))
            .ToList();

        Assert.Equal(csv, ConversaoCompras.Trechos);
    }

    // O número da receita está escrito no trecho da TACO, não só no código.
    [Fact]
    public void Quantidades_Das_Receitas_Estao_Nos_Trechos_Da_Taco()
    {
        var receitas = Conversoes.Values.SelectMany(Passos<PassoReceita>).ToList();

        Assert.Equal(4, receitas.Count);
        Assert.All(receitas, r =>
        {
            Assert.Contains("4 litros de água", r.TrechoAgua.Texto);
            Assert.Equal(r.TrechoIngrediente.Codigo, r.TrechoAgua.Codigo);
            Assert.Equal(4000m, r.MlDeAgua);
            var quantidade = r.Quantidade.ToString(CultureInfo.InvariantCulture);
            Assert.Matches($@"\b{quantidade} ?(g|gramas)\b", r.TrechoIngrediente.Texto);
        });
    }

    // ---------- Pares cru/cozido ----------

    private static decimal Arredonda1(decimal valor) => Math.Round(valor, 1, MidpointRounding.AwayFromZero);

    [Fact]
    public void Lado_Cozido_Do_Par_E_A_Linha_Do_Proprio_Item_Da_Base()
    {
        var comPar = Conversoes.Values.Where(c => Passos<PassoParCruCozido>(c).Any()).ToList();
        // 13 por carboidrato, 8 por matéria seca e 24 por proteína.
        Assert.Equal(45, comPar.Count);

        Assert.All(comPar, c =>
        {
            var item = Base.Single(i => i.Id == c.AlimentoId);
            var cozido = Passos<PassoParCruCozido>(c).Single().Cozido;

            if (cozido is LinhaUsda usda)
            {
                Assert.Equal(FonteNutricional.Usda, item.Fonte);
                Assert.Equal(item.CodigoFonte, usda.FdcId);
                // A base guarda o USDA com 1 casa (30,86 → 30,9).
                Assert.Equal(item.ProteinaG, Arredonda1(usda.Proteina));
                Assert.Equal(item.CarboidratoG, Arredonda1(usda.Carboidrato));
            }
            else
            {
                Assert.Contains(item.Fonte, new[] { FonteNutricional.Taco, FonteNutricional.Derivado });
                Assert.Equal(item.CodigoFonte, cozido.Codigo);
                Assert.Equal(item.ProteinaG, cozido.Proteina);
                Assert.Equal(item.CarboidratoG, cozido.Carboidrato);
            }
        });
    }

    [Fact]
    public void Lado_Cru_Do_Par_E_Um_Alimento_Cru()
    {
        var crus = Conversoes.Values.SelectMany(Passos<PassoParCruCozido>).Select(p => p.Cru.Nome).Distinct().ToList();

        // A farinha de milho do cuscuz (TACO 33) é o ingrediente cru da receita, sem "cru" no nome.
        Assert.All(crus.Where(n => n != "Farinha, de milho, amarela"), nome =>
            Assert.Matches(new Regex(@"\bcrua?\b|\braw\b|\bdry\b"), nome));
    }

    [Fact]
    public void Nutriente_Do_Par_Segue_A_Categoria_Do_Item()
    {
        Assert.All(Conversoes.Values.Where(c => Passos<PassoParCruCozido>(c).Any()), c =>
        {
            var categoria = Base.Single(i => i.Id == c.AlimentoId).Categoria;
            var par = Passos<PassoParCruCozido>(c).Single();
            var esperado = categoria switch
            {
                CategoriaAlimento.CarneBovina or CategoriaAlimento.Aves or CategoriaAlimento.CarneSuina or CategoriaAlimento.Peixes
                    => NutrienteDoPar.Proteina,
                CategoriaAlimento.Hortalicas => NutrienteDoPar.MateriaSeca,
                _ => NutrienteDoPar.Carboidrato
            };
            Assert.Equal(esperado, par.Nutriente);
            Assert.Equal(categoria == CategoriaAlimento.Hortalicas, par.TetoUm);
        });
    }

    // D1 (2026-10-26): hortaliça cozida nunca pede menos cru do que se come.
    [Fact]
    public void Hortalica_Cozida_Nunca_Passa_De_1_E_O_Teto_E_Explicado_Como_Calibracao()
    {
        var hortalicas = Conversoes.Values.Where(c => Passos<PassoParCruCozido>(c).Any(p => p.Nutriente == NutrienteDoPar.MateriaSeca)).ToList();

        Assert.All(hortalicas, c => Assert.True(Passos<PassoParCruCozido>(c).Single().Fator <= 1m, c.AlimentoId));

        var comTeto = hortalicas.Where(c => Passos<PassoParCruCozido>(c).Single().TetoAplicado).Select(c => c.AlimentoId).Order();
        Assert.Equal(new[] { "abobora-cozida", "chuchu-cozido" }, comTeto);
        Assert.All(hortalicas.Where(c => Passos<PassoParCruCozido>(c).Single().TetoAplicado), c =>
            Assert.StartsWith("Calibração do projeto", c.Observacao));
    }

    // ---------- Unidades, descarte, pronto ----------

    // Itens com unidade na base que a lista compra por peso: fatia não é unidade de compra (abacaxi,
    // melancia, melão, mamão formosa, queijos), e o morango vai por peso (D10).
    private static readonly string[] UnidadeDaBaseMasPorPeso =
        ["abacaxi", "melancia", "melao", "mamao-formosa", "morango", "queijo-mucarela", "queijo-prato"];

    [Fact]
    public void Contaveis_Usam_O_Peso_Da_Unidade_Da_Base_E_A_Unidade_De_Compra_E_De_Contagem()
    {
        foreach (var item in Base.Where(i => i.MedidaUnidade is not null))
        {
            var conversao = Conversoes[item.Id];
            if (UnidadeDaBaseMasPorPeso.Contains(item.Id))
            {
                Assert.Empty(Passos<PassoUnidade>(conversao));
                continue;
            }
            Assert.Equal(item.MedidaUnidade!.Gramas, Passos<PassoUnidade>(conversao).Single().Gramas);
        }

        Assert.All(Conversoes.Values.Where(c => Passos<PassoUnidade>(c).Any()), c =>
        {
            var compra = Compra(c.ChaveCompra);
            Assert.Contains(compra.Unidade, new[] { UnidadeCompra.Unidades, UnidadeCompra.Fatias });
            Assert.False(string.IsNullOrEmpty(compra.UnidadeSingular));
            Assert.False(string.IsNullOrEmpty(compra.UnidadePlural));
        });
    }

    // A cenoura cozida vira cenoura crua e conta pela unidade da crua.
    [Fact]
    public void Cenoura_Cozida_Conta_Pela_Unidade_Da_Cenoura_Crua()
    {
        var unidade = Passos<PassoUnidade>(Conversoes["cenoura-cozida"]).Single();

        Assert.Equal(Base.Single(i => i.Id == "cenoura-crua").MedidaUnidade!.Gramas, unidade.Gramas);
    }

    [Fact]
    public void Descarte_So_Nos_Itens_Aprovados()
    {
        var comDescarte = Conversoes.Values
            .Where(c => Passos<PassoDescarte>(c).Any())
            .ToDictionary(c => c.AlimentoId, c => Passos<PassoDescarte>(c).Single().Fonte.Ndb);

        Assert.Equal(new Dictionary<string, string>
        {
            ["batata-inglesa-cozida"] = "11352", ["batata-doce-cozida"] = "11507", ["mandioca-cozida"] = "11134",
            ["banana-nanica"] = "09040", ["mamao-papaia"] = "09226", ["mamao-formosa"] = "09226", ["abacaxi"] = "09266",
            ["melancia"] = "09326", ["melao"] = "09184", ["manga-palmer"] = "09176", ["abacate"] = "09037",
            ["frango-sem-pele-assado"] = "05011"
        }, comDescarte);
    }

    [Fact]
    public void Pronto_So_Nos_Refogados_E_No_Suco_E_Sem_Conversao()
    {
        var prontos = ConversaoCompras.Itens.Where(i => i.Pronto).Select(i => i.Chave).Order();

        Assert.Equal(new[] { "abobrinha-refogada", "couve-refogada", "espinafre-refogado", "suco-de-laranja" }, prontos);
        Assert.All(Conversoes.Values.Where(c => Compra(c.ChaveCompra).Pronto), c => Assert.Empty(c.Passos));
    }

    [Fact]
    public void Aviso_Do_Oleo_So_Nos_Refogados_E_No_Ovo_Frito()
    {
        var comOleo = Conversoes.Values.Where(c => c.LevaOleoNoPreparo).Select(c => c.AlimentoId).Order();

        Assert.Equal(new[] { "abobrinha-refogada", "couve-refogada", "espinafre-refogado", "ovo-de-galinha-frito" }, comOleo);
    }

    // D3: clara junta com ovo inteiro em "Ovos"; fator 1 de calibração e 33 g de clara por ovo.
    [Fact]
    public void Clara_Vira_Ovos_Com_Fator_1_De_Calibracao_E_33_G_Por_Ovo()
    {
        var clara = Conversoes["clara-de-ovo-cozida"];

        Assert.Equal("ovos", clara.ChaveCompra);
        Assert.Equal(1m, Passos<PassoCalibracao>(clara).Single().Valor);
        Assert.StartsWith("Calibração do projeto", Passos<PassoCalibracao>(clara).Single().Motivo);
        Assert.Equal(33m, Passos<PassoUnidade>(clara).Single().Gramas);
        Assert.Equal("Sobram as gemas.", clara.Observacao);
        // 100 g de clara = 3,03 ovos.
        Assert.Equal(3.03m, Math.Round(100m * clara.Fator, 2));
    }

    [Fact]
    public void Ovo_Cozido_Ovo_Frito_E_Clara_Somam_Num_Item_So()
    {
        var ovos = Conversoes.Values.Where(c => c.ChaveCompra == "ovos").Select(c => c.AlimentoId).Order();

        Assert.Equal(new[] { "clara-de-ovo-cozida", "ovo-de-galinha-cozido", "ovo-de-galinha-frito" }, ovos);
    }

    // A derivação da base diz: 100 g de tapioca = 89,7 g de fécula. A conta daqui tem de chegar no mesmo.
    [Fact]
    public void Fecula_Da_Tapioca_Bate_Com_A_Derivacao_Da_Base()
    {
        Assert.Contains("89,7 g de fécula", Base.Single(i => i.Id == "tapioca").Observacao);
        Assert.Equal(89.7m, Math.Round(100m * Conversoes["tapioca"].Fator, 1));
    }

    // ---------- Seções e nomes ----------

    private static SecaoMercado SecaoDaCategoria(CategoriaAlimento categoria) => categoria switch
    {
        CategoriaAlimento.TuberculosERaizes or CategoriaAlimento.Hortalicas or CategoriaAlimento.Frutas => SecaoMercado.Hortifruti,
        CategoriaAlimento.CarneBovina or CategoriaAlimento.Aves or CategoriaAlimento.CarneSuina or CategoriaAlimento.Peixes => SecaoMercado.AcougueEPeixaria,
        CategoriaAlimento.Paes => SecaoMercado.Padaria,
        CategoriaAlimento.Ovos or CategoriaAlimento.Laticinios => SecaoMercado.FriosLaticiniosEOvos,
        CategoriaAlimento.Bebidas or CategoriaAlimento.BebidasAlcoolicas => SecaoMercado.Bebidas,
        _ => SecaoMercado.Mercearia
    };

    // Exceções aprovadas no passo 0 (2026-10-26): onde a categoria engana.
    private static readonly Dictionary<string, SecaoMercado> ExcecoesDeSecao = new()
    {
        ["atum"] = SecaoMercado.Mercearia,
        ["acai"] = SecaoMercado.Congelados,
        ["pao-de-queijo"] = SecaoMercado.Congelados,
        ["manteiga"] = SecaoMercado.FriosLaticiniosEOvos,
        ["cafe-em-po"] = SecaoMercado.Mercearia,
        ["cha-mate"] = SecaoMercado.Mercearia,
        ["cha-preto"] = SecaoMercado.Mercearia,
        ["cha-de-erva-doce"] = SecaoMercado.Mercearia,
    };

    [Fact]
    public void Secao_Vem_Da_Categoria_Salvo_As_Excecoes_Aprovadas()
    {
        Assert.All(Conversoes.Values, c =>
        {
            var compra = Compra(c.ChaveCompra);
            var esperada = ExcecoesDeSecao.TryGetValue(compra.Chave, out var excecao)
                ? excecao
                : SecaoDaCategoria(Base.Single(i => i.Id == c.AlimentoId).Categoria);
            Assert.True(esperada == compra.Secao, $"{compra.Chave}: {compra.Secao}, esperado {esperada}");
        });
    }

    [Fact]
    public void Apelidos_Da_Mandioca_Tem_As_Mesmas_Ufs_Da_Base()
    {
        var naBase = Base.Single(i => i.Id == "mandioca-cozida").Apelidos;
        var naCompra = Compra("mandioca").Apelidos;

        Assert.Equal(naBase.Select(a => a.Nome.Replace(" cozida", "", StringComparison.Ordinal).Replace(" cozido", "", StringComparison.Ordinal)),
            naCompra.Select(a => a.Nome));
        Assert.Equal(naBase.Select(a => string.Join(",", a.Ufs)), naCompra.Select(a => string.Join(",", a.Ufs)));
        Assert.All(ConversaoCompras.Itens.Where(i => i.Chave != "mandioca"), i => Assert.Empty(i.Apelidos));
    }

    // D5: coxa, sobrecoxa e bisteca dizem na própria linha que o peso é de carne sem osso. A bisteca
    // não fala em pele.
    [Theory]
    [InlineData("coxa-de-frango", "de carne, sem osso e sem pele · compre um pouco mais")]
    [InlineData("sobrecoxa-de-frango", "de carne, sem osso e sem pele · compre um pouco mais")]
    [InlineData("bisteca", "de carne, sem osso · compre um pouco mais")]
    public void Carne_Com_Osso_Avisa_Na_Linha_Que_O_Peso_E_Sem_Osso(string chave, string nota)
    {
        Assert.Equal(nota, Compra(chave).NotaNaLinha);
    }

    // ---------- Fatores ----------

    // Calculados à parte, em Python, sobre os CSVs de docs/compras/ (6 casas, arredondamento comercial).
    [Theory]
    [InlineData("pao-frances", "0.020000")]
    [InlineData("pao-de-forma-integral", "0.040000")]
    [InlineData("pao-de-queijo-assado", "0.050000")]
    [InlineData("tomate-cru", "0.010000")]
    [InlineData("pepino-cru", "0.010000")]
    [InlineData("cenoura-crua", "0.008333")]
    [InlineData("cebola-crua", "0.014286")]
    [InlineData("banana-prata", "0.013333")]
    [InlineData("maca-fuji", "0.006667")]
    [InlineData("pera-williams", "0.007692")]
    [InlineData("laranja-pera", "0.005556")]
    [InlineData("tangerina", "0.007407")]
    [InlineData("kiwi", "0.010638")]
    [InlineData("goiaba-vermelha", "0.005882")]
    [InlineData("ovo-de-galinha-cozido", "0.022222")]
    [InlineData("ovo-de-galinha-frito", "0.020000")]
    [InlineData("arroz-branco-cozido", "0.356599")]
    [InlineData("arroz-integral-cozido", "0.332903")]
    [InlineData("macarrao-cozido", "0.413285")]
    [InlineData("cuscuz-de-milho-cozido", "0.319848")]
    [InlineData("feijao-cozido", "0.222222")]
    [InlineData("feijao-preto-cozido", "0.238095")]
    [InlineData("feijao-fradinho-cozido", "0.220588")]
    [InlineData("lentilha-cozida", "0.262903")]
    [InlineData("batata-inglesa-cozida", "1.079365")]
    [InlineData("batata-doce-cozida", "0.906225")]
    [InlineData("mandioca-cozida", "0.989871")]
    [InlineData("mandioquinha-cozida", "0.787500")]
    [InlineData("cara-cozido", "0.821739")]
    [InlineData("cenoura-cozida", "0.006987")]
    [InlineData("beterraba-cozida", "0.671429")]
    [InlineData("brocolis-cozido", "0.840909")]
    [InlineData("couve-flor-cozida", "0.791667")]
    [InlineData("abobrinha-cozida", "0.770492")]
    [InlineData("chuchu-cozido", "1.000000")]
    [InlineData("abobora-cozida", "1.000000")]
    [InlineData("berinjela-cozida", "0.903226")]
    [InlineData("banana-nanica", "1.562500")]
    [InlineData("mamao-papaia", "1.612903")]
    [InlineData("mamao-formosa", "1.612903")]
    [InlineData("abacaxi", "1.960784")]
    [InlineData("melancia", "1.923077")]
    [InlineData("melao", "2.173913")]
    [InlineData("manga-palmer", "1.408451")]
    [InlineData("abacate", "1.351351")]
    [InlineData("patinho-bovino-grelhado", "1.654378")]
    [InlineData("patinho-bovino-moido-refogado", "1.654378")]
    [InlineData("carne-moida-de-acem-cozida", "1.376289")]
    [InlineData("acem-bovino-cozido", "1.312500")]
    [InlineData("alcatra-bovina-grelhada", "1.476852")]
    [InlineData("contrafile-bovino-grelhado", "1.495833")]
    [InlineData("coxao-mole-bovino-cozido", "1.528302")]
    [InlineData("lagarto-bovino-cozido", "1.604878")]
    [InlineData("musculo-bovino-cozido", "1.444444")]
    [InlineData("maminha-bovina-grelhada", "1.468900")]
    [InlineData("file-mignon-grelhado", "1.518519")]
    [InlineData("peito-de-frango-sem-pele-grelhado", "1.488372")]
    [InlineData("peito-de-frango-sem-pele-cozido", "1.465116")]
    [InlineData("coxa-de-frango-sem-pele-cozida", "1.511236")]
    [InlineData("sobrecoxa-de-frango-sem-pele-assada", "1.659091")]
    [InlineData("frango-sem-pele-assado", "2.831715")]
    [InlineData("lombo-suino-assado", "1.579646")]
    [InlineData("bisteca-suina-grelhada", "1.344186")]
    [InlineData("pernil-suino-assado", "1.597015")]
    [InlineData("file-de-tilapia-grelhado", "1.302291")]
    [InlineData("salmao-grelhado", "1.352332")]
    [InlineData("sardinha-assada", "1.526066")]
    [InlineData("merluza-assada", "1.602410")]
    [InlineData("corvina-assada", "1.440860")]
    [InlineData("clara-de-ovo-cozida", "0.030303")]
    [InlineData("cafe-coado", "0.100000")]
    [InlineData("cha-mate", "0.005000")]
    [InlineData("cha-preto", "0.005000")]
    [InlineData("cha-de-erva-doce", "0.005000")]
    [InlineData("tapioca", "0.896749")]
    public void Fator_Confere_Com_A_Conta_Independente(string alimentoId, string esperado)
    {
        var fator = Math.Round(Conversoes[alimentoId].Fator, 6, MidpointRounding.AwayFromZero);

        Assert.Equal(decimal.Parse(esperado, CultureInfo.InvariantCulture), fator);
    }

    // Os 46 restantes são "só soma" ou "pronto": fator 1, sem passo nenhum.
    [Fact]
    public void Itens_Sem_Conversao_Tem_Fator_1()
    {
        var semPasso = Conversoes.Values.Where(c => c.Passos.Count == 0).ToList();

        Assert.Equal(46, semPasso.Count);
        Assert.All(semPasso, c => Assert.Equal(1m, c.Fator));
    }
}
