using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
using Ronu.Api.Services.Email;
using Ronu.Api.Validacao;

namespace Ronu.Api.Tests;

/// <summary>
/// "Esqueci minha senha": pedido (resposta igual, só contas existentes,
/// aviso para conta Google, limite por conta, pedido novo invalida os
/// anteriores), link (inválido, expirado, usado), troca da senha, regras da
/// senha, textos do email e a derrubada das sessões JWT emitidas antes da troca.
/// Banco em memória e fila de email falsa.
/// </summary>
public class RedefinicaoSenhaTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Pedido ----------

    [Fact]
    public async Task Pedido_De_Conta_Com_Senha_Envia_Link_E_Guarda_So_O_Hash()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");

        await servico.SolicitarAsync("Fulano@Gmail.com ", Agora);

        var email = Assert.Single(fila.Mensagens);
        Assert.Equal("fulano@gmail.com", email.Para);
        var token = TokenDoEmail(email);
        Assert.Contains("http://127.0.0.1:5500/frontend/redefinir-senha.html#token=", email.Texto);
        Assert.Contains("30 minutos", email.Texto);

        var pedido = await contexto.PedidosRedefinicaoSenha.SingleAsync();
        Assert.Equal(PedidoRedefinicaoSenha.TipoLink, pedido.Tipo);
        Assert.Equal(ServicoRedefinicaoSenha.Hash(token), pedido.TokenHash);
        Assert.NotEqual(token, pedido.TokenHash);
        Assert.Equal(Agora.AddMinutes(30), pedido.ExpiraEm);
    }

    [Fact]
    public async Task Pedido_De_Email_Sem_Conta_Nao_Envia_Nem_Guarda_Nada()
    {
        var (contexto, fila, servico) = Cenario();

        await servico.SolicitarAsync("ninguem@gmail.com", Agora);

        Assert.Empty(fila.Mensagens);
        Assert.Empty(await contexto.PedidosRedefinicaoSenha.ToListAsync());
    }

    [Fact]
    public async Task Conta_So_Com_Google_Recebe_Aviso_Sem_Link()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "google@gmail.com", senha: null);

        await servico.SolicitarAsync("google@gmail.com", Agora);

        var email = Assert.Single(fila.Mensagens);
        Assert.Contains("login com Google", email.Texto);
        Assert.DoesNotContain("token=", email.Texto);
        var pedido = await contexto.PedidosRedefinicaoSenha.SingleAsync();
        Assert.Equal(PedidoRedefinicaoSenha.TipoAvisoGoogle, pedido.Tipo);
        Assert.Null(pedido.TokenHash);
    }

    [Fact]
    public async Task Controller_Responde_202_Com_A_Mesma_Mensagem_Exista_Ou_Nao_A_Conta()
    {
        var (contexto, _, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        var controller = new RedefinicaoSenhaController(servico);

        var comConta = await controller.EsqueciSenha(new EsqueciSenhaRequest { Email = "fulano@gmail.com" });
        var semConta = await controller.EsqueciSenha(new EsqueciSenhaRequest { Email = "ninguem@gmail.com" });

        Assert.Equal(202, Assert.IsType<AcceptedResult>(comConta).StatusCode);
        Assert.Equal(202, Assert.IsType<AcceptedResult>(semConta).StatusCode);
        Assert.Equal(Mensagem(comConta), Mensagem(semConta));
        Assert.Equal(RedefinicaoSenhaController.MensagemPedido, Mensagem(comConta));
    }

    [Fact]
    public async Task Acima_De_3_Pedidos_Por_Hora_O_Email_Nao_Sai()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");

        for (var i = 0; i < 4; i++)
        {
            await servico.SolicitarAsync("fulano@gmail.com", Agora.AddMinutes(i));
        }

        Assert.Equal(3, fila.Mensagens.Count);

        // Passada a hora, volta a enviar.
        await servico.SolicitarAsync("fulano@gmail.com", Agora.AddMinutes(61));
        Assert.Equal(4, fila.Mensagens.Count);
    }

    [Fact]
    public async Task Acima_De_10_Pedidos_Por_Dia_O_Email_Nao_Sai()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");

        // 3 por hora, espalhados: 11 pedidos em ~4 horas.
        for (var i = 0; i < 11; i++)
        {
            await servico.SolicitarAsync("fulano@gmail.com", Agora.AddMinutes(25 * i));
        }

        Assert.Equal(ServicoRedefinicaoSenha.LimitePorDia, fila.Mensagens.Count);
    }

    [Fact]
    public async Task Pedido_Novo_Invalida_O_Link_Anterior()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");

        await servico.SolicitarAsync("fulano@gmail.com", Agora);
        await servico.SolicitarAsync("fulano@gmail.com", Agora.AddMinutes(1));

        var primeiro = TokenDoEmail(fila.Mensagens[0]);
        var segundo = TokenDoEmail(fila.Mensagens[1]);
        Assert.Equal(MotivoLinkInvalido.Invalido, (await servico.VerificarAsync(primeiro, Agora.AddMinutes(2))).Motivo);
        Assert.Null((await servico.VerificarAsync(segundo, Agora.AddMinutes(2))).Motivo);
    }

    [Fact]
    public async Task Pedidos_Com_Mais_De_7_Dias_Sao_Apagados()
    {
        var (contexto, _, servico) = Cenario();
        var usuario = await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        contexto.PedidosRedefinicaoSenha.Add(new PedidoRedefinicaoSenha
        {
            UsuarioId = usuario.Id, Tipo = PedidoRedefinicaoSenha.TipoLink, TokenHash = "antigo",
            CriadoEm = Agora.AddDays(-8), ExpiraEm = Agora.AddDays(-8).AddMinutes(30)
        });
        await contexto.SaveChangesAsync();

        await servico.SolicitarAsync("ninguem@gmail.com", Agora);

        Assert.Empty(await contexto.PedidosRedefinicaoSenha.ToListAsync());
    }

    // ---------- Link ----------

    [Fact]
    public async Task Token_Desconhecido_Vazio_Ou_Enorme_E_Invalido()
    {
        var (_, _, servico) = Cenario();

        Assert.Equal(MotivoLinkInvalido.Invalido, (await servico.VerificarAsync("token-que-nao-existe", Agora)).Motivo);
        Assert.Equal(MotivoLinkInvalido.Invalido, (await servico.VerificarAsync("", Agora)).Motivo);
        Assert.Equal(MotivoLinkInvalido.Invalido, (await servico.VerificarAsync(new string('a', 500), Agora)).Motivo);
    }

    [Fact]
    public async Task Link_Vale_Por_30_Minutos()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        await servico.SolicitarAsync("fulano@gmail.com", Agora);
        var token = TokenDoEmail(Assert.Single(fila.Mensagens));

        Assert.Null((await servico.VerificarAsync(token, Agora.AddMinutes(29))).Motivo);
        Assert.Equal(MotivoLinkInvalido.Expirado, (await servico.VerificarAsync(token, Agora.AddMinutes(30))).Motivo);
        Assert.Equal(MotivoLinkInvalido.Expirado, await servico.RedefinirAsync(token, "senha-nova-123", Agora.AddMinutes(31)));
    }

    [Fact]
    public async Task Controller_Devolve_O_Motivo_Do_Link_Invalido()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        await servico.SolicitarAsync("fulano@gmail.com", DateTime.UtcNow.AddHours(-1));
        var controller = new RedefinicaoSenhaController(servico);

        var expirado = await controller.Verificar(new VerificarRedefinicaoSenhaRequest { Token = TokenDoEmail(fila.Mensagens[0]) });
        var invalido = await controller.Verificar(new VerificarRedefinicaoSenhaRequest { Token = "nao-existe" });

        Assert.Equal("expirado", Campo(expirado, "motivo"));
        Assert.Equal("invalido", Campo(invalido, "motivo"));
    }

    [Fact]
    public async Task Link_Valido_Devolve_O_Email_Da_Conta()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        await servico.SolicitarAsync("Fulano@Gmail.com", DateTime.UtcNow);
        var token = TokenDoEmail(Assert.Single(fila.Mensagens));
        var controller = new RedefinicaoSenhaController(servico);

        var valido = await controller.Verificar(new VerificarRedefinicaoSenhaRequest { Token = token });
        var invalido = await controller.Verificar(new VerificarRedefinicaoSenhaRequest { Token = "nao-existe" });

        Assert.IsType<OkObjectResult>(valido);
        Assert.Equal("fulano@gmail.com", Campo(valido, "email"));
        Assert.Null(Campo(invalido, "email"));
    }

    // ---------- Troca da senha ----------

    [Fact]
    public async Task Redefinir_Troca_A_Senha_Marca_O_Link_Como_Usado_E_Grava_SenhaAlteradaEm()
    {
        var (contexto, fila, servico) = Cenario();
        var usuario = await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        await servico.SolicitarAsync("fulano@gmail.com", Agora);
        var token = TokenDoEmail(Assert.Single(fila.Mensagens));

        var resultado = await servico.RedefinirAsync(token, "senha-nova-123", Agora.AddMinutes(5));

        Assert.Null(resultado);
        var salvo = await contexto.Usuarios.SingleAsync(u => u.Id == usuario.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("senha-nova-123", salvo.SenhaHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("senha-antiga", salvo.SenhaHash));
        Assert.Equal(Agora.AddMinutes(5), salvo.SenhaAlteradaEm);

        // Uso único.
        Assert.Equal(MotivoLinkInvalido.Usado, (await servico.VerificarAsync(token, Agora.AddMinutes(6))).Motivo);
        Assert.Equal(MotivoLinkInvalido.Usado, await servico.RedefinirAsync(token, "outra-senha-123", Agora.AddMinutes(6)));
    }

    [Fact]
    public async Task Login_Funciona_Com_A_Senha_Nova_E_Nao_Com_A_Antiga()
    {
        var (contexto, fila, servico) = Cenario();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        await servico.SolicitarAsync("fulano@gmail.com", DateTime.UtcNow);
        await servico.RedefinirAsync(TokenDoEmail(fila.Mensagens[0]), "senha-nova-123", DateTime.UtcNow);
        var auth = new AuthController(contexto, BancoEmMemoria.Configuracao());

        Assert.IsType<OkObjectResult>(await auth.Login(new LoginRequest { Email = "fulano@gmail.com", Senha = "senha-nova-123" }));
        Assert.IsType<UnauthorizedObjectResult>(await auth.Login(new LoginRequest { Email = "fulano@gmail.com", Senha = "senha-antiga" }));
    }

    // ---------- Regras da senha (cadastro e redefinição) ----------

    [Theory]
    [InlineData("1234567", false)]
    [InlineData("12345678", true)]
    [InlineData("senha com espaço e acentuação", true)]
    public void Minimo_De_8_Caracteres(string senha, bool valida)
    {
        Assert.Equal(valida, Valida(new RedefinirSenhaRequest { Token = "x", NovaSenha = senha }));
        Assert.Equal(valida, Valida(new CadastroRequest { Nome = "F", Email = "f@x.com", Estado = "SP", Senha = senha }));
    }

    [Fact]
    public void Maximo_De_64_Caracteres_E_72_Bytes()
    {
        Assert.True(Valida(new RedefinirSenhaRequest { Token = "x", NovaSenha = new string('a', 64) }));
        Assert.False(Valida(new RedefinirSenhaRequest { Token = "x", NovaSenha = new string('a', 65) }));
        // 40 letras acentuadas = 80 bytes: passaria do que o BCrypt considera.
        Assert.False(Valida(new RedefinirSenhaRequest { Token = "x", NovaSenha = new string('á', 40) }));
        Assert.False(Valida(new CadastroRequest { Nome = "F", Email = "f@x.com", Estado = "SP", Senha = new string('a', 65) }));
    }

    // ---------- Email ----------

    [Fact]
    public void Nome_Da_Pessoa_E_Escapado_No_Html()
    {
        var email = ModelosEmail.LinkRedefinicaoSenha("a@x.com", "<script>Fulano</script>", "http://x/r.html#token=abc", 30);

        Assert.DoesNotContain("<script>", email.Html);
        Assert.Contains("&lt;script&gt;", email.Html);
        Assert.Equal("Redefinição de senha do Ronu", email.Assunto);
    }

    [Theory]
    [InlineData("Fulano de Tal", "Olá, Fulano de Tal.")]
    [InlineData("Fulano@Gmail.com", "Olá!")]
    [InlineData("  ", "Olá!")]
    public void Saudacao_Usa_O_Nome_So_Quando_Ele_Parece_Um_Nome(string nome, string esperada)
    {
        var link = ModelosEmail.LinkRedefinicaoSenha("fulano@gmail.com", nome, "http://x/r.html#token=abc", 30);
        var aviso = ModelosEmail.AvisoContaGoogle("fulano@gmail.com", nome);

        foreach (var email in new[] { link, aviso })
        {
            Assert.StartsWith(esperada + "\n", email.Texto.ReplaceLineEndings("\n"));
            Assert.StartsWith($"<p>{esperada}</p>", email.Html);
            Assert.DoesNotContain("Olá, .", email.Texto);
            Assert.DoesNotContain("@", email.Texto.ReplaceLineEndings("\n").Split('\n')[0]);
        }
    }

    // ---------- Sessões JWT depois da troca ----------

    [Fact]
    public async Task Token_Emitido_Antes_Da_Troca_De_Senha_Deixa_De_Valer()
    {
        using var contexto = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        var principal = Principal(usuario.Id, iat: Agora.AddMinutes(-10));

        Assert.True(await ValidacaoSessaoJwt.SessaoValidaAsync(principal, contexto));

        usuario.SenhaAlteradaEm = Agora;
        await contexto.SaveChangesAsync();

        Assert.False(await ValidacaoSessaoJwt.SessaoValidaAsync(principal, contexto));
        Assert.True(await ValidacaoSessaoJwt.SessaoValidaAsync(Principal(usuario.Id, iat: Agora), contexto));
        Assert.True(await ValidacaoSessaoJwt.SessaoValidaAsync(Principal(usuario.Id, iat: Agora.AddMinutes(1)), contexto));
    }

    // Encontrado no teste de ponta a ponta: login e troca no mesmo segundo.
    // O "iat" só tem segundos, então o token desse segundo também cai.
    [Fact]
    public async Task Token_Emitido_No_Mesmo_Segundo_Da_Troca_Tambem_Deixa_De_Valer()
    {
        using var contexto = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        usuario.SenhaAlteradaEm = Agora.AddMilliseconds(500);
        await contexto.SaveChangesAsync();

        Assert.False(await ValidacaoSessaoJwt.SessaoValidaAsync(Principal(usuario.Id, iat: Agora), contexto));
        Assert.True(await ValidacaoSessaoJwt.SessaoValidaAsync(Principal(usuario.Id, iat: Agora.AddSeconds(1)), contexto));
    }

    [Fact]
    public async Task Token_Sem_Iat_Vale_Ate_Expirar_E_Usuario_Inexistente_Nao()
    {
        using var contexto = BancoEmMemoria.NovoContexto();
        var usuario = await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        usuario.SenhaAlteradaEm = Agora;
        await contexto.SaveChangesAsync();

        Assert.True(await ValidacaoSessaoJwt.SessaoValidaAsync(Principal(usuario.Id, iat: null), contexto));
        Assert.False(await ValidacaoSessaoJwt.SessaoValidaAsync(Principal(usuario.Id + 999, iat: null), contexto));
    }

    // O token real do login, lido como o JwtBearer lê, traz o "iat" que a
    // ValidacaoSessaoJwt usa.
    [Fact]
    public async Task Token_Do_Login_Tem_Iat_E_E_Derrubado_Pela_Troca()
    {
        using var contexto = BancoEmMemoria.NovoContexto();
        await NovoUsuario(contexto, "fulano@gmail.com", senha: "senha-antiga");
        var configuracao = BancoEmMemoria.Configuracao();
        var login = await new AuthController(contexto, configuracao)
            .Login(new LoginRequest { Email = "fulano@gmail.com", Senha = "senha-antiga" });
        var token = ((LoginResponse)Assert.IsType<OkObjectResult>(login).Value!).Token;

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuracao["Jwt:ChaveSecreta"]!)),
            ValidateIssuer = false,
            ValidateAudience = false
        }, out _);

        Assert.NotNull(principal.FindFirst(JwtRegisteredClaimNames.Iat));
        Assert.True(await ValidacaoSessaoJwt.SessaoValidaAsync(principal, contexto));

        var usuario = await contexto.Usuarios.SingleAsync();
        usuario.SenhaAlteradaEm = DateTime.UtcNow.AddSeconds(5);
        await contexto.SaveChangesAsync();

        Assert.False(await ValidacaoSessaoJwt.SessaoValidaAsync(principal, contexto));
    }

    // ---------- Apoio ----------

    private static (ApplicationDbContext Contexto, FilaFalsa Fila, ServicoRedefinicaoSenha Servico) Cenario()
    {
        var contexto = BancoEmMemoria.NovoContexto();
        var fila = new FilaFalsa();
        var servico = new ServicoRedefinicaoSenha(contexto, fila, BancoEmMemoria.Configuracao(), NullLogger<ServicoRedefinicaoSenha>.Instance);
        return (contexto, fila, servico);
    }

    private static async Task<Usuario> NovoUsuario(ApplicationDbContext contexto, string email, string? senha)
    {
        var usuario = new Usuario
        {
            Nome = "Fulano", Email = email, Estado = "SP",
            SenhaHash = senha is null ? null : BCrypt.Net.BCrypt.HashPassword(senha),
            GoogleId = senha is null ? "google-sub" : null
        };
        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync();
        return usuario;
    }

    private static string TokenDoEmail(MensagemEmail email)
    {
        const string marcador = "#token=";
        var inicio = email.Texto.IndexOf(marcador, StringComparison.Ordinal) + marcador.Length;
        var fim = email.Texto.IndexOfAny(['\r', '\n', ' '], inicio);
        return email.Texto[inicio..(fim < 0 ? email.Texto.Length : fim)];
    }

    private static System.Security.Claims.ClaimsPrincipal Principal(int usuarioId, DateTime? iat)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, usuarioId.ToString())
        };
        if (iat is not null)
        {
            claims.Add(new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(iat.Value).ToUnixTimeSeconds().ToString()));
        }
        return new(new System.Security.Claims.ClaimsIdentity(claims, "teste"));
    }

    private static bool Valida(object dto) =>
        Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), validateAllProperties: true);

    private static string? Mensagem(IActionResult resultado) => Campo(resultado, "mensagem");

    private static string? Campo(IActionResult resultado, string nome)
    {
        var valor = ((ObjectResult)resultado).Value!;
        return valor.GetType().GetProperty(nome)?.GetValue(valor) as string;
    }

    private sealed class FilaFalsa : IFilaEmail
    {
        public List<MensagemEmail> Mensagens { get; } = new();

        public bool Enfileirar(MensagemEmail mensagem)
        {
            Mensagens.Add(mensagem);
            return true;
        }
    }
}
