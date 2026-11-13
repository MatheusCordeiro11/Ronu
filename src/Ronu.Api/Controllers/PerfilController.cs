using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Services.Consentimentos;
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

    // Valores fixos enviados pelas pills de alimentacao.html — sem normalização
    // (diferente do Estado), porque não vêm de digitação livre.
    private static readonly HashSet<string> ValoresOrcamentoSemanal = new()
    {
        "economico", "moderado", "sem_restricao"
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
        // precisa ser uma das faixas conhecidas pela regra 12 do prompt.
        if (!string.IsNullOrEmpty(request.OrcamentoSemanal) && !ValoresOrcamentoSemanal.Contains(request.OrcamentoSemanal))
        {
            return BadRequest(new { mensagem = "Orçamento semanal inválido." });
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
