using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;

namespace Ronu.Api.Services.Conta;

/// <summary>
/// Os prazos de guarda de docs/privacidade.md que a API cumpre sozinha:
/// - pedidos de redefinição de senha com mais de 30 dias saem (o limite por conta só olha as últimas 24 horas, e o
///   link vale 30 minutos: depois disso o pedido só serviria para investigar abuso recente);
/// - conta que nunca aceitou o consentimento geral sai depois de 30 dias, mas só quando as três coisas valem (decisão
///   de 2026-11-01): nunca aceitou nenhuma versão; não tem nenhum dado além de nome e email (nem perfil, nem UF, nem
///   linha em outra tabela); e CriadoEm é conhecido e passou de 30 dias. As contas anteriores ao CriadoEm ficam com
///   ele nulo e nunca entram: muitas não têm aceite porque o consentimento não existia, e algumas têm peso e dietas;
/// - a rotina em texto livre (Usuario.RotinaDiaria) de todas as contas sai 60 dias depois da publicação das telas sem
///   ela (decisão de 2026-11-02, "opção A"): o texto não é mais usado desde os horários estruturados, e guardar dado
///   sem uso vai contra a necessidade (art. 6º, III). Os 60 dias dão tempo de a pessoa ver o texto na Alimentação e
///   baixá-lo na exportação. A data de início vem da configuração (ChaveRotinaForaDaTelaEm), porque a publicação
///   ainda não aconteceu; sem ela, esta parte não roda.
/// </summary>
public static class LimpezaRetencao
{
    public static readonly TimeSpan PrazoPedidosRedefinicao = TimeSpan.FromDays(30);
    public static readonly TimeSpan PrazoContaSemAceite = TimeSpan.FromDays(30);
    public static readonly TimeSpan PrazoRotinaTextoLivre = TimeSpan.FromDays(60);

    // O dia em que o frontend sem o campo "Sua rotina" foi publicado (passo 4 do roteiro da seção 3 de
    // docs/privacidade.md), em "AAAA-MM-DD". No App Service, a variável Retencao__RotinaForaDaTelaEm. Ausente ou
    // vazia = a limpeza da rotina não roda (nenhuma data inventada antes da publicação).
    public const string ChaveRotinaForaDaTelaEm = "Retencao:RotinaForaDaTelaEm";

    public record Resultado(int PedidosApagados, int ContasApagadas, int RotinasApagadas = 0);

    /// <summary>
    /// Lê a data de ChaveRotinaForaDaTelaEm como o início do dia em UTC. Nulo se ausente ou vazia; exceção se o
    /// valor não for uma data "AAAA-MM-DD", para o erro aparecer na subida da API (Program.cs), e não virar uma
    /// limpeza que nunca roda sem ninguém saber.
    /// </summary>
    public static DateTime? LerRotinaForaDaTelaEm(IConfiguration configuracao)
    {
        var valor = configuracao[ChaveRotinaForaDaTelaEm];
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(valor.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
        {
            throw new InvalidOperationException($"{ChaveRotinaForaDaTelaEm} precisa ser uma data no formato AAAA-MM-DD.");
        }

        return data.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    // rotinaForaDaTelaEm: ver ChaveRotinaForaDaTelaEm. Nulo = a parte da rotina não roda.
    public static async Task<Resultado> ExecutarAsync(ApplicationDbContext contexto, DateTime agora, DateTime? rotinaForaDaTelaEm = null)
    {
        var limitePedidos = agora - PrazoPedidosRedefinicao;
        var pedidos = await contexto.PedidosRedefinicaoSenha.Where(p => p.CriadoEm < limitePedidos).ToListAsync();
        contexto.PedidosRedefinicaoSenha.RemoveRange(pedidos);
        await contexto.SaveChangesAsync();

        var limiteContas = agora - PrazoContaSemAceite;
        var contas = await contexto.Usuarios
            .Where(u => u.CriadoEm != null && u.CriadoEm < limiteContas)
            .Where(u => !contexto.Consentimentos.Any(c => c.UsuarioId == u.Id))
            .Where(u => u.Altura == null && u.DataNascimento == null && u.Sexo == null && u.RotinaDiaria == null
                && u.OrcamentoSemanal == null && u.NivelAtividade == null && (u.Estado == null || u.Estado == ""))
            .Where(u => !contexto.ObjetivosUsuario.Any(x => x.UsuarioId == u.Id)
                && !contexto.UsuarioModalidades.Any(x => x.UsuarioId == u.Id)
                && !contexto.PreferenciasAlimentares.Any(x => x.UsuarioId == u.Id)
                && !contexto.DietasIA.Any(x => x.UsuarioId == u.Id)
                && !contexto.MetasDieta.Any(x => x.UsuarioId == u.Id)
                && !contexto.ListaComprasMarcacoes.Any(x => x.UsuarioId == u.Id)
                // Na prática, quem tem alergia já tem o aceite "alergias" e nem chega aqui; fica pela regra de "nenhuma
                // linha em outra tabela", para não depender disso.
                && !contexto.AlergiasUsuario.Any(x => x.UsuarioId == u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        var apagadas = 0;
        foreach (var id in contas)
        {
            if (await ExclusaoConta.ApagarAsync(contexto, id))
            {
                apagadas++;
            }
        }

        // Por último, para não mudar a regra das contas sem aceite nesta rodada: uma conta nova cujo único dado era a
        // rotina só fica "sem nenhum dado" a partir da próxima. Carrega e grava pelo EF (não ExecuteUpdate) para valer
        // também no banco em memória dos testes; depois do prazo, cada rodada encontra zero linhas.
        var rotinasApagadas = 0;
        if (rotinaForaDaTelaEm is { } inicio && agora >= inicio + PrazoRotinaTextoLivre)
        {
            var comRotina = await contexto.Usuarios.Where(u => u.RotinaDiaria != null).ToListAsync();
            foreach (var usuario in comRotina)
            {
                usuario.RotinaDiaria = null;
            }
            await contexto.SaveChangesAsync();
            rotinasApagadas = comRotina.Count;
        }

        return new Resultado(pedidos.Count, apagadas, rotinasApagadas);
    }
}
