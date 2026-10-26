using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// Lista de compras, etapa 3: o "já tenho" salvo na conta (tabela ListaComprasMarcacoes), o PUT
/// /api/dietas/atual/lista-compras/itens/{chave} e o campo tenho do GET. Só contas fictícias.
/// </summary>
public class ListaComprasMarcacoesTests
{
    // ---------- Apoio ----------

    private static MacrosDto Zero() => new() { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 };

    private static AlimentoDto Alimento(string? id, decimal quantidade)
    {
        var item = id is null ? null : BaseNutricional.Itens.Single(i => i.Id == id);
        return new AlimentoDto { Nome = item?.Nome ?? "Bolo caseiro", Quantidade = quantidade, Unidade = item?.Medida ?? "g", AlimentoId = id };
    }

    // Os 7 dias com os nomes que o RepositorioDietaIA espera, cada um com os mesmos alimentos.
    private static DietaSemanalDto Dieta(params AlimentoDto[] alimentos) => new()
    {
        Dias = GeradorDietaGemini.NomesDias.Select(nome => new DiaDietaDto
        {
            DiaSemana = nome,
            Refeicoes = [new RefeicaoDto { Nome = "Almoço", Alimentos = alimentos.ToList(), Macros = Zero() }],
            TotalDoDia = Zero(),
            MetaCalculada = Zero()
        }).ToList()
    };

    private static DietaSemanalDto DietaPadrao() =>
        Dieta(Alimento("arroz-branco-cozido", 200), Alimento("banana-prata", 75), Alimento("ovo-de-galinha-cozido", 90));

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

    private static Usuario NovoUsuario(ApplicationDbContext banco, string email)
    {
        var usuario = new Usuario { Nome = "Teste", Email = email, Estado = "SP" };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        return usuario;
    }

    private static DietaIA NovaDieta(ApplicationDbContext banco, Usuario usuario, DateTime quando, DietaSemanalDto? dieta = null)
    {
        var dietaIA = new DietaIA { UsuarioId = usuario.Id, DataGeracao = quando, ConteudoJson = JsonSerializer.Serialize(dieta ?? DietaPadrao()) };
        banco.DietasIA.Add(dietaIA);
        banco.SaveChanges();
        return dietaIA;
    }

    private static MarcacaoListaComprasRequest Pedido(int dietaId, bool tenho) => new() { DietaId = dietaId, Tenho = tenho };

    private static async Task<ListaComprasResponse> Lista(ApplicationDbContext banco, int usuarioId) =>
        (ListaComprasResponse)((OkObjectResult)await Controller(banco, usuarioId).Obter()).Value!;

    private static bool Tenho(ListaComprasResponse lista, string chave) =>
        lista.Secoes.SelectMany(s => s.Itens).Single(i => i.Chave == chave).Tenho;

    private static string? Mensagem(IActionResult resultado)
    {
        var valor = ((ObjectResult)resultado).Value!;
        return valor.GetType().GetProperty("mensagem")?.GetValue(valor) as string;
    }

    private static readonly DateTime Ontem = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Hoje = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Marcar e desmarcar ----------

    [Fact]
    public async Task Sem_Marcacao_O_Get_Vem_Com_Tenho_Falso()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca1@exemplo.com");
        NovaDieta(banco, usuario, Hoje);

        var lista = await Lista(banco, usuario.Id);

        Assert.All(lista.Secoes.SelectMany(s => s.Itens), i => Assert.False(i.Tenho));
    }

    [Fact]
    public async Task Marcar_E_Desmarcar_E_Idempotente()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca2@exemplo.com");
        var dieta = NovaDieta(banco, usuario, Hoje);
        var controller = Controller(banco, usuario.Id);

        Assert.IsType<NoContentResult>(await controller.Marcar("ovos", Pedido(dieta.Id, true)));
        Assert.IsType<NoContentResult>(await controller.Marcar("ovos", Pedido(dieta.Id, true)));
        var marcacao = Assert.Single(banco.ListaComprasMarcacoes);
        Assert.Equal((usuario.Id, dieta.Id, "ovos"), (marcacao.UsuarioId, marcacao.DietaIAId, marcacao.Chave));
        Assert.True(Tenho(await Lista(banco, usuario.Id), "ovos"));
        Assert.False(Tenho(await Lista(banco, usuario.Id), "arroz-branco"));

        Assert.IsType<NoContentResult>(await controller.Marcar("ovos", Pedido(dieta.Id, false)));
        Assert.IsType<NoContentResult>(await controller.Marcar("ovos", Pedido(dieta.Id, false)));
        Assert.Empty(banco.ListaComprasMarcacoes);
        Assert.False(Tenho(await Lista(banco, usuario.Id), "ovos"));
    }

    // ---------- Dieta que mudou ----------

    [Fact]
    public async Task DietaId_Que_Nao_E_O_Da_Dieta_Atual_Da_409_E_Nao_Grava()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca3@exemplo.com");
        var antiga = NovaDieta(banco, usuario, Ontem);
        NovaDieta(banco, usuario, Hoje);

        var resultado = await Controller(banco, usuario.Id).Marcar("ovos", Pedido(antiga.Id, true));

        Assert.Equal(StatusCodes.Status409Conflict, ((ObjectResult)resultado).StatusCode);
        Assert.Equal("Sua dieta mudou desde que a lista foi aberta. Atualize a lista e tente de novo.", Mensagem(resultado));
        Assert.Empty(banco.ListaComprasMarcacoes);
    }

    [Fact]
    public async Task Dieta_Nova_Comeca_Sem_Marcacoes()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca4@exemplo.com");
        var antiga = NovaDieta(banco, usuario, Ontem);
        await Controller(banco, usuario.Id).Marcar("ovos", Pedido(antiga.Id, true));

        NovaDieta(banco, usuario, Hoje);

        Assert.False(Tenho(await Lista(banco, usuario.Id), "ovos"));
    }

    // ---------- Chave ----------

    [Theory]
    [InlineData("Ovos")]
    [InlineData("ovos!")]
    [InlineData("../ovos")]
    [InlineData("ovos;drop")]
    [InlineData("")]
    public async Task Chave_Fora_Do_Formato_Da_400_Item_Invalido(string chave)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca5@exemplo.com");
        var dieta = NovaDieta(banco, usuario, Hoje);

        var resultado = await Controller(banco, usuario.Id).Marcar(chave, Pedido(dieta.Id, true));

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal("Item inválido.", Mensagem(resultado));
        Assert.Empty(banco.ListaComprasMarcacoes);
    }

    [Fact]
    public async Task Chave_De_65_Caracteres_Da_400()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca6@exemplo.com");
        var dieta = NovaDieta(banco, usuario, Hoje);

        var resultado = await Controller(banco, usuario.Id).Marcar(new string('a', 65), Pedido(dieta.Id, true));

        Assert.Equal("Item inválido.", Mensagem(resultado));
    }

    [Theory]
    [InlineData("feijao-preto")]      // existe no catálogo, mas não está nesta dieta
    [InlineData("item-que-nao-existe")]
    public async Task Chave_Fora_Da_Lista_Da_Dieta_Da_400(string chave)
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca7@exemplo.com");
        var dieta = NovaDieta(banco, usuario, Hoje);

        var resultado = await Controller(banco, usuario.Id).Marcar(chave, Pedido(dieta.Id, true));

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal("Este item não está na sua lista de compras.", Mensagem(resultado));
        Assert.Empty(banco.ListaComprasMarcacoes);
    }

    [Fact]
    public async Task Dieta_Antiga_Sem_Lista_Nao_Aceita_Marcacao()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca8@exemplo.com");
        var dieta = NovaDieta(banco, usuario, Hoje, Dieta(Alimento(null, 150)));

        var resultado = await Controller(banco, usuario.Id).Marcar("ovos", Pedido(dieta.Id, true));

        Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Empty(banco.ListaComprasMarcacoes);
    }

    [Fact]
    public async Task Sem_Dieta_Da_404()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var usuario = NovoUsuario(banco, "marca9@exemplo.com");

        Assert.IsType<NotFoundObjectResult>(await Controller(banco, usuario.Id).Marcar("ovos", Pedido(1, true)));
    }

    // ---------- Outra pessoa ----------

    // O usuário sai só do token: mandar o dietaId da dieta de outra pessoa não alcança essa dieta.
    [Fact]
    public async Task Ninguem_Marca_Na_Dieta_De_Outra_Pessoa()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var dona = NovoUsuario(banco, "marca10@exemplo.com");
        var intrusa = NovoUsuario(banco, "marca11@exemplo.com");
        var dietaDaDona = NovaDieta(banco, dona, Hoje);
        await Controller(banco, dona.Id).Marcar("ovos", Pedido(dietaDaDona.Id, true));

        // Sem dieta própria: 404. Com dieta própria: o id da outra não é a dieta atual dela, 409.
        Assert.IsType<NotFoundObjectResult>(await Controller(banco, intrusa.Id).Marcar("arroz-branco", Pedido(dietaDaDona.Id, true)));
        NovaDieta(banco, intrusa, Hoje);
        var resultado = await Controller(banco, intrusa.Id).Marcar("ovos", Pedido(dietaDaDona.Id, false));
        Assert.Equal(StatusCodes.Status409Conflict, ((ObjectResult)resultado).StatusCode);

        var marcacao = Assert.Single(banco.ListaComprasMarcacoes);
        Assert.Equal((dona.Id, dietaDaDona.Id, "ovos"), (marcacao.UsuarioId, marcacao.DietaIAId, marcacao.Chave));
        Assert.All((await Lista(banco, intrusa.Id)).Secoes.SelectMany(s => s.Itens), i => Assert.False(i.Tenho));
    }

    // ---------- Toque duplo ----------

    // Só a chave duplicada (23505) é tratada como "já marcado"; qualquer outro erro do banco aparece.
    [Theory]
    [InlineData("23505", true)]   // unique_violation
    [InlineData("23503", false)]  // foreign_key_violation
    [InlineData("23502", false)]  // not_null_violation
    [InlineData("40001", false)]  // serialization_failure
    public void So_A_Chave_Duplicada_Do_Postgres_E_Ignorada(string sqlState, bool ignorada)
    {
        var erro = new DbUpdateException("falha", new Npgsql.PostgresException("falha", "ERROR", "ERROR", sqlState));

        Assert.Equal(ignorada, ListaComprasController.EhChaveDuplicada(erro));
    }

    [Fact]
    public void Erro_Que_Nao_Vem_Do_Postgres_Nao_E_Ignorado()
    {
        Assert.False(ListaComprasController.EhChaveDuplicada(new DbUpdateException("falha", new InvalidOperationException("outro"))));
        Assert.False(ListaComprasController.EhChaveDuplicada(new DbUpdateException("falha")));
    }

    // ---------- Histórico ----------

    // O RepositorioDietaIA abre uma transação, que o banco em memória ignora com um aviso.
    private static ApplicationDbContext BancoQueAceitaTransacao() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task Marcacoes_Somem_Quando_A_Dieta_Sai_Do_Historico()
    {
        using var banco = BancoQueAceitaTransacao();
        var usuario = NovoUsuario(banco, "marca12@exemplo.com");
        var outro = NovoUsuario(banco, "marca13@exemplo.com");
        var repositorio = new RepositorioDietaIA(banco);

        // Três dietas, cada uma com uma marcação; e uma marcação de outra pessoa, que não pode sumir.
        for (var i = 0; i < 3; i++)
        {
            await repositorio.SalvarAsync(usuario.Id, DietaPadrao());
            var atual = banco.DietasIA.Where(d => d.UsuarioId == usuario.Id).OrderByDescending(d => d.DataGeracao).First();
            await Controller(banco, usuario.Id).Marcar("ovos", Pedido(atual.Id, true));
        }
        await repositorio.SalvarAsync(outro.Id, DietaPadrao());
        var dietaDoOutro = banco.DietasIA.Single(d => d.UsuarioId == outro.Id);
        await Controller(banco, outro.Id).Marcar("ovos", Pedido(dietaDoOutro.Id, true));

        var maisAntiga = banco.DietasIA.Where(d => d.UsuarioId == usuario.Id).OrderBy(d => d.DataGeracao).First().Id;
        Assert.Equal(4, banco.ListaComprasMarcacoes.Count());

        // A quarta dieta tira a mais antiga do histórico, e as marcações dela vão junto. Antes, o contexto
        // esquece o que carregou, como numa requisição nova: com as marcações na memória, o EF já as
        // apagaria em cascata, e o teste não provaria a remoção do repositório.
        banco.ChangeTracker.Clear();
        await repositorio.SalvarAsync(usuario.Id, DietaPadrao());

        Assert.DoesNotContain(banco.DietasIA, d => d.Id == maisAntiga);
        Assert.DoesNotContain(banco.ListaComprasMarcacoes, m => m.DietaIAId == maisAntiga);
        Assert.Equal(2, banco.ListaComprasMarcacoes.Count(m => m.UsuarioId == usuario.Id));
        Assert.Single(banco.ListaComprasMarcacoes, m => m.UsuarioId == outro.Id);
    }

    // No PostgreSQL, a cascata garante o mesmo quando a dieta ou a conta é apagada por outro caminho
    // (a limpeza de dados de teste, por exemplo). O teste confere o modelo que gera a migration.
    [Fact]
    public void Marcacao_Apaga_Em_Cascata_Com_A_Dieta_E_Com_O_Usuario_E_E_Unica_Por_Dieta_E_Chave()
    {
        using var banco = BancoEmMemoria.NovoContexto();
        var entidade = banco.Model.FindEntityType(typeof(ListaComprasMarcacao))!;

        var chaves = entidade.GetForeignKeys().ToDictionary(f => f.PrincipalEntityType.ClrType, f => f.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, chaves[typeof(DietaIA)]);
        Assert.Equal(DeleteBehavior.Cascade, chaves[typeof(Usuario)]);

        var unico = Assert.Single(entidade.GetIndexes(), i => i.IsUnique);
        Assert.Equal(new[] { "DietaIAId", "Chave" }, unico.Properties.Select(p => p.Name));
        Assert.Equal(64, entidade.FindProperty("Chave")!.GetMaxLength());
    }
}
