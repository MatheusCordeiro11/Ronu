using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;
using Ronu.Api.Services.PlanoFamilia;
using Ronu.Api.Services.Troca;
using Ronu.Api.Validacao;

namespace Ronu.Api.Controllers;

/// <summary>
/// Plano família: a casa, os convites, os membros e a transferência (etapa 1), a semana da casa (etapa 4) e os pedidos,
/// a troca na casa e a entrada e a saída no meio da semana (etapa 5). A lista de compras da casa (etapa 6) fica em
/// CasaController.ListaCompras.cs.
///
/// - Quem não está numa casa recebe 404 em toda rota de membro, até numa casa que existe: a resposta não revela casa
///   nenhuma. Membro em rota de dono recebe 403.
/// - As rotas que mostram a casa exigem também o aceite "casa" vigente (409 consentimento_casa). Sair e recusar a
///   transferência não: deixar de dividir os dados não pode depender de aceitar dividir.
/// - Sobre os outros, a resposta leva só o primeiro nome e o papel (CasaResponse). Nenhuma porção, meta, restrição
///   com dono ou dado de saúde de outro membro sai daqui; os pedidos não dizem quem pediu.
/// </summary>
[ApiController]
[Route("api/casa")]
[Authorize]
public partial class CasaController : ControllerBase
{
    private const string MensagemSemCasa = "Você não está em nenhuma casa.";
    private const string MensagemSoDono = "Só quem cuida da casa pode fazer isso.";
    private const string MensagemJaEmCasa = "Você já está numa casa. Para entrar em outra, saia dela antes.";
    private const string MensagemCasaCheia = "Esta casa já tem 6 pessoas.";
    private const string MensagemConviteInvalido = "Este convite não vale mais. Peça um link novo a quem convidou.";
    private const string MensagemConviteUsado = "Este convite já foi usado. Peça um link novo a quem convidou.";
    private const string MensagemConviteExpirado = "Este convite venceu. Peça um link novo a quem convidou.";
    private const string MensagemCadastroIncompleto = "Complete seu perfil (data de nascimento, altura, sexo e objetivo) antes de entrar numa casa.";
    private const string MensagemTransferirAntes = "Passe a casa para outra pessoa antes de sair.";
    private const string MensagemRemoverASiMesmo = "Para sair da casa, use a opção Sair.";
    private const string MensagemTransferirASiMesmo = "Escolha outra pessoa da casa.";
    private const string MensagemSemTransferencia = "Não há nenhuma casa sendo passada para você.";
    private const string MensagemMembroNaoEncontrado = "Pessoa não encontrada na casa.";
    private const string MensagemConviteNaoEncontrado = "Convite não encontrado.";
    public const string MensagemSemSemana = "A casa ainda não tem uma semana gerada.";

    public const string MotivoInvalido = "invalido";
    public const string MotivoUsado = "usado";
    public const string MotivoExpirado = "expirado";
    public const string MotivoJaEmCasa = "ja_em_casa";
    public const string MotivoCadastroIncompleto = "cadastro_incompleto";
    public const string MotivoMenorDe18 = "menor_de_18";
    public const string MotivoCasaCheia = "casa_cheia";
    public const string MotivoTransferirAntes = "transferir_antes";

    private readonly ApplicationDbContext _context;
    private readonly string _urlBaseFrontend;
    private readonly ILogger<CasaController>? _logger;

    // O logger é opcional só para os testes, que montam o controller sem ele.
    public CasaController(ApplicationDbContext context, IConfiguration configuration, ILogger<CasaController>? logger = null)
    {
        _context = context;
        _logger = logger;
        // Da configuração, nunca dos cabeçalhos da requisição: um Host forjado mandaria o link para outro site.
        _urlBaseFrontend = (configuration["Frontend:UrlBase"]
            ?? throw new InvalidOperationException("Frontend:UrlBase não configurado.")).TrimEnd('/');
    }

    // Do token, nunca do corpo nem da rota.
    private int UsuarioIdLogado =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ---------- Casa ----------

    [HttpGet]
    public async Task<IActionResult> Obter()
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: false, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        return Ok(await MontarRespostaAsync(eu!, DateTime.UtcNow));
    }

    /// <summary>Cria a casa com a pessoa como dono, e grava o aceite "casa" junto (não há outro caminho para ele).</summary>
    [HttpPost]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> Criar(CriarCasaRequest request)
    {
        if (!RegrasOrcamento.Valido(request.Orcamento))
        {
            return BadRequest(new { mensagem = RegrasOrcamento.MensagemInvalido });
        }

        if (await _context.MembrosCasa.AnyAsync(m => m.UsuarioId == UsuarioIdLogado))
        {
            return Conflict(new { mensagem = MensagemJaEmCasa, motivo = MotivoJaEmCasa });
        }

        if (ErroVersaoConsentimento(request.VersaoConsentimento) is { } erroConsentimento)
        {
            return erroConsentimento;
        }

        var agora = DateTime.UtcNow;
        var casa = new Casa
        {
            OrcamentoSemanal = string.IsNullOrEmpty(request.Orcamento) ? null : request.Orcamento,
            CriadaEm = agora
        };
        _context.Casas.Add(casa);
        _context.MembrosCasa.Add(new MembroCasa { Casa = casa, UsuarioId = UsuarioIdLogado, Papel = MembroCasa.PapelDono, EntrouEm = agora });
        await AdicionarAceiteAsync(agora);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (OperacoesCasa.EhChaveDuplicada(ex))
        {
            // Duas abas criando ao mesmo tempo, ou um convite aceito no meio: o índice único (uma casa por pessoa)
            // barrou esta.
            return Conflict(new { mensagem = MensagemJaEmCasa, motivo = MotivoJaEmCasa });
        }

        var eu = await _context.MembrosCasa.AsNoTracking().FirstAsync(m => m.UsuarioId == UsuarioIdLogado);
        return Created("/api/casa", await MontarRespostaAsync(eu, agora));
    }

    /// <summary>
    /// O orçamento da casa vale só para o almoço e o jantar da casa: vai ao prompt da semana da casa (e à regra do custo
    /// dela). O café e os lanches de cada um seguem o orçamento do perfil da pessoa (Usuario.OrcamentoSemanal), na
    /// geração individual. Só o dono muda; todos veem, só para ler, no CasaResponse.
    /// </summary>
    [HttpPut("orcamento")]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> AtualizarOrcamento(OrcamentoCasaRequest request)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        if (!RegrasOrcamento.Valido(request.Orcamento))
        {
            return BadRequest(new { mensagem = RegrasOrcamento.MensagemInvalido });
        }

        var casa = await _context.Casas.FirstAsync(c => c.Id == eu!.CasaId);
        casa.OrcamentoSemanal = string.IsNullOrEmpty(request.Orcamento) ? null : request.Orcamento;
        await _context.SaveChangesAsync();

        return Ok(await MontarRespostaAsync(eu!, DateTime.UtcNow));
    }

    // ---------- Convites ----------

    /// <summary>
    /// Um link novo, válido por 7 dias. O token aparece só nesta resposta; o banco guarda o SHA-256. Vai no fragmento
    /// (#), que não chega a servidor nenhum nem ao Referer. Gerar outro não invalida os anteriores.
    /// </summary>
    [HttpPost("convites")]
    [EnableRateLimiting(LimitesCasa.PoliticaConvite)]
    public async Task<IActionResult> CriarConvite()
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        if (await _context.MembrosCasa.CountAsync(m => m.CasaId == eu!.CasaId) >= OperacoesCasa.MaximoMembros)
        {
            return Conflict(new { mensagem = MensagemCasaCheia, motivo = MotivoCasaCheia });
        }

        var agora = DateTime.UtcNow;
        var token = OperacoesCasa.NovoToken();
        var convite = new ConviteCasa
        {
            CasaId = eu!.CasaId,
            UsuarioId = UsuarioIdLogado,
            TokenHash = OperacoesCasa.Hash(token),
            CriadoEm = agora,
            ExpiraEm = agora + OperacoesCasa.ValidadeConvite
        };
        _context.ConvitesCasa.Add(convite);
        await _context.SaveChangesAsync();

        return Created("/api/casa", new ConviteCriadoResponse
        {
            Link = $"{_urlBaseFrontend}/casa-convite.html#convite={token}",
            ExpiraEm = convite.ExpiraEm
        });
    }

    /// <summary>Revoga um convite da casa. Repetir (ou revogar um já usado) não é erro e não muda nada.</summary>
    [HttpDelete("convites/{id:int}")]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> RevogarConvite(int id)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        // Pela casa, e não só pelo id: o id de um convite de outra casa dá 404, igual a um que não existe.
        var convite = await _context.ConvitesCasa.FirstOrDefaultAsync(c => c.Id == id && c.CasaId == eu!.CasaId);
        if (convite is null)
        {
            return NotFound(new { mensagem = MensagemConviteNaoEncontrado });
        }

        if (convite.UsadoEm is null && convite.RevogadoEm is null)
        {
            convite.RevogadoEm = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>
    /// Confere o convite antes de mostrar a caixa do aceite. Na ordem: invalido, usado, expirado (400); ja_em_casa
    /// (409); cadastro_incompleto (409); menor_de_18 (403); casa_cheia (409). Quem ainda não entrou vê só o primeiro
    /// nome do dono e quantas pessoas a casa tem.
    /// </summary>
    [HttpPost("convites/verificar")]
    [EnableRateLimiting(LimitesCasa.PoliticaConviteVerificar)]
    public async Task<IActionResult> VerificarConvite(VerificarConviteRequest request)
    {
        var conferencia = await ConferirConviteAsync(request.Token, DateTime.UtcNow);
        if (conferencia.Erro is not null)
        {
            return conferencia.Erro;
        }

        var tipo = TextosConsentimento.TipoCasa;
        var texto = TextosConsentimento.TextoVigente(tipo);
        return Ok(new ConviteVerificadoResponse
        {
            PrimeiroNomeDono = conferencia.PrimeiroNomeDono!,
            Membros = conferencia.Membros,
            Consentimento = new ConsentimentoTextoResponse
            {
                Tipo = tipo,
                Versao = TextosConsentimento.VersaoVigente(tipo),
                Titulo = texto.Titulo,
                Texto = texto.Corpo
            }
        });
    }

    /// <summary>
    /// Entra na casa: as mesmas checagens da verificação, refeitas com a casa travada, e o aceite, o membro e o UsadoEm
    /// gravados numa transação. O uso único é um UPDATE condicional (OperacoesCasa.UsarConviteAsync): de dois aceites
    /// ao mesmo tempo, só um passa.
    /// </summary>
    /// <remarks>
    /// Etapa 5: depois do aceite gravado, a pessoa recebe na hora as porções da semana atual da casa, sem chamar o Gemini
    /// (GeracaoSemanaCasa.EntrarNaSemanaAsync). O parâmetro é opcional só para os testes da etapa 1, que montam o
    /// controller sem os serviços; na API ele sempre vem.
    /// </remarks>
    [HttpPost("convites/aceitar")]
    [EnableRateLimiting(LimitesCasa.PoliticaConviteVerificar)]
    public async Task<IActionResult> AceitarConvite(AceitarConviteRequest request, [FromServices] GeracaoSemanaCasa? geracao = null)
    {
        var agora = DateTime.UtcNow;

        // Primeiro sem trava, para responder logo o convite que não serve e descobrir qual casa travar.
        var previa = await ConferirConviteAsync(request.Token, agora);
        if (previa.Erro is not null)
        {
            return previa.Erro;
        }

        if (ErroVersaoConsentimento(request.VersaoConsentimento) is { } erroConsentimento)
        {
            return erroConsentimento;
        }

        await using var transacao = await OperacoesCasa.IniciarTransacaoAsync(_context);
        await OperacoesCasa.TravarAsync(_context, previa.Convite!.CasaId);

        // De novo, com a casa travada: outro aceite pode ter enchido a casa ou usado o convite entre as duas leituras.
        var conferencia = await ConferirConviteAsync(request.Token, agora);
        if (conferencia.Erro is not null)
        {
            return conferencia.Erro;
        }

        if (!await OperacoesCasa.UsarConviteAsync(_context, conferencia.Convite!.Id, agora))
        {
            return BadRequest(new { mensagem = MensagemConviteUsado, motivo = MotivoUsado });
        }

        _context.MembrosCasa.Add(new MembroCasa
        {
            CasaId = conferencia.Convite.CasaId, UsuarioId = UsuarioIdLogado, Papel = MembroCasa.PapelMembro, EntrouEm = agora
        });
        await AdicionarAceiteAsync(agora);

        try
        {
            await _context.SaveChangesAsync();
            if (transacao is not null)
            {
                await transacao.CommitAsync();
            }
        }
        catch (DbUpdateException ex) when (OperacoesCasa.EhChaveDuplicada(ex))
        {
            // Dois convites de casas diferentes aceitos ao mesmo tempo pela mesma pessoa: o índice único (uma casa por
            // pessoa) barrou este, e a transação desfaz o UsadoEm, para o convite continuar servindo a outra pessoa.
            return Conflict(new { mensagem = MensagemJaEmCasa, motivo = MotivoJaEmCasa });
        }

        var eu = await _context.MembrosCasa.AsNoTracking().FirstAsync(m => m.UsuarioId == UsuarioIdLogado);
        if (geracao is not null)
        {
            try
            {
                await geracao.EntrarNaSemanaAsync(eu.CasaId, UsuarioIdLogado);
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException or KeyNotFoundException)
            {
                // A entrada na casa já está gravada e vale; só as porções desta semana não saíram. A pessoa gera o café e
                // os lanches (que trazem o almoço e o jantar da casa) pelo painel, como sempre. Só o id da casa no log.
                _logger?.LogWarning(ex, "Porções da semana da casa {CasaId} não calculadas na entrada de um membro", eu.CasaId);
            }
        }

        return Ok(await MontarRespostaAsync(eu, agora));
    }

    // ---------- Membros ----------

    /// <summary>O dono tira alguém da casa. O aceite "casa" da pessoa é revogado, como quando ela sai.</summary>
    [HttpDelete("membros/{membroId:int}")]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> RemoverMembro(int membroId)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        if (membroId == eu!.Id)
        {
            return BadRequest(new { mensagem = MensagemRemoverASiMesmo });
        }

        var membro = await _context.MembrosCasa.FirstOrDefaultAsync(m => m.Id == membroId && m.CasaId == eu.CasaId);
        if (membro is null)
        {
            return NotFound(new { mensagem = MensagemMembroNaoEncontrado });
        }

        await OperacoesCasa.SairAsync(_context, membro, DateTime.UtcNow);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Sair da casa. O membro sai; o dono só sai se estiver sozinho (e aí a casa acaba). Dono com membros: 409, para
    /// passar a casa antes. Dispensa os dois consentimentos: sair é o "desfazer" da autorização (art. 18, IX).
    /// </summary>
    [HttpDelete("eu")]
    [DispensaConsentimento]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> Sair()
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: false, exigirConsentimento: false, rastrear: true);
        if (erro is not null)
        {
            return erro;
        }

        await using var transacao = await OperacoesCasa.IniciarTransacaoAsync(_context);
        if (eu!.Papel == MembroCasa.PapelDono)
        {
            // Com a casa travada, para um convite aceito agora não ficar numa casa apagada.
            await OperacoesCasa.TravarAsync(_context, eu.CasaId);
            if (await _context.MembrosCasa.AnyAsync(m => m.CasaId == eu.CasaId && m.Id != eu.Id))
            {
                return Conflict(new { mensagem = MensagemTransferirAntes, motivo = MotivoTransferirAntes });
            }
        }

        await OperacoesCasa.SairAsync(_context, eu, DateTime.UtcNow);
        await _context.SaveChangesAsync();
        if (transacao is not null)
        {
            await transacao.CommitAsync();
        }

        return NoContent();
    }

    // ---------- Transferência ----------

    /// <summary>O dono oferece a casa a um membro. Uma oferta por vez: oferecer a outro desfaz a anterior.</summary>
    [HttpPost("transferencia")]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> OferecerTransferencia(TransferenciaCasaRequest request)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        if (request.MembroId == eu!.Id)
        {
            return BadRequest(new { mensagem = MensagemTransferirASiMesmo });
        }

        var membros = await _context.MembrosCasa.Where(m => m.CasaId == eu.CasaId).ToListAsync();
        var escolhido = membros.FirstOrDefault(m => m.Id == request.MembroId);
        if (escolhido is null)
        {
            return NotFound(new { mensagem = MensagemMembroNaoEncontrado });
        }

        var agora = DateTime.UtcNow;
        foreach (var membro in membros)
        {
            membro.TransferenciaOferecidaEm = membro.Id == escolhido.Id ? agora : null;
        }
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// O membro escolhido aceita a casa: ele vira dono e o antigo dono vira membro. Em dois SaveChanges, dentro de uma
    /// transação: o índice de um dono por casa recusaria o novo dono enquanto o antigo ainda é dono.
    /// </summary>
    [HttpPost("transferencia/aceitar")]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> AceitarTransferencia()
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: false, exigirConsentimento: true, rastrear: true);
        if (erro is not null)
        {
            return erro;
        }

        if (eu!.TransferenciaOferecidaEm is null)
        {
            return Conflict(new { mensagem = MensagemSemTransferencia });
        }

        await using var transacao = await OperacoesCasa.IniciarTransacaoAsync(_context);
        await OperacoesCasa.TravarAsync(_context, eu.CasaId);

        // De novo, com a casa travada: o dono pode ter cancelado a oferta (ou oferecido a outro) entre a primeira leitura
        // e a trava. Sem isto, um aceite atrasado passaria por cima do cancelamento. Só no PostgreSQL: no banco em
        // memória dos testes não há trava nem leitura concorrente.
        if (_context.Database.IsRelational())
        {
            await _context.Entry(eu).ReloadAsync();
            if (eu.TransferenciaOferecidaEm is null)
            {
                return Conflict(new { mensagem = MensagemSemTransferencia });
            }
        }

        var dono = await _context.MembrosCasa.FirstOrDefaultAsync(m => m.CasaId == eu.CasaId && m.Papel == MembroCasa.PapelDono);
        if (dono is not null)
        {
            dono.Papel = MembroCasa.PapelMembro;
            await _context.SaveChangesAsync();
        }

        eu.Papel = MembroCasa.PapelDono;
        eu.TransferenciaOferecidaEm = null;
        await _context.SaveChangesAsync();
        if (transacao is not null)
        {
            await transacao.CommitAsync();
        }

        return Ok(await MontarRespostaAsync(eu, DateTime.UtcNow));
    }

    /// <summary>O dono cancela a oferta, ou o escolhido recusa. Sem oferta, não muda nada.</summary>
    [HttpDelete("transferencia")]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> CancelarTransferencia()
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: false, exigirConsentimento: false, rastrear: true);
        if (erro is not null)
        {
            return erro;
        }

        if (eu!.Papel == MembroCasa.PapelDono)
        {
            foreach (var oferecido in await _context.MembrosCasa.Where(m => m.CasaId == eu.CasaId && m.TransferenciaOferecidaEm != null).ToListAsync())
            {
                oferecido.TransferenciaOferecidaEm = null;
            }
        }
        else if (eu.TransferenciaOferecidaEm is not null)
        {
            eu.TransferenciaOferecidaEm = null;
        }
        else
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { mensagem = MensagemSoDono });
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Semana da casa (etapa 4) ----------

    /// <summary>
    /// Só o dono gera a semana da casa: uma chamada ao Gemini para o almoço e o jantar dos 7 dias, e uma dieta nova para
    /// cada membro, com a porção dele. 400 com papel do prato vazio (sem chamar o Gemini) ou perfil do dono incompleto;
    /// 429 no limite de 3 por hora do dono; 503 se o Gemini falhar.
    /// </summary>
    [HttpPost("semana")]
    public async Task<IActionResult> GerarSemana([FromServices] GeracaoSemanaCasa geracao)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        var resultado = await geracao.GerarAsync(eu!.CasaId, UsuarioIdLogado);
        if (resultado.Falha is { } falha)
        {
            return StatusCode(falha.Status, new { mensagem = falha.Mensagem });
        }

        return Ok(SemanaCasaResponse.Montar(resultado.Semana!, souDono: true));
    }

    /// <summary>A semana atual da casa, para qualquer membro: só os nomes dos alimentos (a porção está na dieta de cada um).</summary>
    [HttpGet("semana")]
    public async Task<IActionResult> ObterSemana()
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: false, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        var semana = await GeracaoSemanaCasa.AtualAsync(_context, eu!.CasaId);
        if (semana is null)
        {
            return NotFound(new { mensagem = MensagemSemSemana });
        }

        return Ok(SemanaCasaResponse.Montar(semana, souDono: eu.Papel == MembroCasa.PapelDono));
    }

    // ---------- Pedidos (etapa 5) ----------

    /// <summary>
    /// Um membro pede uma semana nova ou a troca de um item de uma refeição da casa. O pedido não guarda nem devolve
    /// quem pediu. 202 também quando um pedido igual já estava aberto (nada muda): quem pede não descobre se outra
    /// pessoa já pediu. O dono não pede (ele gera ou troca direto).
    /// </summary>
    [HttpPost("pedidos")]
    [EnableRateLimiting(LimitesCasa.PoliticaPedido)]
    public async Task<IActionResult> Pedir(PedidoCasaRequest request)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: false, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        if (eu!.Papel == MembroCasa.PapelDono)
        {
            return BadRequest(new { mensagem = RegrasPedidosCasa.MensagemDono });
        }

        if (await RegrasPedidosCasa.PedirAsync(_context, eu.CasaId, request, DateTime.UtcNow) is { } falha)
        {
            return StatusCode(falha.Status, new { mensagem = falha.Mensagem });
        }

        await _context.SaveChangesAsync();
        var semana = await GeracaoSemanaCasa.AtualAsync(_context, eu.CasaId);
        return Accepted(new PedidoEnviadoResponse { SemanaId = semana?.Id });
    }

    /// <summary>O dono dispensa um pedido ou aviso. Apagado, sem histórico. O id de outra casa dá 404.</summary>
    [HttpDelete("pedidos/{id:int}")]
    [EnableRateLimiting(LimitesCasa.PoliticaEscrita)]
    public async Task<IActionResult> DispensarPedido(int id)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        var pedido = await _context.PedidosCasa.FirstOrDefaultAsync(p => p.Id == id && p.CasaId == eu!.CasaId);
        if (pedido is null)
        {
            return NotFound(new { mensagem = RegrasPedidosCasa.MensagemNaoEncontrado });
        }

        _context.PedidosCasa.Remove(pedido);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Troca na casa (etapa 5) ----------

    /// <summary>
    /// Os substitutos de um item da casa, só para o dono, com as quantidades da porção dele. Um substituto que não
    /// serve para a porção de alguém aparece só na contagem "naoServeParaACasa".
    /// </summary>
    [HttpGet("semana/trocas/opcoes")]
    [EnableRateLimiting(LimitesTroca.PoliticaLeitura)]
    public async Task<IActionResult> OpcoesTroca([FromQuery] string? chaveCasa, [FromQuery] int item)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        var (carga, falha) = await TrocaNaCasa.CarregarAsync(_context, eu!.CasaId, UsuarioIdLogado, rastrear: false);
        if (falha is not null)
        {
            return Falha(falha);
        }

        if (TrocaNaCasa.Posicao(carga!.Referencia, chaveCasa, item) is not { } posicao)
        {
            return BadRequest(new { mensagem = TrocaNaCasa.MensagemItem });
        }

        return Ok(TrocaNaCasa.Opcoes(carga, posicao));
    }

    /// <summary>O dono troca um item da casa: vale para todos, e a porção de cada um é refeita.</summary>
    [HttpPost("semana/trocas")]
    [EnableRateLimiting(LimitesTroca.PoliticaEscrita)]
    public async Task<IActionResult> TrocarNaCasa(TrocaCasaRequest request)
    {
        if (LerEscopo(request.Escopo) is not { } escopo)
        {
            return BadRequest(new { mensagem = "Escopo inválido." });
        }

        var (carga, posicao, erro) = await CarregarTrocaAsync(request.SemanaId!.Value, request.Revisao!.Value, request.ChaveCasa, request.Item!.Value);
        if (erro is not null)
        {
            return erro;
        }

        // A posição pode ter outro alimento se a tela estava desatualizada: confere o item, não só o índice.
        var atual = CalculadoraTroca.Encontrar(carga!.Referencia.Dias, posicao!)!;
        if (atual.AlimentoId != request.AlimentoIdAtual)
        {
            return Conflict(new { mensagem = TrocaNaCasa.MensagemSemanaMudou });
        }

        if (TrocaNaCasa.Trocar(carga, posicao!, request.AlimentoIdNovo!, escopo, out var chaves) is { } recusa)
        {
            return Falha(recusa);
        }

        if (await TrocaNaCasa.GravarAsync(_context, carga, chaves, atual.AlimentoId) is { } conflito)
        {
            return Falha(conflito);
        }

        // Só ids e contagens: nada de nenhum membro no log.
        _logger?.LogInformation("Troca na casa {CasaId}: '{De}' por '{Para}', {Escopo}, {Refeicoes} refeições, {Dietas} dietas refeitas",
            carga.Linha.CasaId, atual.AlimentoId, request.AlimentoIdNovo, escopo, chaves.Count, carga.Membros.Count);
        return Ok(SemanaCasaResponse.Montar(carga.Linha, souDono: true));
    }

    /// <summary>"Desfazer" da casa: volta ao que estava antes da última troca ou volta ao original.</summary>
    [HttpDelete("semana/trocas/ultima")]
    [EnableRateLimiting(LimitesTroca.PoliticaEscrita)]
    public async Task<IActionResult> DesfazerNaCasa([FromQuery] int semanaId, [FromQuery] int revisao)
    {
        var (carga, _, erro) = await CarregarTrocaAsync(semanaId, revisao, chaveCasa: null, item: 0);
        if (erro is not null)
        {
            return erro;
        }

        if (TrocaNaCasa.Desfazer(carga!) is { } recusa)
        {
            return Falha(recusa);
        }

        if (await TrocaNaCasa.GravarAsync(_context, carga!, [], null) is { } conflito)
        {
            return Falha(conflito);
        }

        return Ok(SemanaCasaResponse.Montar(carga!.Linha, souDono: true));
    }

    /// <summary>"Voltar ao original" da casa: o item (ou, na semana, todos os trocados do mesmo jeito) volta ao primeiro original.</summary>
    [HttpDelete("semana/trocas")]
    [EnableRateLimiting(LimitesTroca.PoliticaEscrita)]
    public async Task<IActionResult> VoltarAoOriginalNaCasa(
        [FromQuery] int semanaId, [FromQuery] int revisao, [FromQuery] string? chaveCasa, [FromQuery] int item, [FromQuery] string? escopo)
    {
        if (LerEscopo(escopo) is not { } escopoTroca)
        {
            return BadRequest(new { mensagem = "Escopo inválido." });
        }

        if (string.IsNullOrEmpty(chaveCasa))
        {
            return BadRequest(new { mensagem = TrocaNaCasa.MensagemItem });
        }

        var (carga, posicao, erro) = await CarregarTrocaAsync(semanaId, revisao, chaveCasa, item);
        if (erro is not null)
        {
            return erro;
        }

        if (TrocaNaCasa.VoltarAoOriginal(carga!, posicao!, escopoTroca) is { } recusa)
        {
            return Falha(recusa);
        }

        if (await TrocaNaCasa.GravarAsync(_context, carga!, [], null) is { } conflito)
        {
            return Falha(conflito);
        }

        return Ok(SemanaCasaResponse.Montar(carga!.Linha, souDono: true));
    }

    // Dono, aceite "casa", a semana atual com a revisão que a tela viu e, com chaveCasa, a posição do item.
    private async Task<(TrocaNaCasa.Carga? Carga, PosicaoItem? Posicao, IActionResult? Erro)> CarregarTrocaAsync(
        int semanaId, int revisao, string? chaveCasa, int item)
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: true, exigirConsentimento: true);
        if (erro is not null)
        {
            return (null, null, erro);
        }

        var (carga, falha) = await TrocaNaCasa.CarregarAsync(_context, eu!.CasaId, UsuarioIdLogado, rastrear: true);
        if (falha is not null)
        {
            return (null, null, Falha(falha));
        }

        if (carga!.Linha.Id != semanaId || carga.Linha.Revisao != revisao)
        {
            return (null, null, Conflict(new { mensagem = TrocaNaCasa.MensagemSemanaMudou }));
        }

        if (chaveCasa is null)
        {
            return (carga, null, null);
        }

        return TrocaNaCasa.Posicao(carga.Referencia, chaveCasa, item) is { } posicao
            ? (carga, posicao, null)
            : (null, null, BadRequest(new { mensagem = TrocaNaCasa.MensagemItem }));
    }

    private ObjectResult Falha(TrocaNaCasa.Erro erro) => StatusCode(erro.Status, erro.Motivo is null
        ? new { mensagem = erro.Mensagem }
        : (object)new { mensagem = erro.Mensagem, motivo = erro.Motivo });

    private static EscopoTroca? LerEscopo(string? escopo) => escopo switch
    {
        "refeicao" => EscopoTroca.Refeicao,
        "semana" => EscopoTroca.Semana,
        _ => null
    };

    // ---------- Apoio ----------

    /// <summary>
    /// A linha da pessoa na casa, ou a resposta de erro: 404 sem casa, 403 se a rota é de dono, 409 sem o aceite "casa"
    /// vigente (com a versão, para a tela pedir o aceite de novo).
    /// </summary>
    private async Task<(MembroCasa? Membro, IActionResult? Erro)> MembroLogadoAsync(bool exigirDono, bool exigirConsentimento, bool rastrear = false)
    {
        var consulta = rastrear ? _context.MembrosCasa : _context.MembrosCasa.AsNoTracking();
        var membro = await consulta.FirstOrDefaultAsync(m => m.UsuarioId == UsuarioIdLogado);
        if (membro is null)
        {
            return (null, NotFound(new { mensagem = MensagemSemCasa }));
        }

        if (exigirDono && membro.Papel != MembroCasa.PapelDono)
        {
            return (null, StatusCode(StatusCodes.Status403Forbidden, new { mensagem = MensagemSoDono }));
        }

        if (exigirConsentimento && !await RegrasConsentimento.TemAceiteVigenteAsync(_context, UsuarioIdLogado, TextosConsentimento.TipoCasa))
        {
            return (null, ConflitoConsentimento(RegrasConsentimento.MensagemSemAceiteCasa));
        }

        return (membro, null);
    }

    // Ausente: a tela ainda não mostrou a caixa. Diferente da vigente: a tela mostrou um texto velho.
    private IActionResult? ErroVersaoConsentimento(int? versao)
    {
        if (versao is null)
        {
            return ConflitoConsentimento(RegrasConsentimento.MensagemSemAceiteCasa);
        }

        return versao != TextosConsentimento.VersaoVigente(TextosConsentimento.TipoCasa)
            ? ConflitoConsentimento(RegrasConsentimento.MensagemVersaoMudou)
            : null;
    }

    private ConflictObjectResult ConflitoConsentimento(string mensagem) => Conflict(new
    {
        mensagem,
        motivo = RegrasConsentimento.MotivoSemAceiteCasa,
        versaoVigente = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoCasa)
    });

    // Sair revoga o aceite, então quem entra de novo sempre grava um novo; a checagem só evita bater no índice único
    // se sobrar um aceite vigente de algum caminho antigo.
    private async Task AdicionarAceiteAsync(DateTime agora)
    {
        if (!await RegrasConsentimento.TemAceiteVigenteAsync(_context, UsuarioIdLogado, TextosConsentimento.TipoCasa))
        {
            _context.Consentimentos.Add(new Consentimento
            {
                UsuarioId = UsuarioIdLogado,
                Tipo = TextosConsentimento.TipoCasa,
                Versao = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoCasa),
                AceitoEm = agora
            });
        }
    }

    private record ConferenciaConvite(IActionResult? Erro, ConviteCasa? Convite = null, string? PrimeiroNomeDono = null, int Membros = 0);

    /// <summary>A ordem das checagens do convite, a mesma na verificação e no aceite. Só leitura, sem rastrear.</summary>
    private async Task<ConferenciaConvite> ConferirConviteAsync(string? token, DateTime agora)
    {
        // Um token nosso tem 43 caracteres (32 bytes em Base64Url); mais que isso nem vai ao banco.
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
        {
            return new(BadRequest(new { mensagem = MensagemConviteInvalido, motivo = MotivoInvalido }));
        }

        var hash = OperacoesCasa.Hash(token.Trim());
        var convite = await _context.ConvitesCasa.AsNoTracking().FirstOrDefaultAsync(c => c.TokenHash == hash);

        // Revogado conta como inexistente: o dono desfez o link, e quem o tem não precisa saber mais que isso.
        if (convite is null || convite.RevogadoEm is not null)
        {
            return new(BadRequest(new { mensagem = MensagemConviteInvalido, motivo = MotivoInvalido }));
        }

        if (convite.UsadoEm is not null)
        {
            return new(BadRequest(new { mensagem = MensagemConviteUsado, motivo = MotivoUsado }));
        }

        if (convite.ExpiraEm <= agora)
        {
            return new(BadRequest(new { mensagem = MensagemConviteExpirado, motivo = MotivoExpirado }));
        }

        // Inclusive na mesma casa: o dono abrindo o próprio link cai aqui.
        if (await _context.MembrosCasa.AnyAsync(m => m.UsuarioId == UsuarioIdLogado))
        {
            return new(Conflict(new { mensagem = MensagemJaEmCasa, motivo = MotivoJaEmCasa }));
        }

        // Sem esses dados a porção da pessoa não tem como ser calculada (e sem a data, a idade não é conhecida).
        var usuario = await _context.Usuarios.AsNoTracking()
            .Where(u => u.Id == UsuarioIdLogado)
            .Select(u => new { u.DataNascimento, u.Altura, u.Sexo })
            .FirstAsync();
        var temObjetivo = await _context.ObjetivosUsuario.AnyAsync(o => o.UsuarioId == UsuarioIdLogado);
        if (usuario.DataNascimento is null || usuario.Altura is null || string.IsNullOrEmpty(usuario.Sexo) || !temObjetivo)
        {
            return new(Conflict(new { mensagem = MensagemCadastroIncompleto, motivo = MotivoCadastroIncompleto }));
        }

        // A mesma regra do perfil (decisão de 2026-10-17: o Ronu é para adultos). Uma data no futuro só existe em
        // dado antigo, e também não é de um adulto.
        var erroIdade = RegrasDadosCorporais.ValidarNascimento(usuario.DataNascimento.Value, DateOnly.FromDateTime(agora));
        if (erroIdade is RegrasDadosCorporais.MensagemIdadeMinima or RegrasDadosCorporais.MensagemNascimentoFuturo)
        {
            return new(StatusCode(StatusCodes.Status403Forbidden, new { mensagem = RegrasDadosCorporais.MensagemIdadeMinima, motivo = MotivoMenorDe18 }));
        }

        var membros = await _context.MembrosCasa.CountAsync(m => m.CasaId == convite.CasaId);
        if (membros >= OperacoesCasa.MaximoMembros)
        {
            return new(Conflict(new { mensagem = MensagemCasaCheia, motivo = MotivoCasaCheia }));
        }

        var nomeDono = await _context.MembrosCasa.AsNoTracking()
            .Where(m => m.CasaId == convite.CasaId && m.Papel == MembroCasa.PapelDono)
            .Select(m => m.Usuario.Nome)
            .FirstOrDefaultAsync();

        return new(null, convite, OperacoesCasa.PrimeiroNome(nomeDono), membros);
    }

    private async Task<CasaResponse> MontarRespostaAsync(MembroCasa eu, DateTime agora)
    {
        var casa = await _context.Casas.AsNoTracking().FirstAsync(c => c.Id == eu.CasaId);
        var membros = await _context.MembrosCasa.AsNoTracking()
            .Where(m => m.CasaId == eu.CasaId)
            .OrderBy(m => m.EntrouEm).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.UsuarioId, m.Papel, m.TransferenciaOferecidaEm, m.Usuario.Nome })
            .ToListAsync();
        var souDono = membros.Any(m => m.UsuarioId == UsuarioIdLogado && m.Papel == MembroCasa.PapelDono);

        List<ConviteAbertoResponse>? convites = null;
        List<PedidoCasaResponse>? pedidos = null;
        if (souDono)
        {
            pedidos = await RegrasPedidosCasa.AbertosAsync(_context, eu.CasaId);
            convites = await _context.ConvitesCasa.AsNoTracking()
                .Where(c => c.CasaId == eu.CasaId && c.UsadoEm == null && c.RevogadoEm == null && c.ExpiraEm > agora)
                .OrderBy(c => c.CriadoEm)
                .Select(c => new ConviteAbertoResponse { Id = c.Id, ExpiraEm = c.ExpiraEm })
                .ToListAsync();
        }

        return new CasaResponse
        {
            SouDono = souDono,
            Orcamento = casa.OrcamentoSemanal,
            ConvitesAbertos = convites,
            Pedidos = pedidos,
            PedidosAbertos = pedidos?.Count,
            Membros = membros.Select(m => new MembroCasaResponse
            {
                MembroId = m.Id,
                PrimeiroNome = OperacoesCasa.PrimeiroNome(m.Nome),
                Dono = m.Papel == MembroCasa.PapelDono,
                SouEu = m.UsuarioId == UsuarioIdLogado,
                TransferenciaPendente = m.TransferenciaOferecidaEm is not null
            }).ToList()
        };
    }
}
