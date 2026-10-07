using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Services.Email;

namespace Ronu.Api.Services;

/// <summary>Por que um link de redefinição não serve.</summary>
public enum MotivoLinkInvalido
{
    // Não existe, ou foi substituído por um pedido mais novo.
    Invalido,
    Expirado,
    Usado
}

/// <summary>
/// "Esqueci minha senha": pedido, conferência do link e troca da senha.
///
/// - Só contas existentes geram pedido e email; email sem conta não deixa
///   registro. O controller responde igual nos dois casos.
/// - Conta com senha recebe um link com token de uso único, válido por 30
///   minutos; o banco guarda só o SHA-256 do token. Conta só com Google recebe
///   um aviso de que usa o login com Google, sem link.
/// - Um pedido novo invalida os links anteriores: só o mais recente vale.
/// - Limite por conta (3 por hora, 10 por dia, contando os avisos do Google);
///   acima dele o email não sai, sem mudar a resposta. O limite por IP fica
///   no rate limiter do Program.cs.
/// - A troca da senha grava SenhaAlteradaEm, que derruba os tokens JWT
///   emitidos antes (ValidacaoSessaoJwt), e invalida os outros links.
///
/// O token e o link nunca vão para o log.
/// </summary>
public class ServicoRedefinicaoSenha
{
    public static readonly TimeSpan ValidadeLink = TimeSpan.FromMinutes(30);
    public const int LimitePorHora = 3;
    public const int LimitePorDia = 10;

    // Pedidos mais velhos que isso são apagados a cada pedido novo (não há
    // tarefa agendada no plano F1).
    public static readonly TimeSpan Retencao = TimeSpan.FromDays(7);

    private readonly ApplicationDbContext _context;
    private readonly IFilaEmail _filaEmail;
    private readonly string _urlBaseFrontend;
    private readonly ILogger<ServicoRedefinicaoSenha> _logger;

    public ServicoRedefinicaoSenha(
        ApplicationDbContext context, IFilaEmail filaEmail, IConfiguration configuration, ILogger<ServicoRedefinicaoSenha> logger)
    {
        _context = context;
        _filaEmail = filaEmail;
        // Da configuração, nunca dos cabeçalhos da requisição (Host/Origin
        // forjados mandariam o link para outro site).
        _urlBaseFrontend = (configuration["Frontend:UrlBase"]
            ?? throw new InvalidOperationException("Frontend:UrlBase não configurado.")).TrimEnd('/');
        _logger = logger;
    }

    public async Task SolicitarAsync(string emailDigitado, DateTime agora)
    {
        await RemoverPedidosAntigosAsync(agora);

        var email = NormalizacaoEmail.Normalizar(emailDigitado);
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

        if (usuario is null)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Pedido de redefinição de senha para um email sem conta (nenhum email enviado)");
            return;
        }

        var pedidosRecentes = await _context.PedidosRedefinicaoSenha
            .Where(p => p.UsuarioId == usuario.Id && p.CriadoEm > agora.AddDays(-1))
            .Select(p => p.CriadoEm)
            .ToListAsync();

        if (pedidosRecentes.Count >= LimitePorDia || pedidosRecentes.Count(c => c > agora.AddHours(-1)) >= LimitePorHora)
        {
            await _context.SaveChangesAsync();
            _logger.LogWarning("Pedido de redefinição de senha acima do limite por conta, usuário {UsuarioId} (nenhum email enviado)", usuario.Id);
            return;
        }

        await InvalidarLinksAtivosAsync(usuario.Id, agora);

        MensagemEmail mensagem;
        if (usuario.SenhaHash is null)
        {
            _context.PedidosRedefinicaoSenha.Add(new PedidoRedefinicaoSenha
            {
                UsuarioId = usuario.Id,
                Tipo = PedidoRedefinicaoSenha.TipoAvisoGoogle,
                CriadoEm = agora
            });
            mensagem = ModelosEmail.AvisoContaGoogle(usuario.Email, usuario.Nome);
        }
        else
        {
            var token = NovoToken();
            _context.PedidosRedefinicaoSenha.Add(new PedidoRedefinicaoSenha
            {
                UsuarioId = usuario.Id,
                Tipo = PedidoRedefinicaoSenha.TipoLink,
                TokenHash = Hash(token),
                CriadoEm = agora,
                ExpiraEm = agora + ValidadeLink
            });
            // No fragmento (#), não na query: o fragmento não vai ao servidor
            // nem no cabeçalho Referer para outros sites.
            var link = $"{_urlBaseFrontend}/redefinir-senha.html#token={token}";
            mensagem = ModelosEmail.LinkRedefinicaoSenha(usuario.Email, usuario.Nome, link, (int)ValidadeLink.TotalMinutes);
        }

        await _context.SaveChangesAsync();

        if (!_filaEmail.Enfileirar(mensagem))
        {
            _logger.LogError("Fila de email cheia: email de redefinição de senha do usuário {UsuarioId} não enviado", usuario.Id);
        }
    }

    public async Task<MotivoLinkInvalido?> VerificarAsync(string token, DateTime agora)
    {
        var (_, motivo) = await BuscarPedidoValidoAsync(token, agora);
        return motivo;
    }

    /// <summary>Troca a senha. Nulo = trocada; senão, o motivo do link não servir.</summary>
    public async Task<MotivoLinkInvalido?> RedefinirAsync(string token, string novaSenha, DateTime agora)
    {
        var (pedido, motivo) = await BuscarPedidoValidoAsync(token, agora);
        if (pedido is null)
        {
            return motivo;
        }

        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == pedido.UsuarioId);
        usuario.SenhaHash = BCrypt.Net.BCrypt.HashPassword(novaSenha);
        usuario.SenhaAlteradaEm = agora;
        pedido.UsadoEm = agora;
        await InvalidarLinksAtivosAsync(usuario.Id, agora, excetoPedidoId: pedido.Id);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Senha redefinida pelo link, usuário {UsuarioId}", usuario.Id);
        return null;
    }

    private async Task<(PedidoRedefinicaoSenha? Pedido, MotivoLinkInvalido? Motivo)> BuscarPedidoValidoAsync(string token, DateTime agora)
    {
        // Um token nosso tem 43 caracteres (32 bytes em base64url).
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
        {
            return (null, MotivoLinkInvalido.Invalido);
        }

        var hash = Hash(token.Trim());
        var pedido = await _context.PedidosRedefinicaoSenha
            .FirstOrDefaultAsync(p => p.TokenHash == hash && p.Tipo == PedidoRedefinicaoSenha.TipoLink);

        if (pedido is null)
        {
            return (null, MotivoLinkInvalido.Invalido);
        }

        // Usado antes de invalidado: a própria redefinição invalida os outros
        // links, e o que foi usado deve dizer "já usado".
        if (pedido.UsadoEm is not null)
        {
            return (null, MotivoLinkInvalido.Usado);
        }

        if (pedido.InvalidadoEm is not null)
        {
            return (null, MotivoLinkInvalido.Invalido);
        }

        if (pedido.ExpiraEm is null || pedido.ExpiraEm <= agora)
        {
            return (null, MotivoLinkInvalido.Expirado);
        }

        return (pedido, null);
    }

    // Os links ainda válidos do usuário, menos o que acabou de ser usado.
    private async Task InvalidarLinksAtivosAsync(int usuarioId, DateTime agora, int? excetoPedidoId = null)
    {
        var ativos = await _context.PedidosRedefinicaoSenha
            .Where(p => p.UsuarioId == usuarioId
                && p.Id != excetoPedidoId
                && p.Tipo == PedidoRedefinicaoSenha.TipoLink
                && p.UsadoEm == null
                && p.InvalidadoEm == null
                && p.ExpiraEm > agora)
            .ToListAsync();

        foreach (var pedido in ativos)
        {
            pedido.InvalidadoEm = agora;
        }
    }

    private async Task RemoverPedidosAntigosAsync(DateTime agora)
    {
        var limite = agora - Retencao;
        var antigos = await _context.PedidosRedefinicaoSenha.Where(p => p.CriadoEm < limite).ToListAsync();
        _context.PedidosRedefinicaoSenha.RemoveRange(antigos);
    }

    // 32 bytes aleatórios (256 bits): impossível de adivinhar, por isso basta
    // um SHA-256 no banco (sem BCrypt).
    private static string NovoToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
