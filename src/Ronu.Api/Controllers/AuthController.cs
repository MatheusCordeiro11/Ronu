using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;
using System.Globalization;
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
    [EnableRateLimiting(LimitesAutenticacao.PoliticaCadastro)]
    public async Task<IActionResult> Cadastro(CadastroRequest request)
    {
        // A conta e o aceite nascem juntos: nunca existe conta criada por aqui sem o consentimento, com o
        // interruptor ligado. Desligado (só durante a publicação), o frontend antigo ainda cadastra sem ele.
        if (request.ConsentimentoGeral is null && RegrasConsentimento.Exigir(_configuration))
        {
            return BadRequest(new { mensagem = RegrasConsentimento.MensagemCadastroSemAceite });
        }

        var vigente = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoGeral);
        if (request.ConsentimentoGeral is not null && request.ConsentimentoGeral.Versao != vigente)
        {
            return Conflict(new { mensagem = RegrasConsentimento.MensagemVersaoMudou, versaoVigente = vigente });
        }

        var email = NormalizacaoEmail.Normalizar(request.Email);

        bool emailJaExiste = await _context.Usuarios.AnyAsync(u => u.Email == email);
        if (emailJaExiste)
        {
            return Conflict(new { mensagem = "Este email já está cadastrado." });
        }

        var usuario = new Usuario
        {
            Nome = request.Nome,
            Email = email,
            Estado = request.Estado,
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha),
            CriadoEm = DateTime.UtcNow
        };

        _context.Usuarios.Add(usuario);

        if (request.ConsentimentoGeral is not null)
        {
            _context.Consentimentos.Add(new Consentimento
            {
                Usuario = usuario,
                Tipo = TextosConsentimento.TipoGeral,
                Versao = vigente,
                AceitoEm = usuario.CriadoEm.Value
            });
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dois cadastros com o mesmo email ao mesmo tempo: a checagem acima
            // passou para os dois, e o índice único barrou o segundo.
            return Conflict(new { mensagem = "Este email já está cadastrado." });
        }

        return Ok(new { usuario.Id, usuario.Nome, usuario.Email });
    }

    [HttpPost("login")]
    [EnableRateLimiting(LimitesAutenticacao.PoliticaLogin)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = NormalizacaoEmail.Normalizar(request.Email);
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

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

        usuario.UltimoLoginEm = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(await RespostaLogin(usuario));
    }

    /// <summary>
    /// Autentica (ou cria, se for a primeira vez) um usuário via login com
    /// Google. Recebe o ID token que o frontend obtém do botão "Entrar com
    /// Google" e valida sua autenticidade diretamente com o Google antes de
    /// confiar em qualquer dado nele — nunca decodifica o token sem validar
    /// a assinatura, o que seria forjável por qualquer cliente.
    /// Vincula pelo email: se já existe uma conta com o mesmo email (criada
    /// via cadastro tradicional), o login com Google passa a valer para essa
    /// mesma conta, em vez de criar uma duplicata. Por isso exige o email
    /// verificado pelo Google (EmailVerified).
    /// </summary>
    [HttpPost("google")]
    [EnableRateLimiting(LimitesAutenticacao.PoliticaGoogle)]
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

        // O vínculo com uma conta existente é pelo email, e o email também é
        // o caminho da redefinição de senha: só vale se o Google o verificou.
        if (!payload.EmailVerified)
        {
            return Unauthorized(new { mensagem = "O email da sua conta Google não está verificado. Verifique-o no Google ou entre com email e senha." });
        }

        var email = NormalizacaoEmail.Normalizar(payload.Email);
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                Nome = payload.Name,
                Email = email,
                SenhaHash = null,
                GoogleId = payload.Subject,
                CriadoEm = DateTime.UtcNow
            };

            _context.Usuarios.Add(usuario);
        }
        else if (usuario.GoogleId is null)
        {
            usuario.GoogleId = payload.Subject;
        }

        // A conta Google nasce só com nome e email, sem aceite: o consentimento vem na tela seguinte, antes de
        // qualquer dado de saúde (o FiltroConsentimento barra o resto até lá).
        usuario.UltimoLoginEm = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(await RespostaLogin(usuario));
    }

    private async Task<LoginResponse> RespostaLogin(Usuario usuario) => new()
    {
        Token = GerarToken(usuario),
        Usuario = new UsuarioResumo { Id = usuario.Id, Nome = usuario.Nome },
        PrecisaInformarEstado = string.IsNullOrEmpty(usuario.Estado),
        PrecisaConsentimento = !await RegrasConsentimento.TemAceiteVigenteAsync(_context, usuario.Id, TextosConsentimento.TipoGeral)
    };

    private string GerarToken(Usuario usuario)
    {
        var chaveJwt = _configuration["Jwt:ChaveSecreta"]!;
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        // "iat" (quando o token foi emitido): ValidacaoSessaoJwt o compara com
        // Usuario.SenhaAlteradaEm para derrubar as sessões abertas antes de uma
        // troca de senha.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credenciais
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
