using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
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

        return Ok(new PerfilResponse
        {
            Altura = usuario.Altura,
            Sexo = usuario.Sexo,
            DataNascimento = usuario.DataNascimento,
            RotinaDiaria = usuario.RotinaDiaria,
            OrcamentoSemanal = usuario.OrcamentoSemanal
        });
    }

    /// <summary>
    /// Atualiza os dados pessoais do usuário logado. Diferente de
    /// ObjetivoUsuario, não é histórico — sobrescreve os valores atuais,
    /// pois altura/sexo/data de nascimento não mudam com frequência e não
    /// há necessidade de rastrear versões anteriores.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Atualizar(PerfilRequest request)
    {
        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == UsuarioIdLogado);

        usuario.Altura = request.Altura;
        usuario.Sexo = request.Sexo;
        usuario.DataNascimento = request.DataNascimento;
        usuario.RotinaDiaria = request.RotinaDiaria;
        usuario.OrcamentoSemanal = request.OrcamentoSemanal;

        await _context.SaveChangesAsync();

        return Ok(new PerfilResponse
        {
            Altura = usuario.Altura,
            Sexo = usuario.Sexo,
            DataNascimento = usuario.DataNascimento,
            RotinaDiaria = usuario.RotinaDiaria,
            OrcamentoSemanal = usuario.OrcamentoSemanal
        });
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
