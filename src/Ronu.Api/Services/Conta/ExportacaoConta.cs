using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Validacao;

namespace Ronu.Api.Services.Conta;

/// <summary>
/// "Baixar meus dados" (LGPD, art. 18, II e V): tudo da conta num JSON. Montado campo a campo, nunca serializando as
/// entidades: um campo novo no modelo (como um hash) não entra no arquivo sem alguém escrevê-lo aqui. Ficam de fora o
/// hash da senha, o hash do token de redefinição e o do convite da casa. Toda consulta filtra pelo usuário do token.
/// </summary>
public static class ExportacaoConta
{
    public const string Formato = "ronu-exportacao-v1";

    // Acentos legíveis no arquivo (sem ã). O arquivo vai como anexo JSON, nunca dentro de uma página.
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Nulo se a conta não existe mais.
    public static async Task<byte[]?> GerarAsync(ApplicationDbContext contexto, int usuarioId, DateTime agora)
    {
        var u = await contexto.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usuarioId);
        if (u is null)
        {
            return null;
        }

        var pesagens = await contexto.ObjetivosUsuario.AsNoTracking()
            .Where(o => o.UsuarioId == usuarioId).OrderBy(o => o.DataRegistro)
            .Select(o => new { o.DataRegistro, o.Peso, o.Objetivo, o.Aderencia })
            .ToListAsync();

        var modalidades = await contexto.UsuarioModalidades.AsNoTracking()
            .Where(m => m.UsuarioId == usuarioId).OrderBy(m => m.Id)
            .Select(m => new { Modalidade = m.Modalidade.Nome, m.DiasSemana, m.DuracaoMediaHoras, HorarioInicio = RegrasHorario.Formatar(m.HorarioInicio) })
            .ToListAsync();

        var preferencias = await contexto.PreferenciasAlimentares.AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId).OrderBy(p => p.Id)
            .Select(p => new { p.Alimento, p.Tipo })
            .ToListAsync();

        var dietasGravadas = await contexto.DietasIA.AsNoTracking()
            .Where(d => d.UsuarioId == usuarioId).OrderBy(d => d.DataGeracao)
            .Select(d => new { d.Id, d.DataGeracao, d.Revisao, d.ConteudoJson, d.Origem })
            .ToListAsync();
        // O cardápio vai como JSON de verdade, não como texto com aspas escapadas. A origem diz se a dieta veio da
        // semana da casa (o almoço e o jantar com a porção da pessoa); a semana em si é da casa, não da pessoa.
        var dietas = dietasGravadas.Select(d => new
        {
            DietaId = d.Id, d.DataGeracao, d.Revisao, d.Origem,
            Cardapio = JsonSerializer.Deserialize<JsonElement>(d.ConteudoJson)
        }).ToList();

        var metas = await contexto.MetasDieta.AsNoTracking()
            .Where(m => m.UsuarioId == usuarioId).OrderBy(m => m.DataGeracao)
            .Select(m => new { m.DataGeracao, m.VersaoFormula, m.MetasPorDia, m.ManutencoesPorDia, m.FatorAtividade, m.PercentualAjusteAdaptativo })
            .ToListAsync();

        var marcacoes = await contexto.ListaComprasMarcacoes.AsNoTracking()
            .Where(m => m.UsuarioId == usuarioId).OrderBy(m => m.MarcadoEm)
            .Select(m => new { DietaId = m.DietaIAId, m.Chave, m.MarcadoEm })
            .ToListAsync();

        // TACO 2c: os chips de alergia (o aceite do tipo "alergias" já sai junto com os outros, em Consentimentos).
        var alergias = await contexto.AlergiasUsuario.AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId).OrderBy(a => a.MarcadoEm)
            .Select(a => new { a.Codigo, a.MarcadoEm })
            .ToListAsync();

        var consentimentos = await contexto.Consentimentos.AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId).OrderBy(c => c.AceitoEm)
            .Select(c => new { c.Tipo, c.Versao, c.AceitoEm, c.RevogadoEm })
            .ToListAsync();

        // Sem o TokenHash: ele só serve para conferir o link, e o link já não vale.
        var pedidos = await contexto.PedidosRedefinicaoSenha.AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId).OrderBy(p => p.CriadoEm)
            .Select(p => new { p.Tipo, p.CriadoEm, p.ExpiraEm, p.UsadoEm, p.InvalidadoEm })
            .ToListAsync();

        // Plano família: o papel da pessoa na casa e os convites que ela gerou, sem o TokenHash (como o da redefinição).
        // Nada dos outros membros: nem nomes, nem quantos são. O "já tenho" da lista da casa (ListaCasaMarcacoes, etapa 6)
        // não entra, como os pedidos da casa: é da casa e não guarda quem marcou, então não é dado desta pessoa.
        var casa = await contexto.MembrosCasa.AsNoTracking()
            .Where(m => m.UsuarioId == usuarioId)
            .Select(m => new { m.Papel, m.EntrouEm, m.TransferenciaOferecidaEm })
            .FirstOrDefaultAsync();

        var convitesCasa = await contexto.ConvitesCasa.AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId).OrderBy(c => c.CriadoEm)
            .Select(c => new { c.CriadoEm, c.ExpiraEm, c.UsadoEm, c.RevogadoEm })
            .ToListAsync();

        var arquivo = new
        {
            Formato,
            GeradoEm = agora,
            Conta = new
            {
                u.Nome, u.Email, Estado = string.IsNullOrEmpty(u.Estado) ? null : u.Estado,
                u.CriadoEm, u.UltimoLoginEm,
                TemSenha = u.SenhaHash is not null, u.SenhaAlteradaEm,
                GoogleVinculado = u.GoogleId is not null, u.GoogleId
            },
            Perfil = new
            {
                u.Altura, u.Sexo, u.DataNascimento, u.NivelAtividade, u.RotinaDiaria, u.OrcamentoSemanal,
                HorarioAcordar = RegrasHorario.Formatar(u.HorarioAcordar), HorarioDormir = RegrasHorario.Formatar(u.HorarioDormir)
            },
            Pesagens = pesagens,
            Modalidades = modalidades,
            PreferenciasAlimentares = preferencias,
            Alergias = alergias,
            Dietas = dietas,
            MetasDieta = metas,
            ListaComprasMarcacoes = marcacoes,
            Consentimentos = consentimentos,
            PedidosRedefinicaoSenha = pedidos,
            Casa = casa,
            ConvitesCasa = convitesCasa
        };

        return JsonSerializer.SerializeToUtf8Bytes(arquivo, Opcoes);
    }
}
