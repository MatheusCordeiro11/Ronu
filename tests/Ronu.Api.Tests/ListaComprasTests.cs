using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.ListaCompras;

namespace Ronu.Api.Tests;

/// <summary>
/// Lista de compras, etapa 2: a soma da semana, a conversão, o arredondamento para cima, o nome
/// regional, a dieta antiga e o GET /api/dietas/atual/lista-compras. Só contas e perfis fictícios.
/// </summary>
public class ListaComprasTests
{
    // ---------- Apoio ----------

    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    private static AlimentoDto Alimento(string? id, decimal quantidade)
    {
        var item = id is null ? null : BaseNutricional.Itens.Single(i => i.Id == id);
        return new AlimentoDto { Nome = item?.Nome ?? "Bolo caseiro", Quantidade = quantidade, Unidade = item?.Medida ?? "g", AlimentoId = id };
    }

    // Cada dia com uma refeição só: o montador não olha o dia nem a refeição, só soma.
    private static DietaSemanalDto Dieta(params AlimentoDto[][] dias) => new()
    {
        Dias = dias.Select((alimentos, i) => new DiaDietaDto
        {
            DiaSemana = $"Dia {i + 1}",
            Refeicoes = [new RefeicaoDto { Nome = "Almoço", Alimentos = alimentos.ToList(), Macros = Zero() }],
            TotalDoDia = Zero(),
            MetaCalculada = Zero()
        }).ToList()
    };

    private static AlimentoDto[][] SeteDias(params AlimentoDto[] alimentosDoDia) =>
        Enumerable.Range(0, 7).Select(_ => alimentosDoDia).ToArray();

    private static ItemListaComprasDto Item(ListaComprasMontada lista, string chave) =>
        lista.Secoes.SelectMany(s => s.Itens).Single(i => i.Chave == chave);

    // ---------- Soma e conversão ----------

    [Fact]
    public void Arroz_Da_Semana_Vira_Arroz_Cru_Arredondado_Para_Cima_Em_50_G()
    {
        var lista = MontadorListaCompras.Montar(Dieta(SeteDias(Alimento("arroz-branco-cozido", 200))), "SP");

        var arroz = Item(lista, "arroz-branco");
        // 1.400 g cozido × 0,356599 = 499,24 g cru.
        Assert.Equal(499.24m, arroz.Detalhe.QuantidadeExata);
        Assert.Equal(500m, arroz.Quantidade);
        Assert.Equal("g", arroz.Unidade);
        Assert.Equal("500 g", arroz.Texto);
        Assert.Equal("Arroz branco tipo 1", arroz.Nome);
    }

    [Fact]
    public void Bananas_Contam_Pela_Unidade_Da_Pof_Sem_Pegar_Uma_A_Mais_Por_Erro_De_Divisao()
    {
        // 525 g = 7 × 75 g. Em decimal, 525 × (1 ÷ 75) dá 6,99…98: tem de sair 7, nem 6 nem 8.
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("banana-prata", 450)], [Alimento("banana-prata", 75)]), "SP");

        var banana = Item(lista, "banana-prata");
        Assert.Equal(7m, banana.Quantidade);
        Assert.Equal("unidades", banana.Unidade);
        Assert.Equal("7 bananas", banana.Texto);
    }

    [Fact]
    public void Uma_Unidade_Fica_No_Singular()
    {
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("banana-prata", 75)]), "SP");

        Assert.Equal("1 banana", Item(lista, "banana-prata").Texto);
    }

    [Fact]
    public void Ovo_Cozido_Frito_E_Clara_Somam_Em_Ovos()
    {
        // 2 cozidos (90 g) + 1 frito (50 g) + 100 g de clara (3,03 ovos) = 6,03 → 7 ovos.
        var lista = MontadorListaCompras.Montar(Dieta([
            Alimento("ovo-de-galinha-cozido", 90), Alimento("ovo-de-galinha-frito", 50), Alimento("clara-de-ovo-cozida", 100)]), "SP");

        var ovos = Item(lista, "ovos");
        Assert.Equal(6.03m, ovos.Detalhe.QuantidadeExata);
        Assert.Equal("7 ovos", ovos.Texto);
        Assert.Equal(3, ovos.Detalhe.Origens.Count);
        Assert.Contains("Sobram as gemas.", ovos.Detalhe.Observacoes);
    }

    [Fact]
    public void Peito_Grelhado_E_Cozido_Viram_Uma_Linha_So()
    {
        var lista = MontadorListaCompras.Montar(Dieta([
            Alimento("peito-de-frango-sem-pele-grelhado", 150), Alimento("peito-de-frango-sem-pele-cozido", 150)]), "SP");

        var peito = Item(lista, "peito-de-frango");
        // 150 × 1,488372 + 150 × 1,465116 = 443,02 g.
        Assert.Equal(443.02m, peito.Detalhe.QuantidadeExata);
        Assert.Equal("450 g", peito.Texto);
        Assert.Equal(2, peito.Detalhe.Origens.Count);
    }

    [Fact]
    public void Cenoura_Crua_E_Cozida_Somam_Em_Cenouras()
    {
        // 120 g crua = 1 cenoura; 300 g cozida × 0,838384 = 251,5 g crua = 2,1 cenouras. Total 3,1 → 4.
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("cenoura-crua", 120), Alimento("cenoura-cozida", 300)]), "SP");

        Assert.Equal("4 cenouras", Item(lista, "cenoura").Texto);
    }

    [Fact]
    public void Chuchu_Usa_O_Teto_De_1_E_O_Detalhe_Explica()
    {
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("chuchu-cozido", 300)]), "SP");

        var chuchu = Item(lista, "chuchu");
        Assert.Equal("300 g", chuchu.Texto);
        var passo = chuchu.Detalhe.Origens.Single().Passos.Single();
        Assert.Equal(1m, passo.Fator);
        Assert.EndsWith("a lista usa 1 (calibração do projeto).", passo.Descricao);
        Assert.StartsWith("Calibração do projeto", chuchu.Detalhe.Observacoes.Single());
    }

    [Fact]
    public void Detalhe_Traz_A_Conta_Com_Codigo_E_Pagina_Da_Fonte()
    {
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("frango-sem-pele-assado", 100)]), "SP");

        var origem = Item(lista, "frango-inteiro").Detalhe.Origens.Single();
        Assert.Equal(100m, origem.QuantidadeNaDieta);
        Assert.Equal("g", origem.Medida);
        Assert.Equal(283.17m, origem.Convertido);
        Assert.Equal(new[] { "par-cru-cozido", "descarte" }, origem.Passos.Select(p => p.Tipo));
        Assert.Equal(
            "Cozido para cru pela proteína: TACO 403 (Frango, inteiro, sem pele, assado, p. 53): 28 ÷ "
            + "TACO 405 (Frango, inteiro, sem pele, cru, p. 55): 20,6 = 1,3592.",
            origem.Passos[0].Descricao);
        Assert.Contains("05011", origem.Passos[1].Descricao);
        Assert.Contains("52%", origem.Passos[1].Descricao);
    }

    [Fact]
    public void Cha_Conta_Em_Saches_Pela_Receita_Da_Taco()
    {
        // 400 ml por dia × 7 = 2.800 ml; 1 sachê para cada 200 ml = 14 sachês.
        var lista = MontadorListaCompras.Montar(Dieta(SeteDias(Alimento("cha-mate", 400))), "SP");

        var cha = Item(lista, "cha-mate");
        Assert.Equal("14 sachês", cha.Texto);
        Assert.Contains("p. 121", cha.Detalhe.Origens.Single().Passos.Single().Descricao);
    }

    // ---------- Arredondamento e texto ----------

    [Theory]
    [InlineData(532, 550)]
    [InlineData(500, 500)]
    [InlineData(100.01, 150)]
    [InlineData(100, 100)]
    [InlineData(99.2, 100)]
    [InlineData(73, 80)]
    [InlineData(50.03, 60)]   // o salmão do teste de 2026-10-26: com passos de 50 virava 100 g
    [InlineData(50, 50)]
    [InlineData(49.2, 50)]
    [InlineData(23, 30)]
    [InlineData(10, 10)]
    [InlineData(0.4, 10)]
    [InlineData(1050.01, 1100)]
    public void Gramas_Vao_Para_Cima_Em_10_Ate_100_E_Em_50_Acima_Nunca_Zero(double exato, int esperado)
    {
        Assert.Equal(esperado, MontadorListaCompras.Arredondar((decimal)exato, UnidadeCompra.Gramas));
        Assert.Equal(esperado, MontadorListaCompras.Arredondar((decimal)exato, UnidadeCompra.Mililitros));
    }

    [Theory]
    [InlineData(6.03, 7)]
    [InlineData(7, 7)]
    [InlineData(0.2, 1)]
    [InlineData(6.9999999999, 7)]
    public void Contaveis_Vao_Para_A_Proxima_Unidade_Inteira(double exato, int esperado)
    {
        Assert.Equal(esperado, MontadorListaCompras.Arredondar((decimal)exato, UnidadeCompra.Unidades));
    }

    [Theory]
    [InlineData("arroz-branco", 550, "550 g")]
    [InlineData("arroz-branco", 1000, "1 kg")]
    [InlineData("arroz-branco", 1250, "1,25 kg")]
    [InlineData("leite-integral", 350, "350 ml")]
    [InlineData("leite-integral", 1050, "1,05 L")]
    [InlineData("pao-de-forma", 6, "6 fatias")]
    [InlineData("pao-de-queijo", 2, "2 pães de queijo")]
    [InlineData("pao-frances", 1, "1 pão")]
    public void Texto_Da_Quantidade(string chave, int quantidade, string esperado)
    {
        Assert.Equal(esperado, MontadorListaCompras.Texto(quantidade, ConversaoCompras.Itens.Single(i => i.Chave == chave)));
    }

    // ---------- Nome regional, pronto, notas, seções ----------

    [Theory]
    [InlineData("RJ", "Aipim")]
    [InlineData("pe", "Macaxeira")]
    [InlineData("SP", "Mandioca")]
    [InlineData("", "Mandioca")]
    [InlineData(null, "Mandioca")]
    public void Mandioca_Tem_O_Nome_Da_Uf(string? uf, string esperado)
    {
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("mandioca-cozida", 200)]), uf);

        Assert.Equal(esperado, Item(lista, "mandioca").Nome);
    }

    [Fact]
    public void Suco_E_Refogado_Vem_Prontos_Sem_Conversao()
    {
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("suco-de-laranja-natural", 300), Alimento("couve-refogada", 120)]), "SP");

        var suco = Item(lista, "suco-de-laranja");
        Assert.True(suco.Pronto);
        Assert.Equal("300 ml", suco.Texto);
        Assert.Empty(suco.Detalhe.Origens.Single().Passos);
        Assert.True(Item(lista, "couve-refogada").Pronto);
    }

    [Fact]
    public void Aviso_Do_Oleo_So_Com_Refogado_Ou_Ovo_Frito()
    {
        Assert.True(MontadorListaCompras.Montar(Dieta([Alimento("couve-refogada", 100)]), "SP").AvisoOleo);
        Assert.True(MontadorListaCompras.Montar(Dieta([Alimento("ovo-de-galinha-frito", 50)]), "SP").AvisoOleo);
        Assert.False(MontadorListaCompras.Montar(Dieta([Alimento("ovo-de-galinha-cozido", 45), Alimento("couve-crua", 50)]), "SP").AvisoOleo);
    }

    [Fact]
    public void Nota_Vai_Na_Propria_Linha()
    {
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("coxa-de-frango-sem-pele-cozida", 200), Alimento("bisteca-suina-grelhada", 150)]), "SP");

        Assert.Equal("de carne, sem osso e sem pele · compre um pouco mais", Item(lista, "coxa-de-frango").Nota);
        Assert.Equal("de carne, sem osso · compre um pouco mais", Item(lista, "bisteca").Nota);
    }

    [Fact]
    public void Secoes_Na_Ordem_Do_Mercado_Sem_Secao_Vazia()
    {
        var lista = MontadorListaCompras.Montar(Dieta([
            Alimento("acai-sem-acucar", 200), Alimento("arroz-branco-cozido", 200), Alimento("banana-prata", 75),
            Alimento("patinho-bovino-grelhado", 150), Alimento("cafe-coado", 200)]), "SP");

        Assert.Equal(new[] { "hortifruti", "acougue-e-peixaria", "mercearia", "congelados" }, lista.Secoes.Select(s => s.Chave));
        Assert.Equal(new[] { "Hortifrúti", "Açougue e peixaria", "Mercearia", "Congelados" }, lista.Secoes.Select(s => s.Nome));
        // Dentro da seção, a ordem do catálogo (arroz antes do café).
        Assert.Equal(new[] { "arroz-branco", "cafe-em-po" }, lista.Secoes.Single(s => s.Chave == "mercearia").Itens.Select(i => i.Chave));
    }

    // ---------- Dieta antiga ----------

    [Fact]
    public void Dieta_Com_Item_Sem_AlimentoId_Fica_Sem_Lista()
    {
        var lista = MontadorListaCompras.Montar(Dieta([Alimento("arroz-branco-cozido", 200), Alimento(null, 100)]), "SP");

        Assert.False(lista.Disponivel);
        Assert.Equal("dieta-antiga", lista.Motivo);
        Assert.Empty(lista.Secoes);
    }

    // ---------- GET /api/dietas/atual/lista-compras ----------

    private static ListaComprasController Controller(ApplicationDbContext banco, int usuarioId) => new(banco)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"))
            }
        }
    };

    private static Usuario NovoUsuario(ApplicationDbContext banco, string email, string uf)
    {
        var usuario = new Usuario { Nome = "Teste", Email = email, Estado = uf };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        return usuario;
    }

    private static DietaIA NovaDieta(ApplicationDbContext banco, Usuario usuario, DateTime quando, DietaSemanalDto dieta)
    {
        var dietaIA = new DietaIA { UsuarioId = usuario.Id, DataGeracao = quando, ConteudoJson = JsonSerializer.Serialize(dieta) };
        banco.DietasIA.Add(dietaIA);
        banco.SaveChanges();
        return dietaIA;
    }

    [Fact]
    public async Task Sem_Dieta_Da_404()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "lista1@exemplo.com", "SP");

        var resultado = await Controller(banco, usuario.Id).Obter();

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task Usa_A_Dieta_Mais_Recente_E_A_Uf_Do_Usuario()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "lista2@exemplo.com", "RJ");
        NovaDieta(banco, usuario, new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc), Dieta([Alimento("arroz-branco-cozido", 200)]));
        var atual = NovaDieta(banco, usuario, new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc), Dieta([Alimento("mandioca-cozida", 200)]));

        var resposta = (ListaComprasResponse)((OkObjectResult)await Controller(banco, usuario.Id).Obter()).Value!;

        Assert.Equal(atual.Id, resposta.DietaId);
        Assert.Equal(atual.DataGeracao, resposta.DataGeracao);
        Assert.Equal(ConversaoCompras.VersaoConversao, resposta.VersaoConversao);
        Assert.True(resposta.Disponivel);
        Assert.Equal("Aipim", resposta.Secoes.Single().Itens.Single().Nome);
    }

    // O usuário vem só do token: a dieta de outra pessoa nunca aparece.
    [Fact]
    public async Task Nao_Ve_A_Dieta_De_Outro_Usuario()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var dono = NovoUsuario(banco, "lista3@exemplo.com", "SP");
        var outro = NovoUsuario(banco, "lista4@exemplo.com", "SP");
        NovaDieta(banco, dono, DateTime.UtcNow, Dieta([Alimento("arroz-branco-cozido", 200)]));

        Assert.IsType<NotFoundObjectResult>(await Controller(banco, outro.Id).Obter());
    }

    [Fact]
    public async Task Dieta_Antiga_Da_200_Sem_Lista()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "lista5@exemplo.com", "SP");
        NovaDieta(banco, usuario, DateTime.UtcNow, Dieta([Alimento(null, 150)]));

        var resposta = (ListaComprasResponse)((OkObjectResult)await Controller(banco, usuario.Id).Obter()).Value!;

        Assert.False(resposta.Disponivel);
        Assert.Equal("dieta-antiga", resposta.Motivo);
        Assert.Empty(resposta.Secoes);
    }

    // A resposta vai para o "Compartilhar": nada de calorias, macros, meta ou objetivo.
    [Fact]
    public async Task Resposta_Nao_Leva_Dado_De_Saude()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "lista6@exemplo.com", "SP");
        NovaDieta(banco, usuario, DateTime.UtcNow, Dieta(SeteDias(
            Alimento("arroz-branco-cozido", 200), Alimento("patinho-bovino-grelhado", 150), Alimento("banana-prata", 75))));

        var resposta = ((OkObjectResult)await Controller(banco, usuario.Id).Obter()).Value!;
        var json = JsonSerializer.Serialize(resposta, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ToLowerInvariant();

        Assert.DoesNotContain("calorias", json);
        Assert.DoesNotContain("kcal", json);
        Assert.DoesNotContain("macros", json);
        Assert.DoesNotContain("meta", json);
        Assert.DoesNotContain("objetivo", json);
    }
}
