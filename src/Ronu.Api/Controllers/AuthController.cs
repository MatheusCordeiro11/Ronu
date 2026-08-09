using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ronu.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("cadastro")]
    public async Task<IActionResult> Cadastro(CadastroRequest request)
    {
    bool emailJaExiste = await _context.Usuarios.AnyAsync(u => u.Email == request.Email);
    if (emailJaExiste)
    {
        return Conflict(new { mensagem = "Este email já está cadastrado." });
    }

    var usuario = new Usuario
    {
        Nome = request.Nome,
        Email = request.Email,
        SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha)
    };

    _context.Usuarios.Add(usuario);
    await _context.SaveChangesAsync();

    return Ok(new { usuario.Id, usuario.Nome, usuario.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash))
        {
            return Unauthorized(new { mensagem = "Email ou senha inválidos." });
        }

        var token = GerarToken(usuario);

        var response = new LoginResponse
        {
            Token = token,
            Usuario = new UsuarioResumo { Id = usuario.Id, Nome = usuario.Nome }
        };

        return Ok(response);
    }

    private string GerarToken(Usuario usuario)
    {
        var chaveJwt = _configuration["Jwt:ChaveSecreta"]!;
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credenciais
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}