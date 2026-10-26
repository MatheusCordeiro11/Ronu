using System.Globalization;
using System.Reflection;
using Ronu.Api.Data;

namespace Ronu.Api.Tests;

/// <summary>
/// Fase 2a da base nutricional: grupo de troca e nutriente principal de cada item, e medidas caseiras com
/// fonte. A conferência das medidas é contra o arquivo versionado docs/pof/medidas-pof-itens-mapeados.csv
/// (as linhas da POF 2008-2009 dos alimentos mapeados, como estão na tabela do IBGE): nenhuma medida pode
/// existir sem uma linha da POF com a mesma gramatura, o mesmo alimento e a mesma página.
/// </summary>
public class BaseNutricionalClassificacaoEMedidasTests
{
    private static readonly IReadOnlyList<AlimentoBase> Itens = BaseNutricional.Itens;

    // Os 11 pesos por unidade que a fase 1 já tinha (IBGE, POF 2008-2009): têm de continuar iguais.
    private static readonly Dictionary<string, decimal> PesosDaFase1 = new()
    {
        ["pao-frances"] = 50m, ["pao-de-forma-integral"] = 25m, ["pao-de-queijo-assado"] = 20m,
        ["banana-prata"] = 75m, ["maca-fuji"] = 150m, ["pera-williams"] = 130m, ["laranja-pera"] = 180m,
        ["tangerina"] = 135m, ["kiwi"] = 94m, ["ovo-de-galinha-cozido"] = 45m, ["ovo-de-galinha-frito"] = 50m
    };

    // Marcadas como unidade na fase 2b (2026-10-26), para a contagem "(2 unidades)" no cardápio: medidas da
    // POF que já estavam no código desde a 2a. Abacate, mamão papaia, chuchu e batata-doce ficam em gramas (a
    // unidade é grande demais para uma refeição), uva, castanha-do-pará e noz também (bago e unidade pequenos
    // demais), e a batata inglesa, por ser o carboidrato principal, que a fase 4 precisa ajustar fino.
    private static readonly Dictionary<string, (string Medida, decimal Gramas)> UnidadesDa2b = new()
    {
        ["tomate-cru"] = ("unidade", 100m), ["cenoura-crua"] = ("unidade", 120m), ["cebola-crua"] = ("unidade", 70m),
        ["pepino-cru"] = ("unidade", 100m), ["morango"] = ("unidade", 12m),
        ["goiaba-vermelha"] = ("unidade", 170m), ["melao"] = ("fatia", 90m), ["abacaxi"] = ("fatia", 75m),
        ["melancia"] = ("fatia", 200m), ["mamao-formosa"] = ("fatia", 170m), ["queijo-mucarela"] = ("fatia", 20m),
        ["queijo-prato"] = ("fatia", 15m)
    };

    // ---------- Classificação ----------

    private static T Campo<T>(string nome) =>
        (T)typeof(BaseNutricional).GetField(nome, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void Todo_Item_Tem_Classificacao_E_Nao_Sobra_Classificacao_De_Item_Que_Nao_Existe()
    {
        var classificacao = Campo<IReadOnlyDictionary<string, (GrupoDeTroca, NutrientePrincipal)>>("Classificacao");

        Assert.Equal(Itens.Select(i => i.Id).Order(), classificacao.Keys.Order());
    }

    [Fact]
    public void Medidas_So_De_Item_Que_Existe()
    {
        var medidas = Campo<IReadOnlyDictionary<string, MedidaCaseira[]>>("MedidasPorItem");

        Assert.All(medidas.Keys, id => Assert.Contains(Itens, i => i.Id == id));
    }

    [Fact]
    public void Nutriente_Principal_Entrega_Ao_Menos_10_Por_Cento_Das_Kcal_Do_Item()
    {
        Assert.All(Itens.Where(i => i.Nutriente != NutrientePrincipal.Livre), item =>
        {
            var kcal = item.Nutriente switch
            {
                NutrientePrincipal.Proteina => item.ProteinaG * 4m,
                NutrientePrincipal.Carboidrato => item.CarboidratoG * 4m,
                _ => item.GorduraG * 9m
            };

            Assert.True(kcal >= 0.10m * (item.ProteinaG * 4m + item.CarboidratoG * 4m + item.GorduraG * 9m),
                $"{item.Nome}: {item.Nutriente} é menos de 10% das kcal");
        });
    }

    [Fact]
    public void Livre_So_Em_Hortalicas_Cafes_Chas_E_Bebida_Alcoolica()
    {
        Assert.All(Itens.Where(i => i.Nutriente == NutrientePrincipal.Livre), item =>
            Assert.Contains(item.Grupo, new[] { GrupoDeTroca.Hortalicas, GrupoDeTroca.CafesEChas, GrupoDeTroca.BebidasAlcoolicas }));
        Assert.All(Itens.Where(i => i.Grupo is GrupoDeTroca.Hortalicas or GrupoDeTroca.CafesEChas or GrupoDeTroca.BebidasAlcoolicas), item =>
            Assert.Equal(NutrientePrincipal.Livre, item.Nutriente));
    }

    // Decisão de 2026-10-20: o papel do feijão no prato é a proteína, e ele não se troca por arroz.
    [Fact]
    public void Leguminosas_Sao_Grupo_Proprio_Com_Proteina_Como_Nutriente_Principal()
    {
        var leguminosas = Itens.Where(i => i.Categoria == CategoriaAlimento.Leguminosas).ToList();

        Assert.Equal(4, leguminosas.Count);
        Assert.All(leguminosas, item =>
        {
            Assert.Equal(GrupoDeTroca.Leguminosas, item.Grupo);
            Assert.Equal(NutrientePrincipal.Proteina, item.Nutriente);
        });
        Assert.All(Itens.Where(i => i.Grupo == GrupoDeTroca.Leguminosas), item =>
            Assert.Equal(CategoriaAlimento.Leguminosas, item.Categoria));
    }

    [Fact]
    public void Grupos_De_Um_Item_So_A_Bebida_Alcoolica_E_O_Nutriente_E_O_Mesmo_No_Grupo()
    {
        var sozinhos = Itens.GroupBy(i => i.Grupo).Where(g => g.Count() == 1).Select(g => g.Key).ToList();
        Assert.Equal([GrupoDeTroca.BebidasAlcoolicas], sozinhos);

        // Trocar dentro do grupo preserva o nutriente principal: o grupo não mistura papéis.
        Assert.All(Itens.GroupBy(i => i.Grupo), grupo =>
            Assert.Single(grupo.Select(i => i.Nutriente).Distinct()));
    }

    // Decisão de 2026-10-20: o requeijão é pasta de pão, junto com a manteiga, e não queijo de fatia.
    [Fact]
    public void Requeijao_Esta_No_Grupo_De_Gordura_Com_A_Manteiga()
    {
        var requeijao = Itens.Single(i => i.Id == "requeijao-cremoso");

        Assert.Equal(GrupoDeTroca.OleosEGorduras, requeijao.Grupo);
        Assert.Equal(NutrientePrincipal.Gordura, requeijao.Nutriente);
        Assert.Equal(GrupoDeTroca.OleosEGorduras, Itens.Single(i => i.Id == "manteiga").Grupo);
    }

    [Fact]
    public void Abacate_E_Fonte_De_Gordura_No_Prato()
    {
        var abacate = Itens.Single(i => i.Id == "abacate");

        Assert.Equal(GrupoDeTroca.OleaginosasESementes, abacate.Grupo);
        Assert.Equal(NutrientePrincipal.Gordura, abacate.Nutriente);
    }

    // ---------- Medidas caseiras ----------

    [Fact]
    public void Os_Pesos_Por_Unidade_Sao_Os_11_Da_Fase_1_E_Os_12_Da_2b()
    {
        var comUnidade = Itens.Where(i => i.MedidaUnidade is not null).ToDictionary(i => i.Id, i => i.MedidaUnidade!.Gramas);
        var esperado = PesosDaFase1.Concat(UnidadesDa2b.Select(u => KeyValuePair.Create(u.Key, u.Value.Gramas)));

        Assert.Equal(esperado.OrderBy(p => p.Key), comUnidade.OrderBy(p => p.Key));
    }

    [Fact]
    public void Unidades_Da_2b_Usam_A_Medida_Escolhida_E_Ficam_Fora_Os_Grandes_E_Os_Pequenos()
    {
        foreach (var (id, (medida, gramas)) in UnidadesDa2b)
        {
            var unidade = Itens.Single(i => i.Id == id).MedidaUnidade!;
            Assert.Equal((medida, gramas), (unidade.Nome, unidade.Gramas));
        }

        string[] emGramas = ["abacate", "mamao-papaia", "chuchu-cozido", "uva-italia", "castanha-do-para", "noz", "banana-nanica",
            "batata-inglesa-cozida", "batata-doce-cozida", "lombo-suino-assado"];
        Assert.All(emGramas, id => Assert.Null(Itens.Single(i => i.Id == id).MedidaUnidade));

        // A batata inglesa continua ajustável (a fase 4 a ajusta fino), mesmo tendo a unidade da POF como medida.
        Assert.True(Itens.Single(i => i.Id == "batata-inglesa-cozida").Ajustavel);
    }

    // A unidade de 355 g da batata-doce saiu da espera da 2a (conferida no PDF em 2026-10-26) como medida
    // direta, sem marca de unidade; a fatia de 190 g do lombo ficou fora (medida improvável, ver lacunas).
    [Fact]
    public void Batata_Doce_Ganha_A_Unidade_Da_Pof_E_O_Lombo_Fica_Sem_Fatia()
    {
        var unidade = Itens.Single(i => i.Id == "batata-doce-cozida").Medidas.Single(m => m.Nome == "unidade");
        Assert.Equal((355m, OrigemMedida.Direta, "6400401", 51, false), (unidade.Gramas, unidade.Origem, unidade.CodigoPof, unidade.PaginaPof, unidade.EhUnidade));

        Assert.DoesNotContain(Itens.Single(i => i.Id == "lombo-suino-assado").Medidas, m => m.Nome == "fatia");
    }

    [Fact]
    public void Toda_Medida_Tem_Nome_Gramas_E_Fonte()
    {
        Assert.All(Itens.SelectMany(i => i.Medidas.Select(m => (Item: i, Medida: m))), x =>
        {
            var (item, m) = x;
            Assert.False(string.IsNullOrWhiteSpace(m.Nome), item.Nome);
            Assert.False(string.IsNullOrWhiteSpace(m.NomePlural), item.Nome);
            Assert.True(m.Gramas > 0, $"{item.Nome}: {m.Nome}");
            Assert.Matches(@"^\d{7}$", m.CodigoPof);
            Assert.InRange(m.PaginaPof, 35, 536);   // páginas da tabela de dados no PDF
            Assert.False(string.IsNullOrWhiteSpace(m.DescricaoNaFonte), $"{item.Nome}: {m.Nome}");
        });
    }

    [Fact]
    public void Nao_Repete_A_Mesma_Medida_No_Item()
    {
        Assert.All(Itens, item => Assert.Equal(item.Medidas.Count, item.Medidas.Select(m => m.Nome).Distinct().Count()));
    }

    // Decisão de 2026-10-20: só o filé de 120 g do "peixe cozido" (origem semelhante) e só para os peixes que
    // se comem em filé; é a única medida semelhante da base.
    [Theory]
    [InlineData("file-de-tilapia-grelhado")]
    [InlineData("merluza-assada")]
    [InlineData("corvina-assada")]
    public void Peixe_Comido_Em_File_Tem_So_O_File_Semelhante_De_120_g(string id)
    {
        var medida = Assert.Single(Itens.Single(i => i.Id == id).Medidas);

        Assert.Equal(("filé", 120m, OrigemMedida.Semelhante), (medida.Nome, medida.Gramas, medida.Origem));
        Assert.Equal("Peixe cozido - filé médio", medida.DescricaoNaFonte);
    }

    [Fact]
    public void Semelhante_So_Nos_Tres_Peixes_De_File()
    {
        var semelhantes = Itens.Where(i => i.Medidas.Any(m => m.Origem == OrigemMedida.Semelhante)).Select(i => i.Id).Order();

        Assert.Equal(new[] { "corvina-assada", "file-de-tilapia-grelhado", "merluza-assada" }, semelhantes);
    }

    // Decisão de 2026-10-20: categoria que mistura alimentos (queijo minas padrão para o frescal) e item que não
    // se come assim (salmão em posta, sardinha inteira, músculo e acém, que não são bife) ficam só em gramas.
    [Theory]
    [InlineData("salmao-grelhado")]
    [InlineData("sardinha-assada")]
    [InlineData("queijo-minas-frescal")]
    [InlineData("musculo-bovino-cozido")]
    [InlineData("acem-bovino-cozido")]
    public void Item_Que_Nao_Tem_Medida_Aceita_Fica_So_Em_Gramas(string id) =>
        Assert.Empty(Itens.Single(i => i.Id == id).Medidas);

    // O "bife 100 g" por categoria (a POF mede "bife bovino" para todos os cortes) vale só para os cortes
    // que se comem como bife.
    [Fact]
    public void Bife_De_100_g_So_Nos_Cinco_Cortes_Que_Se_Comem_Como_Bife()
    {
        var comBife = Itens.Where(i => i.Medidas.Any(m => m.Nome == "bife")).Select(i => i.Id).Order();

        Assert.Equal(new[]
        {
            "alcatra-bovina-grelhada", "contrafile-bovino-grelhado", "file-mignon-grelhado",
            "maminha-bovina-grelhada", "patinho-bovino-grelhado"
        }, comBife);
        Assert.All(Itens.SelectMany(i => i.Medidas).Where(m => m.Nome == "bife"), m =>
        {
            Assert.Equal(100m, m.Gramas);
            Assert.Equal(OrigemMedida.Categoria, m.Origem);
        });
    }

    // Decisão de 2026-10-20: do frango, só as medidas diretas; o peito (medido frito/assado) fica em gramas.
    [Theory]
    [InlineData("peito-de-frango-sem-pele-grelhado")]
    [InlineData("peito-de-frango-sem-pele-cozido")]
    [InlineData("coxa-de-frango-sem-pele-cozida")]
    public void Frango_Sem_Medida_Direta_Fica_So_Em_Gramas(string id) =>
        Assert.Empty(Itens.Single(i => i.Id == id).Medidas);

    [Fact]
    public void Copo_Xicara_E_Caneca_So_Em_Bebidas_E_Laticinios_Liquidos()
    {
        var liquidos = new[] { "copo", "xícara", "caneca" };
        Assert.All(Itens.Where(i => i.Medida == "g"), item =>
            Assert.All(item.Medidas.Where(m => liquidos.Any(l => m.Nome.StartsWith(l, StringComparison.Ordinal))), m =>
                Assert.True(
                    item.Grupo is GrupoDeTroca.LeiteEIogurte
                    || item.Id is "farinha-de-mandioca-torrada" or "acai-sem-acucar",
                    $"{item.Nome}: {m.Nome}")));
    }

    // ---------- Conferência contra a POF ----------

    private sealed record LinhaPof(string CodigoAlimento, string Medida, decimal Gramas, string Descricao, int Pagina);

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "pof", "medidas-pof-itens-mapeados.csv")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new FileNotFoundException(
            "docs/pof/medidas-pof-itens-mapeados.csv não encontrado a partir de " + AppContext.BaseDirectory);
    }

    // CSV com campos entre aspas (vírgula e "" dentro do campo).
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

    private static List<LinhaPof> LinhasDaPof()
    {
        var caminho = Path.Combine(RaizDoRepositorio(), "docs", "pof", "medidas-pof-itens-mapeados.csv");
        // Colunas: codigo_alimento, alimento, codigo_preparacao, preparacao, codigo_medida, medida,
        //          codigo_medida_padrao, medida_padrao, gramas, codigo_fonte, descricao_na_fonte, pagina_pdf
        return File.ReadLines(caminho).Skip(1).Select(CamposDoCsv).Select(c => new LinhaPof(
            c[0], c[5], decimal.Parse(c[8], CultureInfo.InvariantCulture), c[10], int.Parse(c[11], CultureInfo.InvariantCulture))).ToList();
    }

    private static string NomeNaPof(string medidaDaPof) =>
        medidaDaPof == "Colher de arroz/servir" ? "colher de servir" : medidaDaPof.ToLowerInvariant();

    private static string SemRuido(string texto) => string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    [Fact]
    public void Toda_Medida_Existe_Na_Tabela_Da_POF_Com_O_Mesmo_Alimento_Gramas_Pagina_E_Descricao()
    {
        var pof = LinhasDaPof();
        Assert.True(pof.Count > 1000, "arquivo de conferência vazio ou truncado");

        Assert.All(Itens.SelectMany(i => i.Medidas.Select(m => (Item: i, Medida: m))), x =>
        {
            var (item, m) = x;
            var achou = pof.Any(l =>
                l.CodigoAlimento == m.CodigoPof
                && NomeNaPof(l.Medida) == m.Nome
                && l.Gramas == m.Gramas
                && l.Pagina == m.PaginaPof
                && SemRuido(l.Descricao).StartsWith(SemRuido(m.DescricaoNaFonte), StringComparison.Ordinal));

            Assert.True(achou, $"{item.Nome}: {m.Nome} {m.Gramas} g não está na POF (alimento {m.CodigoPof}, p. {m.PaginaPof})");
        });
    }
}
