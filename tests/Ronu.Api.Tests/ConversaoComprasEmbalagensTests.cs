using System.Globalization;
using Ronu.Api.Data;

namespace Ronu.Api.Tests;

/// <summary>
/// Embalagens da lista de compras (2026-11-16): o grupo de cada item de compra e os tamanhos, conferidos contra
/// docs/compras/embalagens.csv, onde cada tamanho tem a fonte, a amostra e o motivo.
/// </summary>
public class ConversaoComprasEmbalagensTests
{
    // Colunas: chave, grupo, tamanho, unidade, rotulo, nome_singular, nome_plural, tipo_fonte, referencia,
    // n_amostra, n_tamanho, motivo
    private sealed record Linha(string Chave, string Grupo, decimal? Tamanho, string Unidade, string Rotulo, string Singular,
        string Plural, string TipoFonte, string Referencia, int? Amostra, int? DoTamanho, string Motivo);

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "compras", "embalagens.csv")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new FileNotFoundException(
            "docs/compras/embalagens.csv não encontrado a partir de " + AppContext.BaseDirectory);
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

    private static int? Inteiro(string texto) => texto == "" ? null : int.Parse(texto, CultureInfo.InvariantCulture);

    private static readonly List<Linha> Csv = File.ReadLines(Path.Combine(RaizDoRepositorio(), "docs", "compras", "embalagens.csv"))
        .Skip(1)
        .Select(CamposDoCsv)
        .Select(c =>
        {
            Assert.Equal(12, c.Count);
            return new Linha(c[0], c[1], c[2] == "" ? null : decimal.Parse(c[2], CultureInfo.InvariantCulture), c[3], c[4], c[5], c[6],
                c[7], c[8], Inteiro(c[9]), Inteiro(c[10]), c[11]);
        })
        .ToList();

    private static string Grupo(GrupoEmbalagem grupo) => grupo switch
    {
        GrupoEmbalagem.Embalagem => "embalagem",
        GrupoEmbalagem.Granel => "granel",
        GrupoEmbalagem.Contavel => "contavel",
        GrupoEmbalagem.Pronto => "pronto",
        _ => "peca"
    };

    private static string Fonte(FonteEmbalagem fonte) => fonte switch
    {
        FonteEmbalagem.OpenFoodFacts => "off",
        FonteEmbalagem.Convencao => "convencao",
        _ => "calibracao"
    };

    private static string Sigla(UnidadeCompra unidade) => unidade switch
    {
        UnidadeCompra.Gramas => "g",
        UnidadeCompra.Mililitros => "ml",
        UnidadeCompra.Unidades => "unidades",
        UnidadeCompra.Fatias => "fatias",
        _ => "saches"
    };

    // ---------- Cobertura ----------

    [Fact]
    public void Todo_Item_De_Compra_Tem_Um_Grupo_So_No_Csv_E_No_Codigo_E_Nao_Sobra_Chave()
    {
        var chaves = ConversaoCompras.Itens.Select(i => i.Chave).Order().ToList();

        Assert.Equal(chaves, ConversaoCompras.Embalagens.Keys.Order());
        Assert.Equal(chaves, Csv.Select(l => l.Chave).Distinct().Order());
        Assert.All(Csv.GroupBy(l => l.Chave), g => Assert.Single(g.Select(l => l.Grupo).Distinct()));
    }

    // Os quatro grupos da proposta (A 48, B 52 com o frango inteiro, C 12, D 4 = 116).
    [Fact]
    public void Grupos_Tem_A_Contagem_Da_Proposta()
    {
        var porGrupo = ConversaoCompras.Embalagens.Values.GroupBy(e => e.Grupo).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(48, porGrupo[GrupoEmbalagem.Embalagem]);
        Assert.Equal(51, porGrupo[GrupoEmbalagem.Granel]);
        Assert.Equal(1, porGrupo[GrupoEmbalagem.Peca]);
        Assert.Equal(12, porGrupo[GrupoEmbalagem.Contavel]);
        Assert.Equal(4, porGrupo[GrupoEmbalagem.Pronto]);
    }

    [Fact]
    public void Contaveis_Sao_Os_Itens_Em_Unidades_E_Prontos_Sao_Os_Prontos()
    {
        foreach (var item in ConversaoCompras.Itens)
        {
            var grupo = ConversaoCompras.Embalagens[item.Chave].Grupo;
            Assert.Equal(item.Pronto, grupo == GrupoEmbalagem.Pronto);
            if (grupo == GrupoEmbalagem.Contavel)
            {
                Assert.Equal(UnidadeCompra.Unidades, item.Unidade);
            }
        }
    }

    // ---------- Código igual ao CSV ----------

    [Fact]
    public void Grupo_E_Tamanhos_Do_Codigo_Sao_Os_Do_Csv_Na_Mesma_Ordem()
    {
        foreach (var (chave, doItem) in ConversaoCompras.Embalagens)
        {
            var linhas = Csv.Where(l => l.Chave == chave).ToList();
            var item = ConversaoCompras.Itens.Single(i => i.Chave == chave);

            Assert.Equal(Grupo(doItem.Grupo), linhas[0].Grupo);

            var doCsv = linhas.Where(l => l.Tamanho is not null).ToList();
            Assert.Equal(doCsv.Count, doItem.Tamanhos.Count);
            if (doCsv.Count == 0)
            {
                // Item sem tamanho: uma linha só, com o motivo.
                Assert.Single(linhas);
                Assert.False(string.IsNullOrWhiteSpace(linhas[0].Motivo), chave);
                continue;
            }

            foreach (var (t, l) in doItem.Tamanhos.Zip(doCsv))
            {
                Assert.Equal(l.Tamanho, t.Quantidade);
                Assert.Equal(Sigla(item.Unidade), l.Unidade);
                Assert.Equal(l.Rotulo, t.Rotulo);
                Assert.Equal(l.Singular, t.Singular);
                Assert.Equal(l.Plural, t.Plural);
                Assert.Equal(l.TipoFonte, Fonte(t.Fonte));
                Assert.Equal(l.Amostra, t.Amostra);
                Assert.Equal(l.DoTamanho, t.DoTamanho);
                Assert.False(string.IsNullOrWhiteSpace(l.Referencia), chave);
                Assert.False(string.IsNullOrWhiteSpace(l.Motivo), chave);
            }
        }
    }

    [Fact]
    public void Tamanhos_Sao_Positivos_E_Do_Menor_Para_O_Maior()
    {
        Assert.All(ConversaoCompras.Embalagens.Values, e =>
        {
            Assert.All(e.Tamanhos, t => Assert.True(t.Quantidade > 0m));
            Assert.Equal(e.Tamanhos.Select(t => t.Quantidade).Order(), e.Tamanhos.Select(t => t.Quantidade));
            Assert.Equal(e.Tamanhos.Count, e.Tamanhos.Select(t => t.Quantidade).Distinct().Count());
        });
    }

    [Fact]
    public void Granel_Contavel_E_Pronto_Nao_Tem_Tamanho_E_A_Peca_Tem_Um()
    {
        Assert.All(ConversaoCompras.Embalagens.Values.Where(e => e.Grupo is GrupoEmbalagem.Granel or GrupoEmbalagem.Contavel or GrupoEmbalagem.Pronto),
            e => Assert.Empty(e.Tamanhos));
        Assert.All(ConversaoCompras.Embalagens.Values.Where(e => e.Grupo == GrupoEmbalagem.Peca), e => Assert.Single(e.Tamanhos));
    }

    // ---------- A regra das fontes ----------

    // Open Food Facts: pelo menos 10 embalagens legíveis na amostra e 15% delas no tamanho registrado.
    [Fact]
    public void Tamanho_Do_Open_Food_Facts_Passa_Na_Regra_Da_Amostra()
    {
        var off = ConversaoCompras.Embalagens.Values.SelectMany(e => e.Tamanhos).Where(t => t.Fonte == FonteEmbalagem.OpenFoodFacts).ToList();

        Assert.NotEmpty(off);
        Assert.All(off, t =>
        {
            Assert.NotNull(t.Amostra);
            Assert.NotNull(t.DoTamanho);
            Assert.True(t.Amostra >= ConversaoCompras.AmostraMinimaOff);
            Assert.True((decimal)t.DoTamanho!.Value / t.Amostra!.Value >= ConversaoCompras.FracaoMinimaOff);
        });
    }

    [Fact]
    public void Convencao_E_So_A_Duzia_E_Calibracao_So_A_Peca_Do_Frango()
    {
        var convencao = ConversaoCompras.Embalagens.Where(e => e.Value.Tamanhos.Any(t => t.Fonte == FonteEmbalagem.Convencao)).ToList();
        var calibracao = ConversaoCompras.Embalagens.Where(e => e.Value.Tamanhos.Any(t => t.Fonte == FonteEmbalagem.Calibracao)).ToList();

        Assert.Equal("ovos", Assert.Single(convencao).Key);
        Assert.Equal(12m, Assert.Single(convencao[0].Value.Tamanhos).Quantidade);
        Assert.Equal("frango-inteiro", Assert.Single(calibracao).Key);
        Assert.Equal(GrupoEmbalagem.Peca, calibracao[0].Value.Grupo);
    }

    // O pacote de pão de forma é vendido em gramas; as fatias saem da mesma fatia da POF que a lista usa.
    [Fact]
    public void Fatias_Do_Pao_De_Forma_Sao_Os_Gramas_Do_Pacote_Pela_Fatia_Da_Pof()
    {
        var fatia = ConversaoCompras.PorAlimento["pao-de-forma-integral"].Passos.OfType<PassoUnidade>().Single().Gramas;

        Assert.Equal(25m, fatia);
        Assert.All(ConversaoCompras.Embalagens["pao-de-forma"].Tamanhos, t =>
        {
            var gramas = decimal.Parse(t.Rotulo.Replace(" g", ""), CultureInfo.InvariantCulture);
            Assert.Equal(gramas / fatia, t.Quantidade);
        });
        Assert.Equal([16m, 20m], ConversaoCompras.Embalagens["pao-de-forma"].Tamanhos.Select(t => t.Quantidade));
    }

    // Os itens que a decisão de 2026-11-16 manda deixar em gramas (também vendidos a granel) e os do R2 e R3 da
    // proposta (peso drenado, ml contra g, peso cru contra assado) ficam sem tamanho.
    [Theory]
    [InlineData("queijo-mucarela")]
    [InlineData("queijo-prato")]
    [InlineData("amendoim")]
    [InlineData("castanha-de-caju")]
    [InlineData("castanha-do-para")]
    [InlineData("atum")]
    [InlineData("milho-em-conserva")]
    [InlineData("azeite")]
    [InlineData("oleo-de-soja")]
    [InlineData("pao-de-queijo")]
    [InlineData("queijo-minas-frescal")]
    public void Itens_Fora_Da_V1_Ficam_Sem_Tamanho(string chave)
    {
        Assert.Equal(GrupoEmbalagem.Embalagem, ConversaoCompras.Embalagens[chave].Grupo);
        Assert.Empty(ConversaoCompras.Embalagens[chave].Tamanhos);
    }
}
