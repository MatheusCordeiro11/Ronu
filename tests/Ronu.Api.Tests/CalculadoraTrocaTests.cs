using Ronu.Api.Data;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;
using Ronu.Api.Services.Troca;

namespace Ronu.Api.Tests;

/// <summary>
/// Trocar alimento, etapa 1: substitutos calculados pela base (quantidade pelo nutriente principal, arredondamento
/// da fase 4, porção máxima da troca, margem de 5% do dia, lista fechada, "evitar", uso nos carboidratos e nas
/// gorduras, escopo da semana). A gravação na dieta, o desfazer e o voltar ao original ficam na etapa 2.
/// </summary>
public class CalculadoraTrocaTests
{
    private static AlimentoBase Base(string id) => BaseNutricional.Itens.Single(i => i.Id == id);

    private static AlimentoDto A(string id, decimal quantidade) =>
        new() { Nome = Base(id).Nome, AlimentoId = id, Quantidade = quantidade, Unidade = Base(id).Medida };

    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    // Um dia com as refeições dadas, calculado pela base. Sem meta, o dia fica exatamente na meta.
    private static DiaDietaDto Dia(string nome, decimal? meta, params List<AlimentoDto>[] refeicoes)
    {
        var dia = new DiaDietaDto
        {
            DiaSemana = nome,
            Refeicoes = refeicoes.Select((a, i) => new RefeicaoDto { Nome = $"R{i}", Alimentos = a, Macros = Zero() }).ToList(),
            TotalDoDia = Zero(),
            MetaCalculada = Zero()
        };
        CalculoPelaBase.Calcular([dia]);
        dia.MetaCalculada = new MacrosDto { Calorias = meta ?? dia.TotalDoDia.Calorias, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };
        return dia;
    }

    private static PreferenciaContextoDto Gosto(string alimento) => new() { Alimento = alimento, Tipo = "preferido" };
    private static PreferenciaContextoDto Evitar(string alimento) => new() { Alimento = alimento, Tipo = "evitar" };

    private static ListaAlimentos Lista(string? uf = null, params PreferenciaContextoDto[] prefs) => ListaAlimentosPessoa.Montar(uf, prefs);

    private static ResultadoOpcoesTroca Calcular(List<DiaDietaDto> dias, PosicaoItem posicao, ListaAlimentos? lista = null) =>
        CalculadoraTroca.Calcular(dias, posicao, lista ?? Lista());

    private static IEnumerable<OpcaoTroca> Disponiveis(OpcoesDoEscopo e) => e.DasPreferencias.Concat(e.Parecidos).Concat(e.VerMais);

    private static OpcaoTroca? Opcao(OpcoesDoEscopo e, string id) => Disponiveis(e).SingleOrDefault(o => o.AlimentoId == id);

    // Almoço do canvas: o dia em volta é grande o bastante para os 5% (2.851 kcal) não esconderem a opção.
    private static List<DiaDietaDto> AlmocoComFrango(decimal gramasFrango = 200, decimal? meta = null) =>
    [
        Dia("Segunda-feira", meta,
            [A("pao-frances", 100), A("ovo-de-galinha-cozido", 90), A("cafe-coado", 200)],
            [A("arroz-branco-cozido", 300), A("feijao-cozido", 150), A("peito-de-frango-sem-pele-grelhado", gramasFrango), A("alface-crua", 50), A("azeite-de-oliva-extra-virgem", 10)],
            [A("macarrao-cozido", 400), A("patinho-bovino-grelhado", 200), A("banana-prata", 225), A("leite-integral", 500)])
    ];

    private static readonly PosicaoItem Frango = new(0, 1, 2);
    private static readonly PosicaoItem Arroz = new(0, 1, 0);
    private static readonly PosicaoItem Pao = new(0, 0, 0);

    // Peito grelhado (TACO 410): 32 g de proteína em 100 g, 200 g = 64 g. Tilápia (USDA 175177): 26,2 g em 100 g,
    // 64 ÷ 0,262 = 244,3 g, que vira 245 g (múltiplo de 5 a partir de 50 g), com 64,19 g de proteína e 313,6 kcal.
    [Fact]
    public void Substituto_Mantem_A_Proteina_Em_Gramas_Legiveis()
    {
        var tilapia = Opcao(Calcular(AlmocoComFrango(), Frango).Refeicao, "file-de-tilapia-grelhado")!;

        var o = Assert.Single(tilapia.Ocorrencias);
        Assert.Equal(245m, o.Quantidade);
        Assert.Null(o.Contagem);
        Assert.Equal(64m, o.NutrienteAntesG);
        Assert.Equal(64.19m, o.NutrienteG);
        Assert.Equal(313.6m, o.Kcal);
        Assert.Equal(-4.4m, o.DifKcal);
    }

    // Arroz 300 g = 84,3 g de carboidrato; pão francês (58,6 g em 100 g, unidade de 50 g da POF) = 143,9 g,
    // 2,88 unidades: 3 unidades, 150 g, com o nome rotulado como na geração.
    [Fact]
    public void Contavel_Vai_Para_Unidade_Inteira_Com_A_Contagem_No_Nome()
    {
        var pao = Opcao(Calcular(AlmocoComFrango(), Arroz).Refeicao, "pao-frances")!;

        var o = Assert.Single(pao.Ocorrencias);
        Assert.Equal((150m, 3), (o.Quantidade, o.Contagem));
        Assert.Equal("Pão francês (3 unidades)", o.Nome);
    }

    [Fact]
    public void Contavel_Fica_Com_Pelo_Menos_Uma_Unidade()
    {
        var q = CalculadoraTroca.Quantidade(Base("arroz-branco-cozido"), 20, Base("pao-frances"))!.Value;

        Assert.Equal((50m, 1), (q.Quantidade, q.Contagem));
    }

    // Abaixo de 50 g, passo de 1 g: 15 g de pasta de amendoim (51,4 g de gordura em 100 g) pedem
    // 15 × 51,4 ÷ 63,5 = 12,14 g de castanha-do-pará.
    [Fact]
    public void Abaixo_De_50g_Arredonda_Para_O_Grama()
    {
        var q = CalculadoraTroca.Quantidade(Base("pasta-de-amendoim-integral"), 15, Base("castanha-do-para"))!.Value;

        Assert.Equal(12m, q.Quantidade);
    }

    // Hortaliça não tem nutriente a manter: fica o peso, até a porção máxima do substituto (rúcula, 100 g).
    [Fact]
    public void Item_Livre_Mantem_O_Peso_Limitado_A_Porcao_Maxima()
    {
        Assert.Equal(80m, CalculadoraTroca.Quantidade(Base("alface-crua"), 80, Base("rucula-crua"))!.Value.Quantidade);
        Assert.Equal(100m, CalculadoraTroca.Quantidade(Base("abobrinha-cozida"), 150, Base("rucula-crua"))!.Value.Quantidade);
        // Contável sem nutriente: tomate (unidade de 100 g) no lugar de 150 g de pepino = 2 unidades.
        Assert.Equal((200m, 2), CalculadoraTroca.Quantidade(Base("pepino-cru"), 150, Base("tomate-cru"))!.Value);
        // Café coado por chá: o mesmo volume.
        Assert.Equal(200m, CalculadoraTroca.Quantidade(Base("cafe-coado"), 200, Base("cha-mate"))!.Value.Quantidade);
    }

    [Fact]
    public void Item_Livre_Nao_Mostra_Nutriente()
    {
        var o = Disponiveis(Calcular(AlmocoComFrango(), new PosicaoItem(0, 1, 3)).Refeicao).First().Ocorrencias[0];

        Assert.Null(o.NutrienteAntesG);
        Assert.Null(o.NutrienteG);
    }

    // Atum (USDA 171986, 25,5 g de proteína): 64 ÷ 0,255 = 251 g, acima da porção máxima de 200 g.
    [Fact]
    public void Acima_Da_Porcao_Maxima_Nao_Aparece()
    {
        var r = Calcular(AlmocoComFrango(), Frango).Refeicao;

        Assert.Null(Opcao(r, "atum-em-conserva-em-agua"));
        Assert.True(r.Fora[MotivoForaDaTroca.PorcaoMaxima] >= 1);
    }

    // Batata-doce no lugar de 300 g de arroz: 460 g, acima da PorcaoMaxima (400) e dentro da PorcaoMaximaTroca (500).
    [Fact]
    public void A_Troca_Usa_A_Porcao_Maxima_Da_Troca()
    {
        var batata = Opcao(Calcular(AlmocoComFrango(), Arroz).Refeicao, "batata-doce-cozida")!;

        Assert.Equal(460m, batata.Ocorrencias[0].Quantidade);
        Assert.Equal(400m, Base("batata-doce-cozida").PorcaoMaxima);
        Assert.Equal(500m, Base("batata-doce-cozida").PorcaoMaximaTroca);
    }

    [Fact]
    public void Porcao_Maxima_Da_Troca_Nunca_E_Menor_Que_A_Da_Geracao()
    {
        Assert.All(BaseNutricional.Itens, i => Assert.True(i.PorcaoMaximaTroca >= i.PorcaoMaxima, i.Id));
    }

    // Uso marcado em todo carboidrato e toda gordura de preparo ou de pão, e só neles.
    [Fact]
    public void Uso_Na_Troca_So_Nos_Carboidratos_E_Nas_Gorduras()
    {
        foreach (var item in BaseNutricional.Itens)
        {
            var precisa = item.Grupo == GrupoDeTroca.Carboidratos || item.Grupo == GrupoDeTroca.OleosEGorduras;
            Assert.True(precisa == item.Uso is not null, item.Id);
        }
    }

    // Bisteca (+298 kcal) num dia de 2.851 kcal na meta passa de 5%; a tilápia (−4) fica.
    [Fact]
    public void Opcao_Que_Deixa_O_Dia_Mais_De_5_Por_Cento_Fora_Nao_Aparece()
    {
        var r = Calcular(AlmocoComFrango(), Frango).Refeicao;

        Assert.Null(Opcao(r, "bisteca-suina-grelhada"));
        Assert.NotNull(Opcao(r, "file-de-tilapia-grelhado"));
        Assert.True(r.Fora[MotivoForaDaTroca.ForaDaMeta] >= 1);
        Assert.All(Disponiveis(r), o => Assert.True(Math.Abs(o.Dias[0].DesvioPct) <= 5, o.AlimentoId));
    }

    // Dia já 8% acima da meta: a troca que baixa as kcal passa mesmo fora dos 5%; a que sobe, não.
    [Fact]
    public void Dia_Ja_Fora_Aceita_So_A_Troca_Que_Nao_Piora()
    {
        var total = AlmocoComFrango()[0].TotalDoDia.Calorias;
        var r = Calcular(AlmocoComFrango(meta: total / 1.08m), Frango).Refeicao;

        Assert.NotNull(Opcao(r, "file-de-tilapia-grelhado"));
        Assert.Null(Opcao(r, "lombo-suino-assado"));
    }

    [Theory]
    [InlineData(100, 105, 100, true)]
    [InlineData(100, 106, 100, false)]
    [InlineData(110, 108, 100, true)]
    [InlineData(110, 111, 100, false)]
    [InlineData(90, 112, 100, false)]
    public void Margem_Do_Dia(decimal antes, decimal depois, decimal meta, bool passa)
    {
        Assert.Equal(passa, CalculadoraTroca.PassaNaMeta(antes, depois, meta));
    }

    [Fact]
    public void Evitar_Tira_O_Item_E_E_Contado()
    {
        var r = Calcular(AlmocoComFrango(), Frango, Lista(null, Evitar("peixe")));

        Assert.DoesNotContain(Disponiveis(r.Refeicao), o => Base(o.AlimentoId).Categoria == CategoriaAlimento.Peixes);
        Assert.True(r.ForaPeloEvitar >= 6);
    }

    [Fact]
    public void Gosto_Vai_Para_As_Preferencias()
    {
        var r = Calcular(AlmocoComFrango(), Frango, Lista(null, Gosto("tilápia"))).Refeicao;

        Assert.Equal(["file-de-tilapia-grelhado"], r.DasPreferencias.Select(o => o.AlimentoId));
        Assert.DoesNotContain(r.Parecidos.Concat(r.VerMais), o => o.AlimentoId == "file-de-tilapia-grelhado");
    }

    [Fact]
    public void Ate_Tres_Parecidos_E_O_Resto_No_Ver_Mais_Pelas_Kcal()
    {
        var r = Calcular(AlmocoComFrango(), Frango).Refeicao;

        Assert.Equal(3, r.Parecidos.Count);
        Assert.NotEmpty(r.VerMais);
        var todos = r.Parecidos.Concat(r.VerMais).Select(o => Math.Abs(o.DifKcal)).ToList();
        Assert.Equal(todos.OrderBy(x => x), todos);
    }

    // Fora da lista fechada: abobrinha refogada e ovo frito (o óleo já está no valor da TACO) e a cerveja.
    [Fact]
    public void So_Oferece_O_Que_Esta_Na_Lista_Fechada()
    {
        var dias = new List<DiaDietaDto> { Dia("Segunda-feira", 3000, [A("ovo-de-galinha-cozido", 90), A("abobrinha-cozida", 100)]) };

        Assert.DoesNotContain(Disponiveis(Calcular(dias, new PosicaoItem(0, 0, 0)).Refeicao), o => o.AlimentoId == "ovo-de-galinha-frito");
        Assert.DoesNotContain(Disponiveis(Calcular(dias, new PosicaoItem(0, 0, 1)).Refeicao), o => o.AlimentoId == "abobrinha-refogada");
    }

    // O arroz não aparece no lugar do macarrão numa refeição que já tem arroz.
    [Fact]
    public void Item_Ja_Na_Refeicao_Nao_Aparece()
    {
        var dias = new List<DiaDietaDto> { Dia("Segunda-feira", null, [A("macarrao-cozido", 200), A("arroz-branco-cozido", 100), A("patinho-bovino-grelhado", 150)]) };

        var r = Calcular(dias, new PosicaoItem(0, 0, 0)).Refeicao;

        Assert.Null(Opcao(r, "arroz-branco-cozido"));
        Assert.Equal(1, r.Fora[MotivoForaDaTroca.JaNaRefeicao]);
    }

    // A TACO já conta o óleo do refogado: couve refogada não entra numa refeição com azeite.
    [Fact]
    public void Refogado_Nao_Entra_Em_Refeicao_Com_Azeite()
    {
        var r = Calcular(AlmocoComFrango(), new PosicaoItem(0, 1, 3)).Refeicao;

        Assert.Null(Opcao(r, "couve-refogada"));
        Assert.Null(Opcao(r, "espinafre-refogado"));
        Assert.Equal(2, r.Fora[MotivoForaDaTroca.RefogadoComOleo]);
    }

    // Gorduras: azeite e óleo vão no preparo, manteiga e requeijão no pão; um não substitui o outro.
    [Fact]
    public void Gordura_Troca_So_Pelo_Mesmo_Uso()
    {
        var r = Calcular(AlmocoComFrango(), new PosicaoItem(0, 1, 4)).Refeicao;

        Assert.Equal(["oleo-de-soja"], Disponiveis(r).Select(o => o.AlimentoId));
        Assert.Equal(2, r.Fora[MotivoForaDaTroca.UsoDiferente]);
    }

    // Carboidratos: o uso só ordena. No lugar do pão do café, os de café vêm antes do macarrão, que é o que menos
    // muda as kcal (+0,2), e o macarrão continua no "ver mais".
    [Fact]
    public void Carboidrato_Do_Mesmo_Uso_Vem_Primeiro_E_O_Outro_Fica_No_Ver_Mais()
    {
        var r = Calcular(AlmocoComFrango(), Pao).Refeicao;

        Assert.All(r.Parecidos, o => Assert.True(o.MesmoUso, o.AlimentoId));
        Assert.Contains(r.VerMais, o => o.AlimentoId == "macarrao-cozido");
        Assert.Equal("pao-de-forma-integral", r.Parecidos[0].AlimentoId);
    }

    [Fact]
    public void Nome_Regional_Pela_Uf()
    {
        var r = Calcular(AlmocoComFrango(), Arroz, Lista("RJ")).Refeicao;

        Assert.Equal("Aipim cozido", Opcao(r, "mandioca-cozida")!.Nome);
    }

    // Semana: cada ocorrência recalculada pela própria quantidade (200 g → 245 g; 100 g → 122,1 g → 120 g), com a
    // prévia de cada dia afetado.
    [Fact]
    public void Semana_Recalcula_Cada_Ocorrencia()
    {
        var dias = AlmocoComFrango().Concat([Dia("Terça-feira", null, [A("arroz-branco-cozido", 300), A("peito-de-frango-sem-pele-grelhado", 100)])]).ToList();

        var r = Calcular(dias, Frango);
        var tilapia = Opcao(r.Semana, "file-de-tilapia-grelhado")!;

        Assert.Equal(2, r.OcorrenciasNaSemana);
        Assert.Equal([245m, 120m], tilapia.Ocorrencias.Select(o => o.Quantidade));
        Assert.Equal(["Segunda-feira", "Terça-feira"], tilapia.Dias.Select(d => d.DiaSemana));
        Assert.Single(Opcao(r.Refeicao, "file-de-tilapia-grelhado")!.Ocorrencias);
    }

    // Na terça, 300 g de frango pediriam 366 g de tilápia (porção 300): na semana a tilápia sai, na refeição fica.
    [Fact]
    public void Semana_Tira_A_Opcao_Se_Alguma_Ocorrencia_Nao_Cabe()
    {
        var dias = AlmocoComFrango().Concat([Dia("Terça-feira", null, [A("arroz-branco-cozido", 300), A("peito-de-frango-sem-pele-grelhado", 300)])]).ToList();

        var r = Calcular(dias, Frango);

        Assert.NotNull(Opcao(r.Refeicao, "file-de-tilapia-grelhado"));
        Assert.Null(Opcao(r.Semana, "file-de-tilapia-grelhado"));
    }

    // Na semana, os 5% valem para cada dia: num dia pequeno, a mesma troca pesa mais.
    [Fact]
    public void Semana_Confere_Os_5_Por_Cento_Em_Cada_Dia()
    {
        var dias = AlmocoComFrango().Concat([Dia("Terça-feira", null, [A("peito-de-frango-sem-pele-grelhado", 200)])]).ToList();

        var r = Calcular(dias, Frango);

        // Lombo: +60 kcal. Segunda (2.851) fica a +2%; a terça, só com o frango (318 kcal), a +19%.
        Assert.NotNull(Opcao(r.Refeicao, "lombo-suino-assado"));
        Assert.Null(Opcao(r.Semana, "lombo-suino-assado"));
    }

    [Fact]
    public void Grupo_Sem_Outro_Item_Nao_Tem_Opcao()
    {
        var dias = new List<DiaDietaDto> { Dia("Segunda-feira", null, [A("cerveja-pilsen", 355)]) };

        var r = Calcular(dias, new PosicaoItem(0, 0, 0), Lista(null, Gosto("cerveja")));

        Assert.True(r.Refeicao.Nenhuma);
        Assert.True(r.Semana.Nenhuma);
    }

    [Fact]
    public void Dieta_Antiga_Nao_Tem_Troca()
    {
        var antiga = new DietaSemanalDto
        {
            Dias = [Dia("Segunda-feira", null, [A("arroz-branco-cozido", 100)])],
            VersaoBase = null
        };
        antiga.Dias[0].Refeicoes[0].Alimentos[0].AlimentoId = null;

        Assert.Equal(MotivoSemTroca.DietaAnterior, CalculadoraTroca.MotivoSemTroca(antiga));
    }

    [Fact]
    public void Dieta_De_Outra_Versao_Da_Base_Nao_Tem_Troca()
    {
        var dieta = new DietaSemanalDto { Dias = AlmocoComFrango(), VersaoBase = BaseNutricional.VersaoBase - 1 };

        Assert.Equal(MotivoSemTroca.OutraVersaoDaBase, CalculadoraTroca.MotivoSemTroca(dieta));

        dieta.VersaoBase = BaseNutricional.VersaoBase;
        Assert.Null(CalculadoraTroca.MotivoSemTroca(dieta));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(0, 3, 0)]
    [InlineData(0, 1, 5)]
    public void Posicao_Fora_Da_Dieta_Nao_Encontra_Item(int dia, int refeicao, int item)
    {
        Assert.Null(CalculadoraTroca.Encontrar(AlmocoComFrango(), new PosicaoItem(dia, refeicao, item)));
    }
}
