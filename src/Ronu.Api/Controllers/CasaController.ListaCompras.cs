using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Models;
using Ronu.Api.Services.IA;
using Ronu.Api.Services.ListaCompras;
using Ronu.Api.Services.PlanoFamilia;

namespace Ronu.Api.Controllers;

/// <summary>
/// Plano família, etapa 6: a lista de compras da casa e o "já tenho" da casa. Para quem está numa casa, a tela de compras
/// mostra esta lista por padrão e a alternância "Só a minha parte", que é a lista individual de sempre
/// (GET /api/dietas/atual/lista-compras, com as marcações individuais). Mesmas regras das outras rotas da casa: 404 para
/// quem não é da casa (sem revelar casa nenhuma) e 409 sem o aceite "casa" vigente.
/// </summary>
public partial class CasaController
{
    public const string MensagemSemanaDaListaMudou = "A semana da casa mudou desde que a lista foi aberta. Atualize a lista e tente de novo.";
    public const string MensagemListaDaCasaMudou = "A lista da casa mudou desde que foi aberta. Atualize a lista e tente de novo.";
    public const string MensagemItemForaDaListaDaCasa = "Este item não está na lista da casa.";
    public const string MensagemInformeQuantidade = "Informe a quantidade do item.";

    // O formato de ItemCompra.Chave, o mesmo da lista individual: a chave vem da URL e vai para o banco.
    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex FormatoChaveLista();

    // A UF de quem está vendo: o nome regional e o fuso da data do título. Nunca a de outro membro.
    private Task<string?> UfLogadoAsync() => _context.Usuarios.AsNoTracking()
        .Where(u => u.Id == UsuarioIdLogado)
        .Select(u => u.Estado)
        .FirstOrDefaultAsync();

    /// <summary>
    /// A lista da casa: a semana inteira de todos, somada por alimento, sem dizer de quem é cada item. 404 sem casa ou sem
    /// semana. A marca de alergia usa só as alergias de quem está vendo.
    /// </summary>
    [HttpGet("lista-compras")]
    public async Task<IActionResult> ObterListaCompras()
    {
        var (eu, erro) = await MembroLogadoAsync(exigirDono: false, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        var uf = await UfLogadoAsync();
        var montada = await ListaComprasCasa.MontarAsync(_context, eu!.CasaId, uf);
        if (montada is null)
        {
            return NotFound(new { mensagem = MensagemSemSemana });
        }

        var marcacoes = await _context.ListaCasaMarcacoes.AsNoTracking()
            .Where(m => m.SemanaCasaId == montada.Semana.Id)
            .Select(m => new { m.Chave, m.Quantidade })
            .ToListAsync();
        var marcados = ListaComprasCasa.Marcados(marcacoes.Select(m => (m.Chave, m.Quantidade)), montada.Lista);
        foreach (var item in montada.Lista.Secoes.SelectMany(s => s.Itens))
        {
            item.Tenho = marcados.Contains(item.Chave);
        }

        // Só as alergias de quem vê (decisão da etapa 6). O item que vem do café de outra pessoa com um alérgeno de um
        // terceiro fica sem marca: marcar, mesmo sem dizer qual, contaria a todos que alguém da casa tem aquela alergia.
        AlergiasNaLista.Marcar(montada.Lista.Secoes, await ListaDaPessoa.AlergiasAsync(_context, UsuarioIdLogado));

        return Ok(new ListaCasaResponse
        {
            SemanaId = montada.Semana.Id,
            GeradaEm = montada.Semana.GeradaEm,
            TituloCompartilhar = ListaComprasCasa.TituloCompartilhar(montada.Semana.GeradaEm, uf),
            VersaoConversao = ConversaoCompras.VersaoConversao,
            Disponivel = montada.Lista.Disponivel,
            Motivo = montada.Lista.Motivo,
            AvisoOleo = montada.Lista.AvisoOleo,
            Completa = montada.Completa,
            Secoes = montada.Lista.Secoes
        });
    }

    /// <summary>
    /// Marca (tenho = true) ou desmarca um item como "já tenho" para a casa toda. Qualquer membro marca e desmarca; a marca
    /// não guarda quem foi. Idempotente: marcar o marcado ou desmarcar o desmarcado dá 204 do mesmo jeito.
    /// </summary>
    [HttpPut("lista-compras/itens/{chave}")]
    public async Task<IActionResult> MarcarListaCompras(string chave, [FromBody] MarcacaoListaCasaRequest request)
    {
        if (!FormatoChaveLista().IsMatch(chave))
        {
            return BadRequest(new { mensagem = "Item inválido." });
        }

        var (eu, erro) = await MembroLogadoAsync(exigirDono: false, exigirConsentimento: true);
        if (erro is not null)
        {
            return erro;
        }

        if (request.Tenho == true && request.Quantidade is null)
        {
            return BadRequest(new { mensagem = MensagemInformeQuantidade });
        }

        var montada = await ListaComprasCasa.MontarAsync(_context, eu!.CasaId, await UfLogadoAsync());
        if (montada is null)
        {
            return NotFound(new { mensagem = MensagemSemSemana });
        }

        // O semanaId só é comparado com a semana atual da casa de quem chama: o id de uma semana de outra casa nunca é
        // ela, e cai aqui também, sem gravar nada.
        if (request.SemanaId != montada.Semana.Id)
        {
            return Conflict(new { mensagem = MensagemSemanaDaListaMudou });
        }

        var item = montada.Lista.Secoes.SelectMany(s => s.Itens).FirstOrDefault(i => i.Chave == chave);
        if (item is null)
        {
            return BadRequest(new { mensagem = MensagemItemForaDaListaDaCasa });
        }

        var existente = await _context.ListaCasaMarcacoes
            .FirstOrDefaultAsync(m => m.SemanaCasaId == montada.Semana.Id && m.Chave == chave);

        if (request.Tenho == true)
        {
            // A lista passou a pedir mais do que a tela mostrava: marcar agora diria que a casa tem o que ninguém viu.
            if (item.Quantidade > request.Quantidade)
            {
                return Conflict(new { mensagem = MensagemListaDaCasaMudou });
            }

            var agora = DateTime.UtcNow;
            if (existente is null)
            {
                _context.ListaCasaMarcacoes.Add(new ListaCasaMarcacao
                {
                    CasaId = eu.CasaId,
                    SemanaCasaId = montada.Semana.Id,
                    Chave = chave,
                    Quantidade = item.Quantidade,
                    MarcadoEm = agora
                });
            }
            else
            {
                // Remarcar renova a quantidade: a marca que tinha caído (a lista pediu mais) volta a valer com a de agora.
                existente.Quantidade = item.Quantidade;
                existente.MarcadoEm = agora;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ListaComprasController.EhChaveDuplicada(ex))
            {
                // Dois membros marcando o mesmo item ao mesmo tempo: o índice único (SemanaCasaId, Chave) barra o
                // segundo, e o item já está marcado para a casa, que é o que se pediu.
            }
        }
        else if (existente is not null)
        {
            _context.ListaCasaMarcacoes.Remove(existente);
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }
}
