using Google.Apis.Auth;
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

/// <summary>
/// Endpoints públicos de autenticação: cadastro de novos usuários, login com
/// email/senha e login com Google. Não exige [Authorize] porque é justamente
/// aqui que o usuário obtém seu token JWT.
/// </summary>
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

        // Contas criadas via Google não têm SenhaHash — tentar comparar contra
        // null quebraria o BCrypt.Verify, então checamos isso antes, com uma
        // mensagem que orienta o usuário para o caminho certo.
        if (usuario is not null && usuario.SenhaHash is null)
        {
            return Unauthorized(new { mensagem = "Esta conta usa login com Google. Entre com sua conta Google." });
        }

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

    /// <summary>
    /// Autentica (ou cria, se for a primeira vez) um usuário via login com
    /// Google. Recebe o ID token que o frontend obtém do botão "Entrar com
    /// Google" e valida sua autenticidade diretamente com o Google antes de
    /// confiar em qualquer dado nele — nunca decodifica o token sem validar
    /// a assinatura, o que seria forjável por qualquer cliente.
    /// Vincula pelo email: se já existe uma conta com o mesmo email (criada
    /// via cadastro tradicional), o login com Google passa a valer para essa
    /// mesma conta, em vez de criar uma duplicata.
    /// </summary>
    [HttpPost("google")]
    public async Task<IActionResult> LoginComGoogle(GoogleLoginRequest request)
    {
        GoogleJsonWebSignature.Payload payload;

        try
        {
            var configuracoesValidacao = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"] }
            };

            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, configuracoesValidacao);
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { mensagem = "Token do Google inválido." });
        }

        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == payload.Email);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                Nome = payload.Name,
                Email = payload.Email,
                SenhaHash = null,
                GoogleId = payload.Subject
            };

            _context.Usuarios.Add(usuario);
        }
        else if (usuario.GoogleId is null)
        {
            usuario.GoogleId = payload.Subject;
        }

        await _context.SaveChangesAsync();

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
