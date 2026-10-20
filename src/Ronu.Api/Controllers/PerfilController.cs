using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Validacao;
using System.Security.Claims;

namespace Ronu.Api.Controllers;

/// <summary>
/// Gerencia os dados pessoais (altura, sexo, data de nascimento) do usuário
/// logado — separado dos demais Controllers porque não é autenticação,
/// objetivo, modalidade nem preferência alimentar, e sim dados próprios da
/// entidade Usuario usados no cálculo de dieta.
/// </summary>
[ApiController]
[Route("api/perfil")]
[Authorize]
public class PerfilController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PerfilController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Retorna os dados pessoais do usuário logado (podem estar incompletos,
    /// se o onboarding ainda não os preencheu).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ObterAtual()
    {
        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == UsuarioIdLogado);

        return Ok(MontarResposta(usuario));

    }

    // Mesma resposta no GET e no PUT.
    private static PerfilResponse MontarResposta(Models.Usuario usuario) => new()
    {
        Altura = usuario.Altura,
        Sexo = usuario.Sexo,
        DataNascimento = usuario.DataNascimento,
        RotinaDiaria = usuario.RotinaDiaria,
        OrcamentoSemanal = usuario.OrcamentoSemanal,
        NivelAtividade = usuario.NivelAtividade,
        Nome = usuario.Nome,
        Email = usuario.Email,
        // A coluna guarda "" quando não há UF; a resposta devolve null.
        Estado = string.IsNullOrWhiteSpace(usuario.Estado) ? null : usuario.Estado,
        TemSenha = usuario.SenhaHash is not null,
        TemGoogle = usuario.GoogleId is not null
    };

    // Valores fixos enviados pelas pills de alimentacao.html — sem normalização
    // (diferente do Estado), porque não vêm de digitação livre.
    private static readonly HashSet<string> ValoresOrcamentoSemanal = new()
    {
        "economico", "moderado", "sem_restricao"
    };

    /// <summary>
    /// Atualiza os dados pessoais do usuário logado. Diferente de
    /// ObjetivoUsuario, não é histórico — sobrescreve os valores atuais,
    /// pois altura/sexo/data de nascimento não mudam com frequência e não
    /// há necessidade de rastrear versões anteriores.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Atualizar(PerfilRequest request)
    {
        // Opcional: nulo ou vazio segue normalmente; qualquer outro valor
        // precisa ser uma das faixas conhecidas pela regra 12 do prompt.
        if (!string.IsNullOrEmpty(request.OrcamentoSemanal) && !ValoresOrcamentoSemanal.Contains(request.OrcamentoSemanal))
        {
            return BadRequest(new { mensagem = "Orçamento semanal inválido." });
        }

        if (!string.IsNullOrEmpty(request.NivelAtividade) && !RegrasDadosCorporais.NiveisAtividade.Contains(request.NivelAtividade))
        {
            return BadRequest(new { mensagem = RegrasDadosCorporais.MensagemNivelAtividade });
        }

        // Mesmas regras da simulação (RegrasDadosCorporais); altura pelo [Range]
        // do PerfilRequest. Valor fora do conjunto cairia na fórmula errada sem aviso.
        if (!RegrasDadosCorporais.Sexos.Contains(request.Sexo))
        {
            return BadRequest(new { mensagem = RegrasDadosCorporais.MensagemSexo });
        }

        var erroNascimento = RegrasDadosCorporais.ValidarNascimento(request.DataNascimento, DateOnly.FromDateTime(DateTime.UtcNow));
        if (erroNascimento is not null)
        {
            return BadRequest(new { mensagem = erroNascimento });
        }

        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == UsuarioIdLogado);

        usuario.Altura = request.Altura;
        usuario.Sexo = request.Sexo;
        usuario.DataNascimento = request.DataNascimento;
        usuario.RotinaDiaria = request.RotinaDiaria;
        usuario.OrcamentoSemanal = request.OrcamentoSemanal;

        // Ausente ou vazio mantém o salvo (ver PerfilRequest.NivelAtividade).
        if (!string.IsNullOrEmpty(request.NivelAtividade))
        {
            usuario.NivelAtividade = request.NivelAtividade;
        }

        await _context.SaveChangesAsync();

        return Ok(MontarResposta(usuario));

    }

    private static readonly HashSet<string> SiglasUf = new()
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    };

    /// <summary>
    /// Define o estado (UF) do usuário logado. Usado pelo fluxo pós-login
    /// quando o LoginResponse indica PrecisaInformarEstado — contas criadas
    /// via Google ou antes da coluna Estado existir ficam com o campo vazio.
    /// </summary>
    [HttpPut("estado")]
    public async Task<IActionResult> AtualizarEstado(EstadoRequest request)
    {
        var sigla = request.Estado.Trim().ToUpperInvariant();

        if (!SiglasUf.Contains(sigla))
        {
            return BadRequest(new { mensagem = "Estado inválido." });
        }

        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == UsuarioIdLogado);

        usuario.Estado = sigla;

        await _context.SaveChangesAsync();

        return Ok(new { estado = usuario.Estado });
    }
}
