using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Tests;

/// <summary>
/// "Ritmo da aula" (intensidade do treino, IntensidadeTreino): o MET efetivo de cada nível, a regressão de "normal"
/// igual a antes, o vínculo na API (POST/GET /api/usuarios/modalidades), o contexto da dieta, a composição salva e a
/// meta adaptativa na troca de versão (3 → 4) e na troca de ritmo dentro da versão 4.
/// </summary>
public class IntensidadeTreinoTests
{
    private static readonly CalculadoraGastoCalorico Calc = new();

    // ---------- Domínio ----------

    // Regressão principal: em "normal", as 11 aulas em blocos dão exatamente o MET de referência de antes.
    [Fact]
    public void Normal_Reproduz_O_Met_De_Referencia_Das_11_Aulas_Em_Blocos()
    {
        Assert.Equal(11, MetsCompendio2024.BlocosPorModalidade.Count);
        Assert.All(MetsCompendio2024.BlocosPorModalidade, b =>
        {
            var referencia = MetsCompendio2024.PorModalidade[b.Key];
            var (leve, intenso) = (b.Value.Leve.Met, b.Value.Intenso.Met);

            Assert.Equal(referencia, IntensidadeTreino.MetEfetivo(referencia, leve, intenso, IntensidadeTreino.Normal));
            // E pela fórmula com a fração de "normal", não só pelo atalho.
            Assert.Equal(referencia, MetsCompendio2024.Aula(leve, intenso, IntensidadeTreino.FracaoBlocoIntenso[IntensidadeTreino.Normal]));
        });
    }

    [Theory]
    [InlineData("Jiu-jitsu", 6.80, 7.80, 8.80)]
    [InlineData("Boxe", 6.40, 6.80, 7.20)]
    [InlineData("Treino funcional", 4.70, 5.50, 6.30)]
    public void Met_Efetivo_De_Cada_Nivel(string nome, double tecnica, double normal, double sparring)
    {
        var (leve, intenso) = MetsCompendio2024.BlocosPorModalidade[nome];
        var referencia = MetsCompendio2024.PorModalidade[nome];

        Assert.Equal((decimal)tecnica, IntensidadeTreino.MetEfetivo(referencia, leve.Met, intenso.Met, "tecnica"));
        Assert.Equal((decimal)normal, IntensidadeTreino.MetEfetivo(referencia, leve.Met, intenso.Met, "normal"));
        Assert.Equal((decimal)sparring, IntensidadeTreino.MetEfetivo(referencia, leve.Met, intenso.Met, "sparring"));
    }

    [Theory]
    [InlineData("tecnica")]
    [InlineData("normal")]
    [InlineData("sparring")]
    [InlineData(null)]
    public void Sem_Blocos_O_Met_E_O_De_Referencia_Em_Qualquer_Nivel(string? intensidade)
    {
        Assert.Equal(7.0m, IntensidadeTreino.MetEfetivo(7.0m, null, null, intensidade)); // Vôlei
        Assert.Equal(3.5m, IntensidadeTreino.MetEfetivo(3.5m, null, null, intensidade)); // Musculação
    }

    [Fact]
    public void Niveis_Fracoes_E_Padrao()
    {
        Assert.Equal(new[] { "tecnica", "normal", "sparring" }, IntensidadeTreino.Niveis);
        Assert.Equal(IntensidadeTreino.Niveis.Order(), IntensidadeTreino.FracaoBlocoIntenso.Keys.Order());
        Assert.Equal((0.3m, 0.5m, 0.7m), (IntensidadeTreino.FracaoBlocoIntenso["tecnica"],
            IntensidadeTreino.FracaoBlocoIntenso["normal"], IntensidadeTreino.FracaoBlocoIntenso["sparring"]));
        Assert.Equal(MetsCompendio2024.FracaoTempoBlocoIntenso, IntensidadeTreino.FracaoBlocoIntenso[IntensidadeTreino.Padrao]);
        Assert.Equal("normal", IntensidadeTreino.Padrao);
        Assert.Equal("normal", new UsuarioModalidade().Intensidade);
    }

    [Theory]
    [InlineData(null, true, null)]
    [InlineData(null, false, null)]
    [InlineData("sparring", true, null)]
    [InlineData("normal", false, null)]
    [InlineData("sparring", false, IntensidadeTreino.MensagemSemRitmo)]
    [InlineData("tecnica", false, IntensidadeTreino.MensagemSemRitmo)]
    [InlineData("", true, IntensidadeTreino.MensagemNivelInvalido)]
    [InlineData("forte", true, IntensidadeTreino.MensagemNivelInvalido)]
    [InlineData("forte", false, IntensidadeTreino.MensagemNivelInvalido)]
    public void Validacao(string? intensidade, bool temBlocos, string? erro) =>
        Assert.Equal(erro, IntensidadeTreino.Validar(intensidade, temBlocos));

    // De ponta a ponta pela fórmula: 80 kg, jiu-jitsu 1,5 h seg/ter/qui/sex. O dia de descanso não muda.
    [Theory]
    [InlineData("tecnica", 696)]
    [InlineData("normal", 816)]
    [InlineData("sparring", 936)]
    public void Composicao_Por_Dia_Com_Cada_Ritmo(string intensidade, int kcalTreino)
    {
        var contexto = PerfilA(intensidade);

        var composicao = CalculadoraManutencao.ComposicaoPorDia(contexto, Calc);

        var treino = Assert.Single(composicao[0].Treinos);
        Assert.Equal((kcalTreino, intensidade), (treino.Kcal, treino.Intensidade));
        Assert.Equal(2474.5m + kcalTreino, composicao[0].Manutencao);
        Assert.Empty(composicao[2].Treinos);
        Assert.Equal(2474.5m, composicao[2].Manutencao);
    }

    [Fact]
    public void Composicao_Salva_Leva_O_Ritmo()
    {
        var formula = CalculadoraManutencao.ComposicaoPorDia(PerfilA("sparring"), Calc)[0];

        var salva = ComposicaoMeta.Montar(formula, 0m, formula.MetaFormula);

        var treino = Assert.Single(salva.Treino);
        Assert.Equal(("Jiu-jitsu", "sparring", 936), (treino.Nome, treino.Intensidade, treino.Kcal));
        Assert.Null(new TreinoComposicaoDto { Nome = "Dieta antiga" }.Intensidade);
    }

    // ---------- API: POST e GET /api/usuarios/modalidades ----------

    [Fact]
    public async Task Vinculo_Novo_Sem_O_Campo_Nasce_Normal()
    {
        var (banco, controller, usuario, jj, _) = MontarApi();

        Assert.IsType<OkObjectResult>(await controller.AdicionarModalidade(Pedido(jj, null)));

        Assert.Equal("normal", (await banco.UsuarioModalidades.AsNoTracking().SingleAsync(m => m.UsuarioId == usuario.Id)).Intensidade);
    }

    [Fact]
    public async Task Grava_O_Ritmo_E_Atualizacao_Sem_O_Campo_Mantem_O_Salvo()
    {
        var (banco, controller, usuario, jj, _) = MontarApi();

        var resposta = Assert.IsType<OkObjectResult>(await controller.AdicionarModalidade(Pedido(jj, "sparring"))).Value!;
        Assert.Equal("sparring", resposta.GetType().GetProperty("Intensidade")!.GetValue(resposta));

        // O Treino de antes reenvia o vínculo sem o campo (aqui com outra duração).
        var semCampo = Pedido(jj, null);
        semCampo.DuracaoMediaHoras = 2;
        await controller.AdicionarModalidade(semCampo);

        var vinculo = await banco.UsuarioModalidades.AsNoTracking().SingleAsync(m => m.UsuarioId == usuario.Id);
        Assert.Equal(("sparring", 2m), (vinculo.Intensidade, vinculo.DuracaoMediaHoras));

        await controller.AdicionarModalidade(Pedido(jj, "tecnica"));
        Assert.Equal("tecnica", (await banco.UsuarioModalidades.AsNoTracking().SingleAsync(m => m.UsuarioId == usuario.Id)).Intensidade);
    }

    [Theory]
    [InlineData("forte", IntensidadeTreino.MensagemNivelInvalido)]
    [InlineData("", IntensidadeTreino.MensagemNivelInvalido)]
    [InlineData("NORMAL", IntensidadeTreino.MensagemNivelInvalido)]
    public async Task Ritmo_Fora_Dos_Tres_Da_400_E_Nao_Grava(string intensidade, string mensagem)
    {
        var (banco, controller, _, jj, _) = MontarApi();

        var resultado = Assert.IsType<BadRequestObjectResult>(await controller.AdicionarModalidade(Pedido(jj, intensidade)));

        Assert.Equal(mensagem, resultado.Value!.GetType().GetProperty("mensagem")!.GetValue(resultado.Value));
        Assert.Empty(banco.UsuarioModalidades);
    }

    [Fact]
    public async Task Ritmo_Diferente_De_Normal_Em_Modalidade_Sem_Blocos_Da_400_E_Normal_Passa()
    {
        var (banco, controller, _, _, musculacao) = MontarApi();

        var resultado = Assert.IsType<BadRequestObjectResult>(await controller.AdicionarModalidade(Pedido(musculacao, "sparring")));
        Assert.Equal(IntensidadeTreino.MensagemSemRitmo, resultado.Value!.GetType().GetProperty("mensagem")!.GetValue(resultado.Value));
        Assert.Empty(banco.UsuarioModalidades);

        Assert.IsType<OkObjectResult>(await controller.AdicionarModalidade(Pedido(musculacao, "normal")));
    }

    [Fact]
    public async Task Modalidade_Que_Nao_Existe_Continua_Dando_404()
    {
        var (_, controller, _, _, _) = MontarApi();

        Assert.IsType<NotFoundObjectResult>(await controller.AdicionarModalidade(Pedido(999, "sparring")));
    }

    [Fact]
    public async Task Get_Devolve_O_Ritmo_E_Os_Blocos_E_O_Catalogo_Tambem()
    {
        var (_, controller, _, jj, musculacao) = MontarApi();
        await controller.AdicionarModalidade(Pedido(jj, "tecnica"));
        await controller.AdicionarModalidade(Pedido(musculacao, null));

        var lista = Assert.IsAssignableFrom<IEnumerable<UsuarioModalidadeResponse>>(
            Assert.IsType<OkObjectResult>(await controller.ListarMinhasModalidades()).Value).ToList();

        var deJj = lista.Single(m => m.Modalidade.Id == jj);
        Assert.Equal(("tecnica", 5.3m, 10.3m, 7.8m), (deJj.Intensidade, deJj.Modalidade.MetBlocoLeve, deJj.Modalidade.MetBlocoIntenso, deJj.Modalidade.MetReferencia));
        var deMusculacao = lista.Single(m => m.Modalidade.Id == musculacao);
        Assert.Equal(("normal", (decimal?)null, (decimal?)null), (deMusculacao.Intensidade, deMusculacao.Modalidade.MetBlocoLeve, deMusculacao.Modalidade.MetBlocoIntenso));

        var catalogo = Assert.IsAssignableFrom<IEnumerable<ModalidadeResponse>>(
            Assert.IsType<OkObjectResult>(await controller.ListarTodas()).Value).ToList();
        Assert.Equal((5.3m, 10.3m), (catalogo.Single(m => m.Id == jj).MetBlocoLeve, catalogo.Single(m => m.Id == jj).MetBlocoIntenso));
        Assert.Null(catalogo.Single(m => m.Id == musculacao).MetBlocoLeve);
    }

    // ---------- Contexto da dieta ----------

    [Fact]
    public async Task Contexto_Da_Dieta_Usa_O_Met_Efetivo_Do_Ritmo_Salvo()
    {
        var (banco, _, usuario, jj, musculacao) = MontarApi();
        var dados = await banco.Usuarios.SingleAsync(u => u.Id == usuario.Id);
        dados.Sexo = "Masculino";
        dados.Altura = 178;
        dados.DataNascimento = new DateOnly(1995, 5, 10);
        banco.ObjetivosUsuario.Add(new ObjetivoUsuario { UsuarioId = usuario.Id, Peso = 80, Objetivo = "manter peso", DataRegistro = DateTime.UtcNow });
        banco.UsuarioModalidades.AddRange(
            new UsuarioModalidade { UsuarioId = usuario.Id, ModalidadeId = jj, DiasSemana = [1, 2], DuracaoMediaHoras = 1.5m, Intensidade = "sparring" },
            new UsuarioModalidade { UsuarioId = usuario.Id, ModalidadeId = musculacao, DiasSemana = [3], DuracaoMediaHoras = 1 });
        await banco.SaveChangesAsync();

        var contexto = await new ContextoDietaBuilder(banco, new CalculadoraPesoTendencia()).ConstruirAsync(usuario.Id);

        var deJj = contexto.Modalidades.Single(m => m.Nome == "Jiu-jitsu");
        Assert.Equal((8.8m, "sparring"), (deJj.MetEfetivo, deJj.Intensidade));
        var deMusculacao = contexto.Modalidades.Single(m => m.Nome == "Musculação");
        Assert.Equal((3.5m, "normal"), (deMusculacao.MetEfetivo, deMusculacao.Intensidade));
    }

    // ---------- Meta adaptativa ----------

    private static readonly DateTime Agora = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    // A pessoa treina "muito sparring" desde sempre e gasta o que a fórmula nova calcula (3120 por dia, constante
    // para simplificar). A dieta da janela foi gerada em "normal" (3000): o ritmo foi só descrito melhor.
    private const decimal ManutencaoNormal = 3000m;
    private const decimal ManutencaoSparring = 3120m;

    // Dieta da versão 3 na janela: a manutenção salva não vale (outra versão) e a meta adaptativa usa a atual, já
    // com o ritmo novo. Não corrige em dobro.
    [Fact]
    public void Dieta_Da_Versao_3_Na_Janela_Usa_A_Manutencao_Atual_Com_O_Ritmo_Novo()
    {
        var historico = Historico(40, meta: 2500m, gastoReal: ManutencaoSparring);
        var dietaV3 = Dieta(-40, versao: 3, manutencao: ManutencaoNormal);

        var r = NovaCalculadora().Calcular(historico, new[] { dietaV3 }, "perder peso", 2500m, Semana(ManutencaoSparring), Agora, 1.4m);

        Assert.Equal(0m, r.Percentual);
        Assert.Equal(MotivoAjusteAdaptativo.DentroDoEsperado, r.Motivo);
    }

    // Trocar o ritmo já na versão 4 NÃO reinicia a janela (decisão de 2026-11-16, como trocar a duração): a dieta da
    // janela guarda a manutenção "normal", o erro aprendido soma com a manutenção nova, e a meta corrige em dobro até
    // essa dieta sair da janela. O teto de ±5% limita o efeito. Risco registrado em docs/lacunas-conhecidas.md.
    [Fact]
    public void Trocar_O_Ritmo_Na_Versao_4_Nao_Recomeca_A_Janela_E_Pode_Corrigir_Em_Dobro()
    {
        var historico = Historico(40, meta: 2500m, gastoReal: ManutencaoSparring);
        var dietaV4 = Dieta(-40, versao: CalculadoraManutencao.VersaoFormula, manutencao: ManutencaoNormal);

        var r = NovaCalculadora().Calcular(historico, new[] { dietaV4 }, "perder peso", 2500m, Semana(ManutencaoSparring), Agora, 1.4m);

        Assert.True(r.Percentual > 0.01m, $"ajuste {r.Percentual}");
        Assert.True(r.Percentual <= CalculadoraAjusteAdaptativo.Teto, $"ajuste {r.Percentual}");
        Assert.True(r.PontosUsados > 0);
    }

    // ---------- Apoio ----------

    // Perfil A: homem 80 kg, 178 cm, 30 anos, manter peso, jiu-jitsu 1,5 h seg/ter/qui/sex (TMB 1767,5; × 1,4 = 2474,5).
    private static ContextoDietaDto PerfilA(string intensidade) => new()
    {
        Sexo = "Masculino", Peso = 80, Altura = 178, Idade = 30, Objetivo = "manter peso",
        Modalidades = new()
        {
            new()
            {
                Nome = "Jiu-jitsu", MetEfetivo = IntensidadeTreino.MetEfetivo(7.8m, 5.3m, 10.3m, intensidade),
                Intensidade = intensidade, DuracaoHoras = 1.5m, DiasSemana = new[] { 1, 2, 4, 5 }
            }
        },
        Preferencias = new()
    };

    private static (ApplicationDbContext Banco, ModalidadesController Controller, Usuario Usuario, int Jj, int Musculacao) MontarApi()
    {
        var banco = BancoEmMemoria.NovoContexto();
        var usuario = new Usuario { Nome = "Teste", Email = "ritmo-da-aula@exemplo.test", Estado = "MG" };
        banco.Usuarios.Add(usuario);
        var jj = banco.Modalidades.Add(new Modalidade
        {
            Nome = "Jiu-jitsu", MetReferencia = 7.8m, Categoria = CategoriasModalidade.Lutas, MetBlocoLeve = 5.3m, MetBlocoIntenso = 10.3m
        }).Entity;
        var musculacao = banco.Modalidades.Add(new Modalidade
        {
            Nome = "Musculação", MetReferencia = 3.5m, Categoria = CategoriasModalidade.Condicionamento
        }).Entity;
        banco.SaveChanges();

        var controller = new ModalidadesController(banco)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()) }, "teste"))
                }
            }
        };
        return (banco, controller, usuario, jj.Id, musculacao.Id);
    }

    private static UsuarioModalidadeRequest Pedido(int modalidadeId, string? intensidade) => new()
    {
        ModalidadeId = modalidadeId, DiasSemana = new[] { 1, 3 }, DuracaoMediaHoras = 1.5m, Intensidade = intensidade
    };

    private static decimal[] Semana(decimal valor) => Enumerable.Repeat(valor, 7).ToArray();

    // A pessoa come a meta e gasta "gastoReal" por dia; pesa a cada 2 dias.
    private static List<RegistroPesoAderenciaDto> Historico(int dias, decimal meta, decimal gastoReal)
    {
        var peso = 85.0;
        var historico = new List<RegistroPesoAderenciaDto>();
        for (var dia = -dias; dia <= 0; dia++)
        {
            if ((dia + dias) % 2 == 0 || dia == 0)
            {
                historico.Add(new() { Data = Agora.AddDays(dia), Peso = (decimal)Math.Round(peso, 4), Objetivo = "perder peso", Aderencia = "seguiu" });
            }
            peso += ((double)meta - (double)gastoReal) / 7700;
        }
        return historico;
    }

    private static MetaDietaRegistroDto Dieta(int dia, int versao, decimal manutencao) => new()
    {
        DataGeracao = Agora.AddDays(dia),
        VersaoFormula = versao,
        MetasPorDia = Semana(2500m),
        ManutencoesPorDia = Semana(manutencao),
        FatorAtividade = 1.4m
    };

    private static CalculadoraAjusteAdaptativo NovaCalculadora() => new(new CalculadoraPesoTendencia());
}
