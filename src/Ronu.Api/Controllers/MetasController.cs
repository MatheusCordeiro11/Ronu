using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models.IA;
using Ronu.Api.Services;
using Ronu.Api.Services.IA;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

/// <summary>
/// Simulação da meta da fórmula, para as telas mostrarem a conta enquanto a
/// pessoa preenche (onboarding, Perfil, Treino). Só leitura: não grava nada e
/// não chama a IA. Usa as mesmas classes da geração da dieta
/// (CalculadoraManutencao), então o número da simulação é o da dieta, menos o
/// ajuste adaptativo, que depende do histórico.
/// </summary>
[ApiController]
[Route("api/metas")]
[Authorize]
public class MetasController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ICalculadoraGastoCalorico _calculadora;
    private readonly ICalculadoraPesoTendencia _calculadoraPesoTendencia;

    public MetasController(
        ApplicationDbContext context,
        ICalculadoraGastoCalorico calculadora,
        ICalculadoraPesoTendencia calculadoraPesoTendencia)
    {
        _context = context;
        _calculadora = calculadora;
        _calculadoraPesoTendencia = calculadoraPesoTendencia;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Os valores que a fórmula entende (comparação exata, como no cálculo).
    private static readonly HashSet<string> Sexos = new() { "Masculino", "Feminino" };
    private static readonly HashSet<string> Objetivos = new() { "perder peso", "manter peso", "ganhar peso" };

    // Mesma faixa que o onboarding e o perfil já aplicam no campo de data.
    private const int IdadeMaxima = 100;

    /// <summary>
    /// Calcula a meta da fórmula com o que vier no pedido e, no que faltar, os
    /// dados salvos da pessoa. Só a data "hoje" do servidor (UTC) entra além
    /// disso, para a idade — a mesma regra da geração da dieta.
    /// </summary>
    [HttpPost("simulacao")]
    public async Task<IActionResult> Simular(SimulacaoMetaRequest request)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        // Primeiro o que dá para conferir sem banco: valores fora do conjunto
        // ou da faixa só valem quando vêm no pedido (os salvos já passaram
        // pelas telas).
        if (request.Sexo is not null && !Sexos.Contains(request.Sexo))
        {
            return BadRequest(new { mensagem = "Sexo inválido." });
        }

        if (request.Objetivo is not null && !Objetivos.Contains(request.Objetivo))
        {
            return BadRequest(new { mensagem = "Objetivo inválido." });
        }

        if (request.DataNascimento is { } nascimentoInformado)
        {
            if (nascimentoInformado > hoje)
            {
                return BadRequest(new { mensagem = "A data de nascimento não pode ser no futuro." });
            }

            if (nascimentoInformado < hoje.AddYears(-IdadeMaxima))
            {
                return BadRequest(new { mensagem = "Informe uma data de nascimento de até 100 anos atrás." });
            }
        }

        if (request.Modalidades is not null)
        {
            var erro = ValidarModalidades(request.Modalidades);
            if (erro is not null)
            {
                return BadRequest(new { mensagem = erro });
            }
        }

        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == UsuarioIdLogado);

        var sexo = request.Sexo ?? usuario.Sexo;
        var altura = request.Altura ?? usuario.Altura;
        var dataNascimento = request.DataNascimento ?? usuario.DataNascimento;

        if (sexo is null)
        {
            return BadRequest(new { mensagem = "Informe seu sexo." });
        }

        if (altura is null)
        {
            return BadRequest(new { mensagem = "Informe sua altura." });
        }

        if (dataNascimento is null)
        {
            return BadRequest(new { mensagem = "Informe sua data de nascimento." });
        }

        // Peso e objetivo: o pedido ou o histórico de pesagens. O peso salvo é o
        // de tendência, o mesmo que a geração da dieta usa.
        var historicoPeso = request.Peso is null || request.Objetivo is null
            ? await _context.ObjetivosUsuario
                .Where(o => o.UsuarioId == UsuarioIdLogado)
                .OrderBy(o => o.DataRegistro)
                .Select(o => new RegistroPesoAderenciaDto { Data = o.DataRegistro, Peso = o.Peso, Aderencia = o.Aderencia, Objetivo = o.Objetivo })
                .ToListAsync()
            : new List<RegistroPesoAderenciaDto>();

        if (request.Peso is null && historicoPeso.Count == 0)
        {
            return BadRequest(new { mensagem = "Informe seu peso." });
        }

        var objetivo = request.Objetivo ?? historicoPeso.LastOrDefault()?.Objetivo;
        if (objetivo is null)
        {
            return BadRequest(new { mensagem = "Informe seu objetivo." });
        }

        var peso = request.Peso ?? ContextoDietaBuilder.PesoDeTendencia(historicoPeso, _calculadoraPesoTendencia);
        var pesoOrigem = request.Peso is null ? "tendencia" : "informado";

        var modalidades = request.Modalidades is null
            ? await ModalidadesSalvasAsync()
            : await ModalidadesDoPedidoAsync(request.Modalidades);

        if (modalidades is null)
        {
            return NotFound(new { mensagem = "Modalidade não encontrada." });
        }

        var idade = ContextoDietaBuilder.Idade(dataNascimento.Value, hoje);
        var contexto = new ContextoDietaDto
        {
            Sexo = sexo,
            Altura = altura.Value,
            Idade = idade,
            Peso = peso,
            Objetivo = objetivo,
            Modalidades = modalidades.Select(m => new ModalidadeContextoDto
            {
                Nome = m.Nome,
                MetReferencia = m.MetReferencia,
                DuracaoHoras = m.DuracaoHoras,
                DiasSemana = m.DiasSemana
            }).ToList(),
            Preferencias = new()
        };

        return Ok(MontarResposta(contexto, modalidades, dataNascimento.Value, pesoOrigem, _calculadora));
    }

    // Mesmas regras e mensagens do POST /api/usuarios/modalidades, mais a de
    // não repetir a mesma modalidade (lá ela vira atualização; aqui seria ambíguo).
    private static string? ValidarModalidades(List<UsuarioModalidadeRequest> modalidades)
    {
        foreach (var m in modalidades)
        {
            if (m.DiasSemana.Any(d => d < 1 || d > 7))
            {
                return "Os dias da semana devem estar entre 1 (Segunda) e 7 (Domingo).";
            }

            if (m.DiasSemana.Distinct().Count() != m.DiasSemana.Length)
            {
                return "Não é possível repetir o mesmo dia da semana.";
            }
        }

        if (modalidades.Select(m => m.ModalidadeId).Distinct().Count() != modalidades.Count)
        {
            return "Não é possível repetir a mesma modalidade.";
        }

        return null;
    }

    private async Task<List<ModalidadeSimulada>> ModalidadesSalvasAsync() =>
        await _context.UsuarioModalidades
            .Where(um => um.UsuarioId == UsuarioIdLogado)
            .OrderBy(um => um.Id)
            .Select(um => new ModalidadeSimulada(
                um.ModalidadeId, um.Modalidade.Nome, um.Modalidade.MetReferencia, um.DuracaoMediaHoras, um.DiasSemana))
            .ToListAsync();

    // Nulo se algum Id não existe no catálogo.
    private async Task<List<ModalidadeSimulada>?> ModalidadesDoPedidoAsync(List<UsuarioModalidadeRequest> pedido)
    {
        var ids = pedido.Select(m => m.ModalidadeId).ToList();
        var catalogo = await _context.Modalidades
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        if (catalogo.Count != ids.Count)
        {
            return null;
        }

        return pedido.Select(m => new ModalidadeSimulada(
            m.ModalidadeId, catalogo[m.ModalidadeId].Nome, catalogo[m.ModalidadeId].MetReferencia,
            m.DuracaoMediaHoras, m.DiasSemana)).ToList();
    }

    /// <summary>
    /// Arredonda as parcelas exatas da fórmula para a tela. Cada parte é
    /// arredondada uma vez (a TMB pelo maior resto, para as suas parcelas
    /// somarem o total) e todo total é a soma das partes mostradas.
    /// Separado do banco para os testes.
    /// </summary>
    public static SimulacaoMetaResponse MontarResposta(
        ContextoDietaDto contexto, IReadOnlyList<ModalidadeSimulada> modalidades, DateOnly dataNascimento,
        string pesoOrigem, ICalculadoraGastoCalorico calculadora)
    {
        var composicao = CalculadoraManutencao.ComposicaoPorDia(contexto, calculadora);

        var parcelasTmb = ArredondamentoPartes.MaiorResto(new[]
        {
            10 * contexto.Peso,
            6.25m * contexto.Altura,
            -5m * contexto.Idade,
            contexto.Sexo == "Masculino" ? 5m : -161m
        });
        var tmb = parcelasTmb.Sum();
        var atividade = ArredondamentoPartes.Arredondar(composicao[0].AtividadeForaDoTreino);

        var kcalSessao = modalidades
            .Select(m => ArredondamentoPartes.Arredondar(calculadora.CalcularGastoSessao(m.MetReferencia, contexto.Peso, m.DuracaoHoras)))
            .ToArray();

        var dias = composicao.Select(c =>
        {
            var treino = c.Treinos.Sum(t => kcalSessao[t.IndiceModalidade]);
            var manutencao = tmb + atividade + treino;
            var objetivo = ArredondamentoPartes.Arredondar(c.Objetivo);

            return new DiaSimulacaoDto
            {
                DiaSemana = c.DiaSemana,
                TreinoKcal = treino,
                Manutencao = manutencao,
                ObjetivoKcal = objetivo,
                TetoDeficitAplicado = c.TetoDeficitAplicado,
                MetaFormula = manutencao + objetivo
            };
        }).ToList();

        return new SimulacaoMetaResponse
        {
            VersaoFormula = CalculadoraManutencao.VersaoFormula,
            Entradas = new EntradasSimulacaoDto
            {
                Sexo = contexto.Sexo,
                Altura = contexto.Altura,
                DataNascimento = dataNascimento,
                Idade = contexto.Idade,
                Peso = contexto.Peso,
                PesoOrigem = pesoOrigem,
                Objetivo = contexto.Objetivo
            },
            GastoRepouso = new GastoRepousoSimulacaoDto
            {
                Total = tmb,
                Peso = parcelasTmb[0],
                Altura = parcelasTmb[1],
                Idade = parcelasTmb[2],
                Sexo = parcelasTmb[3]
            },
            AtividadeForaDoTreino = new AtividadeSimulacaoDto
            {
                Fator = CalculadoraManutencao.FatorAtividadeForaDoTreino,
                Kcal = atividade
            },
            RitmoEsperadoKgSemana = Math.Round(RitmoObjetivo.KgPorSemana(contexto.Objetivo, contexto.Peso), 2, MidpointRounding.AwayFromZero),
            Modalidades = modalidades.Select((m, i) => new ModalidadeSimulacaoDto
            {
                ModalidadeId = m.ModalidadeId,
                Nome = m.Nome,
                MetReferencia = m.MetReferencia,
                DuracaoHoras = m.DuracaoHoras,
                DiasSemana = m.DiasSemana,
                KcalSessao = kcalSessao[i]
            }).ToList(),
            Dias = dias
        };
    }
}

/// <summary>Uma modalidade da simulação, salva ou vinda do pedido.</summary>
public sealed record ModalidadeSimulada(int ModalidadeId, string Nome, decimal MetReferencia, decimal DuracaoHoras, int[] DiasSemana);
