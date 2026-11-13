using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services;
using Ronu.Api.Services.Consentimentos;
using Ronu.Api.Services.IA;
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
        HorarioAcordar = RegrasHorario.Formatar(usuario.HorarioAcordar),
        HorarioDormir = RegrasHorario.Formatar(usuario.HorarioDormir),
        Nome = usuario.Nome,
        Email = usuario.Email,
        // A coluna guarda "" quando não há UF; a resposta devolve null.
        Estado = string.IsNullOrWhiteSpace(usuario.Estado) ? null : usuario.Estado,
        TemSenha = usuario.SenhaHash is not null,
        TemGoogle = usuario.GoogleId is not null
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
        // precisa ser uma das faixas conhecidas pela regra 12 do prompt (as
        // mesmas da casa, RegrasOrcamento).
        if (!RegrasOrcamento.Valido(request.OrcamentoSemanal))
        {
            return BadRequest(new { mensagem = RegrasOrcamento.MensagemInvalido });
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

        var acordar = RegrasHorario.Interpretar(request.HorarioAcordar);
        var dormir = RegrasHorario.Interpretar(request.HorarioDormir);
        if (acordar.Erro is not null || dormir.Erro is not null)
        {
            return BadRequest(new { mensagem = RegrasHorario.MensagemFormato });
        }

        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == UsuarioIdLogado);

        usuario.HorarioAcordar = RegrasHorario.Aplicar(usuario.HorarioAcordar, acordar);
        usuario.HorarioDormir = RegrasHorario.Aplicar(usuario.HorarioDormir, dormir);
        usuario.Altura = request.Altura;
        usuario.Sexo = request.Sexo;
        usuario.DataNascimento = request.DataNascimento;
        // request.RotinaDiaria é ignorado de propósito (ver PerfilRequest): nem grava um texto novo vindo de uma
        // tela antiga, nem apaga o antigo quando uma tela nova não manda o campo.
        usuario.OrcamentoSemanal = request.OrcamentoSemanal;

        // Ausente ou vazio mantém o salvo (ver PerfilRequest.NivelAtividade).
        if (!string.IsNullOrEmpty(request.NivelAtividade))
        {
            usuario.NivelAtividade = request.NivelAtividade;
        }

        await _context.SaveChangesAsync();

        return Ok(MontarResposta(usuario));

    }

    /// <summary>
    /// Apaga a rotina em texto livre antiga (Usuario.RotinaDiaria), que não é mais usada desde os horários
    /// estruturados. É o "apagar" do aviso da Alimentação; quem não apagar perde o texto na limpeza dos prazos de
    /// guarda, 60 dias depois da publicação (LimpezaRetencao). Sem texto, não faz nada e responde igual: repetir o
    /// pedido (duas abas, clique duplo) não é erro.
    ///
    /// Dispensa o consentimento, como apagar a conta e baixar os dados: apagar o próprio dado é direito (art. 18, VI)
    /// e não pode depender de aceitar o tratamento antes. O usuário vem sempre do token.
    /// </summary>
    [HttpDelete("rotina")]
    [DispensaConsentimento]
    public async Task<IActionResult> ApagarRotina()
    {
        var usuario = await _context.Usuarios.FirstAsync(u => u.Id == UsuarioIdLogado);

        if (usuario.RotinaDiaria is not null)
        {
            usuario.RotinaDiaria = null;
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    // ---------- Alergias (TACO 2c) ----------
    // Rotas próprias, fora do PUT /api/perfil: com duas abas, o PUT do perfil inteiro de uma sobrescreveria os chips
    // da outra. Cada chip é uma linha (AlergiaUsuario), gravada ou apagada sozinha. Nada daqui vai para o log.

    /// <summary>Os chips marcados e se o uso das alergias está autorizado (aceite vigente do tipo "alergias").</summary>
    [HttpGet("alergias")]
    [EnableRateLimiting(LimitesAlergias.PoliticaAlergias)]
    public async Task<IActionResult> ObterAlergias() => Ok(await EstadoAlergias());

    /// <summary>
    /// Marca um chip. Sem aceite vigente, só grava se o corpo trouxer a versão vigente do texto (a pessoa acabou de
    /// marcar a caixa): aí o aceite e o chip são gravados juntos, num SaveChanges só. Sem aceite e sem versão: 409
    /// com motivo "consentimento_alergias", para a tela abrir a caixa. Versão diferente da vigente: 409 como no
    /// aceite geral. Marcar de novo o mesmo chip não duplica.
    /// </summary>
    [HttpPut("alergias/{codigo}")]
    [EnableRateLimiting(LimitesAlergias.PoliticaAlergias)]
    public async Task<IActionResult> MarcarAlergia(
        string codigo, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] AlergiaRequest? request)
    {
        if (!CodigosAlergia.Valido(codigo))
        {
            return BadRequest(new { mensagem = MensagemAlergiaInvalida });
        }

        var tipo = TextosConsentimento.TipoAlergias;
        var vigente = TextosConsentimento.VersaoVigente(tipo);
        var temAceite = await RegrasConsentimento.TemAceiteVigenteAsync(_context, UsuarioIdLogado, tipo);

        if (request?.VersaoConsentimento is { } versao)
        {
            if (versao != vigente)
            {
                return Conflict(new { mensagem = RegrasConsentimento.MensagemVersaoMudou, motivo = RegrasConsentimento.MotivoSemAceiteAlergias, versaoVigente = vigente });
            }

            if (!temAceite)
            {
                _context.Consentimentos.Add(new Consentimento
                {
                    UsuarioId = UsuarioIdLogado, Tipo = tipo, Versao = vigente, AceitoEm = DateTime.UtcNow
                });
            }
        }
        else if (!temAceite)
        {
            return Conflict(new { mensagem = RegrasConsentimento.MensagemSemAceiteAlergias, motivo = RegrasConsentimento.MotivoSemAceiteAlergias, versaoVigente = vigente });
        }

        if (!await _context.AlergiasUsuario.AnyAsync(a => a.UsuarioId == UsuarioIdLogado && a.Codigo == codigo))
        {
            _context.AlergiasUsuario.Add(new AlergiaUsuario { UsuarioId = UsuarioIdLogado, Codigo = codigo, MarcadoEm = DateTime.UtcNow });
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (EhChaveDuplicada(ex))
        {
            // Dois pedidos ao mesmo tempo (duas abas, ou a tela mandando vários chips logo depois da caixa): um dos
            // índices únicos barrou este. Se foi o do aceite, o aceite já está gravado pelo outro pedido, mas o chip
            // deste não entrou junto: grava só o chip, uma vez. Se foi o do chip, ele já está lá.
            _context.ChangeTracker.Clear();
            if (await RegrasConsentimento.TemAceiteVigenteAsync(_context, UsuarioIdLogado, tipo)
                && !await _context.AlergiasUsuario.AnyAsync(a => a.UsuarioId == UsuarioIdLogado && a.Codigo == codigo))
            {
                _context.AlergiasUsuario.Add(new AlergiaUsuario { UsuarioId = UsuarioIdLogado, Codigo = codigo, MarcadoEm = DateTime.UtcNow });
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException de) when (EhChaveDuplicada(de))
                {
                    _context.ChangeTracker.Clear();
                }
            }
        }

        return Ok(await EstadoAlergias());
    }

    private static bool EhChaveDuplicada(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>
    /// Desmarca um chip. Quando não sobra nenhum, o aceite do tipo "alergias" é revogado (RevogadoEm), e a linha fica
    /// para o histórico: marcar de novo pede a caixa outra vez. Desmarcar o que não está marcado responde igual.
    ///
    /// Dispensa o consentimento geral, como apagar a rotina: apagar o próprio dado é direito (art. 18, VI) e não pode
    /// depender de aceitar o tratamento antes. O usuário vem sempre do token.
    /// </summary>
    [HttpDelete("alergias/{codigo}")]
    [DispensaConsentimento]
    [EnableRateLimiting(LimitesAlergias.PoliticaAlergias)]
    public async Task<IActionResult> DesmarcarAlergia(string codigo)
    {
        if (!CodigosAlergia.Valido(codigo))
        {
            return BadRequest(new { mensagem = MensagemAlergiaInvalida });
        }

        _context.AlergiasUsuario.RemoveRange(
            await _context.AlergiasUsuario.Where(a => a.UsuarioId == UsuarioIdLogado && a.Codigo == codigo).ToListAsync());
        await _context.SaveChangesAsync();

        // Conferido depois de gravar, e não antes: duas abas tirando os dois últimos chips ao mesmo tempo veriam, cada
        // uma, o outro chip ainda lá, e nenhuma revogaria. Depois de gravar, a última a terminar vê zero.
        if (!await _context.AlergiasUsuario.AnyAsync(a => a.UsuarioId == UsuarioIdLogado))
        {
            var aceites = await _context.Consentimentos
                .Where(c => c.UsuarioId == UsuarioIdLogado && c.Tipo == TextosConsentimento.TipoAlergias && c.RevogadoEm == null)
                .ToListAsync();
            var agora = DateTime.UtcNow;
            foreach (var aceite in aceites)
            {
                aceite.RevogadoEm = agora;
            }

            await _context.SaveChangesAsync();
        }

        return Ok(await EstadoAlergias());
    }

    private const string MensagemAlergiaInvalida = "Alergia inválida.";

    private async Task<AlergiasResponse> EstadoAlergias()
    {
        var vigente = TextosConsentimento.VersaoVigente(TextosConsentimento.TipoAlergias);
        var aceitoEm = await _context.Consentimentos.AsNoTracking()
            .Where(c => c.UsuarioId == UsuarioIdLogado && c.Tipo == TextosConsentimento.TipoAlergias && c.Versao == vigente && c.RevogadoEm == null)
            .OrderByDescending(c => c.AceitoEm)
            .Select(c => (DateTime?)c.AceitoEm)
            .FirstOrDefaultAsync();

        return new AlergiasResponse
        {
            Alergias = (await ListaDaPessoa.AlergiasAsync(_context, UsuarioIdLogado)).ToList(),
            AceiteVigente = aceitoEm is not null,
            AceitoEm = aceitoEm
        };
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
