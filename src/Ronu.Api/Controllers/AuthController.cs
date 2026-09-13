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
/// Endpoints públicos de autenticação: cadastro de novos usuários e login.
/// Não exige [Authorize] porque é justamente aqui que o usuário obtém seu token JWT.
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

    /// <summary>
    /// Cria uma nova conta de usuário. Recebe apenas nome, email e senha:
    /// altura, data de nascimento e sexo não são pedidos aqui, pois são coletados
    /// depois, na etapa de onboarding.
    /// </summary>
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
        // A senha nunca é salva em texto puro: o BCrypt gera um hash com salt
        // embutido, então nem o próprio sistema consegue recuperar a senha original.
        SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha)
    };

    _context.Usuarios.Add(usuario);
    await _context.SaveChangesAsync();

    return Ok(new { usuario.Id, usuario.Nome, usuario.Email });
    }

    /// <summary>
    /// Autentica um usuário existente e retorna um token JWT para ser usado
    /// nas próximas requisições autenticadas.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == request.Email);

        // BCrypt.Verify recalcula o hash da senha informada usando o mesmo salt do
        // hash salvo e compara os dois — por isso não dá para comparar a senha
        // diretamente, é preciso usar o método de verificação do próprio BCrypt.
        if (usuario is null || !BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash))
        {
            return Unauthorized(new { mensagem = "Email ou senha inválidos." });
        }

        var token = GerarToken(usuario);

        var response = new LoginResponse
        {
            Token = token,
            // Retornamos apenas um resumo do usuário (id e nome); a senha/hash
            // nunca deve trafegar de volta para o cliente.
            Usuario = new UsuarioResumo { Id = usuario.Id, Nome = usuario.Nome }
        };

        return Ok(response);
    }

    /// <summary>
    /// Gera o token JWT assinado que identifica o usuário nas próximas requisições.
    /// </summary>
    private string GerarToken(Usuario usuario)
    {
        var chaveJwt = _configuration["Jwt:ChaveSecreta"]!;
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        // O token carrega o Id do usuário (NameIdentifier) e o Nome como claims.
        // O Id é o dado importante: é ele que os controllers protegidos usam depois
        // para saber "quem" está fazendo a requisição, sem precisar receber o
        // UsuarioId no corpo/rota (o que seria inseguro, pois poderia ser forjado).
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