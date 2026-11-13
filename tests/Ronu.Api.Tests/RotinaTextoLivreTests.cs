using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services.Conta;

namespace Ronu.Api.Tests;

/// <summary>
/// LGPD base, etapa 3 dos horários ("opção A", 2026-11-02): a rotina em texto livre (Usuario.RotinaDiaria) saiu da
/// tela e do prompt. A API ignora o campo no PUT /perfil (um frontend antigo em cache não recebe erro nem apaga o
/// texto), a pessoa apaga o próprio texto pelo DELETE /perfil/rotina, a exportação continua levando o texto, e a
/// limpeza dos prazos de guarda apaga o que sobrou 60 dias depois da publicação. Só contas fictícias.
/// </summary>
public class RotinaTextoLivreTests
{
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateTime Publicacao = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    // ---------- PUT /perfil ignora a rotina ----------

    [Fact]
    public async Task Put_Com_Rotina_Nova_Nao_Grava_E_Mantem_A_Antiga()
    {
        var (banco, usuario) = Conta("rotina-put@exemplo.test", "Acordo às 6h");

        var resultado = await Perfil(banco, usuario.Id).Atualizar(Pedido(rotina: "Texto novo de uma tela antiga"));

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal("Acordo às 6h", banco.Usuarios.Single(u => u.Id == usuario.Id).RotinaDiaria);
    }

    [Fact]
    public async Task Put_Sem_Rotina_Nao_Apaga_A_Antiga()
    {
        var (banco, usuario) = Conta("rotina-put-sem@exemplo.test", "Acordo às 6h");

        Assert.IsType<OkObjectResult>(await Perfil(banco, usuario.Id).Atualizar(Pedido(rotina: null)));

        Assert.Equal("Acordo às 6h", banco.Usuarios.Single(u => u.Id == usuario.Id).RotinaDiaria);
    }

    // A coluna tem limite de 500 caracteres. Antes, um texto maior estourava no banco; agora o campo nem é lido.
    [Fact]
    public async Task Put_Com_Rotina_Longa_De_Tela_Antiga_Nao_Da_Erro()
    {
        var (banco, usuario) = Conta("rotina-longa@exemplo.test", null);

        var resultado = await Perfil(banco, usuario.Id).Atualizar(Pedido(rotina: new string('a', 5000)));

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Null(banco.Usuarios.Single(u => u.Id == usuario.Id).RotinaDiaria);
    }

    [Fact]
    public async Task Get_Continua_Mostrando_A_Rotina_Antiga()
    {
        var (banco, usuario) = Conta("rotina-get@exemplo.test", "Treino às 19h");

        var resposta = Assert.IsType<PerfilResponse>(Assert.IsType<OkObjectResult>(await Perfil(banco, usuario.Id).ObterAtual()).Value);

        Assert.Equal("Treino às 19h", resposta.RotinaDiaria);
    }

    // ---------- DELETE /perfil/rotina ----------

    [Fact]
    public async Task Delete_Apaga_So_A_Rotina_De_Quem_Pediu()
    {
        var (banco, usuario) = Conta("rotina-delete@exemplo.test", "Acordo às 6h");
        var outro = new Usuario { Nome = "Outro", Email = "rotina-outro@exemplo.test", RotinaDiaria = "Durmo às 23h" };
        banco.Usuarios.Add(outro);
        banco.SaveChanges();

        Assert.IsType<NoContentResult>(await Perfil(banco, usuario.Id).ApagarRotina());

        var depois = banco.Usuarios.AsNoTracking().Single(u => u.Id == usuario.Id);
        Assert.Null(depois.RotinaDiaria);
        Assert.Equal("Masculino", depois.Sexo);
        Assert.Equal("Durmo às 23h", banco.Usuarios.AsNoTracking().Single(u => u.Id == outro.Id).RotinaDiaria);
    }

    // Repetir (duas abas, clique duplo) responde igual.
    [Fact]
    public async Task Delete_Sem_Rotina_Responde_204()
    {
        var (banco, usuario) = Conta("rotina-delete-vazio@exemplo.test", null);
        var controller = Perfil(banco, usuario.Id);

        Assert.IsType<NoContentResult>(await controller.ApagarRotina());
        Assert.IsType<NoContentResult>(await controller.ApagarRotina());
    }

    // ---------- Exportação ----------

    [Fact]
    public async Task Exportacao_Continua_Levando_A_Rotina()
    {
        var (banco, usuario) = Conta("rotina-export@exemplo.test", "Acordo às 6h");
        var conta = new ContaController(banco, NullLogger<ContaController>.Instance) { ControllerContext = Contexto(usuario.Id) };

        var arquivo = Assert.IsType<FileContentResult>(await conta.Exportar());
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(arquivo.FileContents));

        Assert.Equal("Acordo às 6h", json.RootElement.GetProperty("perfil").GetProperty("rotinaDiaria").GetString());
    }

    // ---------- Limpeza dos prazos de guarda ----------

    [Fact]
    public async Task Limpeza_Sem_Data_Configurada_Nao_Toca_Na_Rotina()
    {
        var (banco, usuario) = Conta("rotina-sem-data@exemplo.test", "Acordo às 6h");

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, Publicacao.AddYears(1));

        Assert.Equal(0, resultado.RotinasApagadas);
        Assert.Equal("Acordo às 6h", banco.Usuarios.Single(u => u.Id == usuario.Id).RotinaDiaria);
    }

    // Da configuração até a limpeza, como o ServicoLimpezaRetencao faz: chave vazia (ou ausente) não apaga nada, nem
    // muito depois de qualquer prazo.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Limpeza_Com_Configuracao_Vazia_Nao_Apaga_Nenhuma_Rotina(string? valor)
    {
        var (banco, usuario) = Conta("rotina-config-vazia@exemplo.test", "Acordo às 6h");
        var dataConfigurada = LimpezaRetencao.LerRotinaForaDaTelaEm(Configuracao(valor));

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, Publicacao.AddYears(5), dataConfigurada);

        Assert.Equal(0, resultado.RotinasApagadas);
        Assert.Equal("Acordo às 6h", banco.Usuarios.AsNoTracking().Single(u => u.Id == usuario.Id).RotinaDiaria);
    }

    [Fact]
    public async Task Limpeza_Antes_Dos_60_Dias_Nao_Toca_Na_Rotina()
    {
        var (banco, usuario) = Conta("rotina-antes@exemplo.test", "Acordo às 6h");

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, Publicacao.AddDays(60).AddSeconds(-1), Publicacao);

        Assert.Equal(0, resultado.RotinasApagadas);
        Assert.Equal("Acordo às 6h", banco.Usuarios.Single(u => u.Id == usuario.Id).RotinaDiaria);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    public async Task Limpeza_A_Partir_Dos_60_Dias_Apaga_Todas_As_Rotinas_E_Mais_Nada(int diasDepois)
    {
        var (banco, usuario) = Conta("rotina-depois@exemplo.test", "Acordo às 6h");
        var vazia = new Usuario { Nome = "Vazia", Email = "rotina-vazia@exemplo.test", RotinaDiaria = "" };
        var sem = new Usuario { Nome = "Sem", Email = "rotina-sem@exemplo.test", OrcamentoSemanal = "moderado" };
        banco.Usuarios.AddRange(vazia, sem);
        banco.SaveChanges();
        var agora = Publicacao.AddDays(60 + diasDepois);

        var resultado = await LimpezaRetencao.ExecutarAsync(banco, agora, Publicacao);

        Assert.Equal(2, resultado.RotinasApagadas);
        Assert.All(banco.Usuarios.AsNoTracking().ToList(), u => Assert.Null(u.RotinaDiaria));
        var depois = banco.Usuarios.AsNoTracking().Single(u => u.Id == usuario.Id);
        Assert.Equal("Masculino", depois.Sexo);
        Assert.Equal(new TimeOnly(6, 0), depois.HorarioAcordar);
        Assert.Equal("moderado", banco.Usuarios.AsNoTracking().Single(u => u.Id == sem.Id).OrcamentoSemanal);

        // A rodada seguinte não encontra mais nada.
        Assert.Equal(0, (await LimpezaRetencao.ExecutarAsync(banco, agora.AddDays(1), Publicacao)).RotinasApagadas);
    }

    // ---------- Configuração da data ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Data_Ausente_Ou_Vazia_Desliga_A_Limpeza_Da_Rotina(string? valor) =>
        Assert.Null(LimpezaRetencao.LerRotinaForaDaTelaEm(Configuracao(valor)));

    [Fact]
    public void Data_Valida_Vira_O_Inicio_Do_Dia_Em_Utc()
    {
        var data = LimpezaRetencao.LerRotinaForaDaTelaEm(Configuracao("2026-12-01"));

        Assert.Equal(Publicacao, data);
        Assert.Equal(DateTimeKind.Utc, data!.Value.Kind);
    }

    // Formato errado derruba a subida (Program.cs) em vez de deixar a limpeza parada sem aviso.
    [Theory]
    [InlineData("01/12/2026")]
    [InlineData("2026-13-01")]
    [InlineData("2026-12-1")]
    [InlineData("amanhã")]
    public void Data_Em_Formato_Errado_Da_Erro(string valor) =>
        Assert.Throws<InvalidOperationException>(() => LimpezaRetencao.LerRotinaForaDaTelaEm(Configuracao(valor)));

    // ---------- Apoio ----------

    private static (ApplicationDbContext Banco, Usuario Usuario) Conta(string email, string? rotina)
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario
        {
            Nome = "Pessoa de teste", Email = email, Estado = "SP", Altura = 178, Sexo = "Masculino",
            DataNascimento = Hoje.AddYears(-30), RotinaDiaria = rotina, HorarioAcordar = new TimeOnly(6, 0)
        };
        banco.Usuarios.Add(usuario);
        banco.SaveChanges();
        return (banco, usuario);
    }

    private static PerfilRequest Pedido(string? rotina) => new()
    {
        Altura = 178, Sexo = "Masculino", DataNascimento = Hoje.AddYears(-30), RotinaDiaria = rotina
    };

    private static ControllerContext Contexto(int usuarioId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "teste"))
        }
    };

    private static PerfilController Perfil(ApplicationDbContext banco, int usuarioId) => new(banco) { ControllerContext = Contexto(usuarioId) };

    private static Microsoft.Extensions.Configuration.IConfiguration Configuracao(string? valor) =>
        BancoEmMemoria.Configuracao(new() { [LimpezaRetencao.ChaveRotinaForaDaTelaEm] = valor });
}
