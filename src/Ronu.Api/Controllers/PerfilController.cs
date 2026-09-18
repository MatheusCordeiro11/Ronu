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
            DataNascimento = usuario.DataNascimento
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

        await _context.SaveChangesAsync();

        return Ok(new PerfilResponse
        {
            Altura = usuario.Altura,
            Sexo = usuario.Sexo,
            DataNascimento = usuario.DataNascimento
        });
    }
}
