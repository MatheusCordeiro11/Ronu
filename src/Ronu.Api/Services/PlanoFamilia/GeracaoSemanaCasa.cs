using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Ronu.Api.Models;
using Ronu.Api.Models.IA;
using Ronu.Api.Services.IA;

namespace Ronu.Api.Services.PlanoFamilia;

/// <summary>
/// O dono gera a semana da casa (plano família, etapa 4). Na ordem, do mais barato ao mais caro:
/// 1. limite de 3 gerações por hora do dono e o perfil completo dele (a porção sai da meta);
/// 2. a lista da casa (ListaDaCasa): papel do prato vazio dá 400 sem chamar o Gemini;
/// 3. UMA chamada ao Gemini, só com a lista, as regras e o orçamento da casa;
/// 4. para cada membro, sem IA: as metas do dia (a mesma conta da geração individual), as porções da casa e o café e os
///    lanches que ele já tinha, reajustados em C# para o que sobra da meta (decisão D6);
/// 5. numa transação: a SemanaCasa e uma DietaIA nova para cada membro (Origem = casa), com a MetaDieta de cada uma.
///    Assim a regra das 3 dietas e a meta adaptativa continuam valendo para quem está na casa, sem mudança nenhuma.
/// </summary>
public class GeracaoSemanaCasa
{
    // A atual e a anterior: a anterior fica para o "Desfazer" da troca e o aviso de entrada (etapa 5).
    public const int SemanasGuardadas = 2;

    public const string MensagemPerfilIncompleto = "Complete seu perfil (altura, sexo e data de nascimento) e registre um objetivo antes de gerar a semana da casa.";
    public const string MensagemIndisponivel = "Não foi possível gerar a semana da casa agora. Tente novamente em alguns instantes.";

    private readonly ApplicationDbContext _context;
    private readonly IContextoDietaBuilder _contextoBuilder;
    private readonly IGeradorSemanaCasa _gerador;
    private readonly ILogger<GeracaoSemanaCasa> _logger;

    public GeracaoSemanaCasa(ApplicationDbContext context, IContextoDietaBuilder contextoBuilder, IGeradorSemanaCasa gerador, ILogger<GeracaoSemanaCasa> logger)
    {
        _context = context;
        _contextoBuilder = contextoBuilder;
        _gerador = gerador;
        _logger = logger;
    }

    public record Erro(int Status, string Mensagem);

    public record Resultado(SemanaCasa? Semana, SemanaCasaDto? Conteudo, Erro? Falha);

    public async Task<Resultado> GerarAsync(int casaId, int donoId)
    {
        var agora = DateTime.UtcNow;
        if (await LimiteGeracoes.ConferirAsync(_context, donoId, ehDonoDeCasa: true, agora, "semanas ou dietas") is { } limite)
        {
            return Falhou(429, limite);
        }

        var membros = await _context.MembrosCasa.AsNoTracking()
            .Where(m => m.CasaId == casaId)
            .OrderBy(m => m.EntrouEm).ThenBy(m => m.Id)
            .Select(m => m.UsuarioId)
            .ToListAsync();
        var completos = await PerfisCompletosAsync(membros);
        if (!completos.Contains(donoId))
        {
            return Falhou(400, MensagemPerfilIncompleto);
        }

        // Todos entram na lista da casa, inclusive quem está com o perfil incompleto: as alergias e o "não como" dessa
        // pessoa valem do mesmo jeito.
        var listaDaCasa = await ListaDaCasa.MontarAsync(_context, membros, donoId);
        if (listaDaCasa.Minimos.Vazios.Count > 0)
        {
            return Falhou(400, ListaDaCasa.MensagemPapeisVazios(listaDaCasa.Minimos.Vazios));
        }

        var casa = await _context.Casas.AsNoTracking().FirstAsync(c => c.Id == casaId);
        var ufDoDono = await _context.Usuarios.AsNoTracking().Where(u => u.Id == donoId).Select(u => u.Estado).FirstOrDefaultAsync();

        // Os contextos antes do Gemini: um erro aqui não deve gastar a chamada.
        var contextos = new List<(int UsuarioId, ContextoDietaDto Contexto)>();
        foreach (var id in membros.Where(completos.Contains))
        {
            contextos.Add((id, await _contextoBuilder.ConstruirAsync(id)));
        }

        SemanaCasaDto semana;
        try
        {
            semana = await _gerador.GerarSemanaCasaAsync(listaDaCasa, casa.OrcamentoSemanal, ufDoDono);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or RespostaIaInvalidaException)
        {
            _logger.LogWarning(ex, "Falha ao gerar a semana da casa {CasaId}", casaId);
            return Falhou(503, MensagemIndisponivel);
        }

        // O café e os lanches de cada um vêm da dieta atual dele (decisão D6), lidos antes de gravar a nova.
        var dietas = new List<(int UsuarioId, DietaSemanalDto Dieta)>();
        foreach (var (id, contexto) in contextos)
        {
            var mantidas = await CafeELanchesAtuaisAsync(id, contexto.Lista!);
            dietas.Add((id, MontarDieta(semana, _gerador.CalcularMetas(contexto), contexto.Lista!, mantidas)));
        }

        if (membros.Count != completos.Count)
        {
            // Não deveria acontecer (o convite exige o perfil completo), mas um membro sem perfil fica sem dieta nova
            // em vez de travar a casa. Só a contagem: nenhum id de membro no log da casa.
            _logger.LogWarning("Semana da casa {CasaId}: {SemPerfil} membro(s) sem perfil completo ficaram sem a dieta nova", casaId, membros.Count - completos.Count);
        }

        var linha = await GravarAsync(casaId, semana, dietas, agora);
        return new Resultado(linha, semana, null);
    }

    private static Resultado Falhou(int status, string mensagem) => new(null, null, new Erro(status, mensagem));

    private async Task<HashSet<int>> PerfisCompletosAsync(List<int> usuarioIds)
    {
        var comDados = await _context.Usuarios.AsNoTracking()
            .Where(u => usuarioIds.Contains(u.Id) && u.Altura != null && u.Sexo != null && u.DataNascimento != null)
            .Select(u => u.Id)
            .ToListAsync();
        var comObjetivo = await _context.ObjetivosUsuario.AsNoTracking()
            .Where(o => usuarioIds.Contains(o.UsuarioId))
            .Select(o => o.UsuarioId)
            .Distinct()
            .ToListAsync();
        return comDados.Intersect(comObjetivo).ToHashSet();
    }

    /// <summary>
    /// A dieta da semana de um membro: em cada dia, as porções dele nas refeições da casa e as refeições individuais
    /// (o café e os lanches que ele já tinha, reajustados para o que sobra da meta; sem eles, só as da casa, até ele
    /// gerar os seus). A meta de cada dia é a cheia, e a MetaDieta grava a mesma de uma geração individual.
    /// </summary>
    public static DietaSemanalDto MontarDieta(SemanaCasaDto semana, MetasDaSemana metas, ListaAlimentos listaDaPessoa,
        IReadOnlyDictionary<string, List<RefeicaoDto>>? individuaisPorDia)
    {
        var casa = PorcoesCasa.Montar(semana, metas.MetasPorDia, listaDaPessoa);
        var dias = GeradorDietaGemini.NomesDias.Select(nome => new DiaDietaDto
        {
            DiaSemana = nome,
            Refeicoes = individuaisPorDia is not null && individuaisPorDia.TryGetValue(nome, out var mantidas) ? mantidas : [],
            TotalDoDia = new MacrosDto { Calorias = 0, ProteinasG = 0, CarboidratosG = 0, GordurasG = 0 },
            MetaCalculada = metas.MetasPorDia[nome].Macros,
            ComposicaoMeta = metas.ComposicaoPorDia[nome]
        }).ToList();

        // Selênio: o café e os lanches mantidos passaram pelo teto quando foram gerados, mas sem a castanha desta semana
        // da casa (que pode cair no mesmo dia, ou num 3º dia). O corte cai neles, antes do reajuste, para ele
        // compensar as kcal com os outros itens, como na geração individual.
        LimiteCastanhaDoPara.Aplicar(dias, PorcoesCasa.CastanhaPorDia(casa));

        foreach (var dia in dias)
        {
            var nome = dia.DiaSemana;
            if (dia.Refeicoes.Count > 0)
            {
                PorcoesCasa.Ajustar(dia.Refeicoes, casa.MetasRestantes[nome].Macros);
                PorcoesCasa.RotularDeNovo(dia.Refeicoes);
            }

            PorcoesCasa.Juntar(dia, casa.RefeicoesPorDia.TryGetValue(nome, out var daCasa) ? daCasa : [], metas.MetasPorDia[nome]);
        }

        return new DietaSemanalDto
        {
            Dias = dias,
            AjusteAdaptativo = metas.Ajuste,
            VersaoBase = BaseNutricional.VersaoBase,
            // O aviso de lista pobre da casa fica na SemanaCasa, só para o dono: na dieta de cada um, ele diria algo das
            // restrições dos outros.
            AvisoListaPobre = null,
            ManutencaoPorDia = metas.ManutencaoPorDia,
            FatorAtividade = metas.FatorAtividade
        };
    }

    /// <summary>
    /// O café e os lanches da dieta atual da pessoa, por dia: as refeições que não são da casa nem almoço ou jantar,
    /// só os itens que ainda estão na lista dela (o "não como" ou uma alergia marcada depois tira o item), sem a marca
    /// da troca. Vazio sem dieta, ou com uma dieta de outra versão da base (os ids podem não existir mais).
    /// </summary>
    /// <param name="almocoJantarNoLugar">
    /// Etapa 5, entrada no meio da semana: as refeições da casa que não servem para a pessoa ("qui-jantar"). Nelas, o
    /// almoço ou o jantar que ela já tinha naquele dia fica, como refeição individual, no lugar do da casa.
    /// </param>
    public async Task<Dictionary<string, List<RefeicaoDto>>> CafeELanchesAtuaisAsync(int usuarioId, ListaAlimentos listaDaPessoa,
        IReadOnlyCollection<string>? almocoJantarNoLugar = null)
    {
        var atual = await _context.DietasIA.AsNoTracking()
            .Where(d => d.UsuarioId == usuarioId)
            .OrderByDescending(d => d.DataGeracao)
            .Select(d => d.ConteudoJson)
            .FirstOrDefaultAsync();
        var resultado = new Dictionary<string, List<RefeicaoDto>>(StringComparer.OrdinalIgnoreCase);
        if (atual is null)
        {
            return resultado;
        }

        var dieta = JsonSerializer.Deserialize<DietaSemanalDto>(atual)!;
        if (dieta.VersaoBase != BaseNutricional.VersaoBase)
        {
            return resultado;
        }

        var naLista = listaDaPessoa.Itens.Select(i => i.Item.Id).ToHashSet();
        foreach (var dia in dieta.Dias)
        {
            var indiceDia = Array.FindIndex(GeradorDietaGemini.NomesDias, n => string.Equals(n, dia.DiaSemana.Trim(), StringComparison.OrdinalIgnoreCase));
            bool FicaNoLugar(RefeicaoDto r) => almocoJantarNoLugar is not null && indiceDia >= 0
                && almocoJantarNoLugar.Contains(PorcoesCasa.Chave(indiceDia, r.Nome.Contains("almo", StringComparison.OrdinalIgnoreCase)));
            var refeicoes = dia.Refeicoes
                .Where(r => !r.DaCasa && (!PorcoesCasa.EAlmocoOuJantar(r) || FicaNoLugar(r)))
                .Select(r => new RefeicaoDto
                {
                    Nome = r.Nome,
                    Horario = r.Horario,
                    Macros = r.Macros,
                    Alimentos = r.Alimentos
                        .Where(a => a.AlimentoId is not null && naLista.Contains(a.AlimentoId))
                        .Select(a => new AlimentoDto { Nome = PorcoesCasa.NomeSemContagem(a), Quantidade = a.Quantidade, Unidade = a.Unidade, AlimentoId = a.AlimentoId })
                        .ToList()
                })
                .Where(r => r.Alimentos.Count > 0)
                .ToList();
            if (refeicoes.Count > 0)
            {
                resultado[dia.DiaSemana.Trim()] = refeicoes;
            }
        }

        return resultado;
    }

    private async Task<SemanaCasa> GravarAsync(int casaId, SemanaCasaDto semana, List<(int UsuarioId, DietaSemanalDto Dieta)> dietas, DateTime agora)
    {
        await using var transacao = await OperacoesCasa.IniciarTransacaoAsync(_context);

        // Abre espaço antes de gravar: a casa guarda a atual e a anterior.
        var antigas = await _context.SemanasCasa
            .Where(s => s.CasaId == casaId)
            .OrderByDescending(s => s.GeradaEm).ThenByDescending(s => s.Id)
            .Skip(SemanasGuardadas - 1)
            .ToListAsync();
        // O "já tenho" da casa (etapa 6) vale para uma semana só e sai com ela. Explícito, e não só pela cascata: o banco
        // em memória dos testes não tem cascata.
        var idsAntigas = antigas.Select(s => s.Id).ToList();
        _context.ListaCasaMarcacoes.RemoveRange(await _context.ListaCasaMarcacoes.Where(m => idsAntigas.Contains(m.SemanaCasaId)).ToListAsync());
        _context.SemanasCasa.RemoveRange(antigas);

        // Etapa 5: a semana nova atende todos os pedidos e avisos abertos (eram sobre a semana que saiu). Apagados, não
        // marcados: o pedido não guarda histórico. Assim o "Pedido enviado" do membro também volta a valer.
        _context.PedidosCasa.RemoveRange(await _context.PedidosCasa.Where(p => p.CasaId == casaId).ToListAsync());

        var linha = new SemanaCasa
        {
            CasaId = casaId,
            GeradaEm = agora,
            ConteudoJson = JsonSerializer.Serialize(semana),
            VersaoBase = semana.VersaoBase
        };
        _context.SemanasCasa.Add(linha);
        await _context.SaveChangesAsync();

        foreach (var (usuarioId, dieta) in dietas)
        {
            await RepositorioDietaIA.AbrirEspacoAsync(_context, usuarioId);
            await RepositorioDietaIA.GravarAsync(_context, usuarioId, dieta, agora, DietaIA.OrigemCasa, linha.Id);
        }

        if (transacao is not null)
        {
            await transacao.CommitAsync();
        }

        return linha;
    }

    /// <summary>
    /// Etapa 5, entrada no meio da semana (decisão de 2026-10-26): quem acabou de aceitar o convite recebe na hora as
    /// porções do almoço e do jantar da semana atual da casa, calculadas aqui, sem chamar o Gemini (a semana da casa
    /// não muda). É o mesmo cálculo que a semana nova faz para cada membro (MontarDieta): a meta do dia da pessoa, as
    /// porções da casa e o café e os lanches que ela já tinha, reajustados para o que sobra da meta.
    ///
    /// Uma refeição da casa com algo que a pessoa não come ou tem alergia vira individual só para ela: fica o almoço ou
    /// o jantar que ela já tinha naquele dia (sem os itens que ela não come), ou nada, até ela gerar o café e os
    /// lanches (o POST /api/dietas/gerar monta essa refeição junto). O dono recebe um aviso sem nome
    /// (PedidoCasa.TipoAvisoEntrada) e a semana marca o RefeicaoIndividualEm.
    ///
    /// Sem semana, ou com uma de outra versão da base (os AlimentoId podem não existir), não faz nada: a pessoa gera a
    /// dieta como hoje. Devolve as refeições da casa que viraram individuais (vazia quando todas servem).
    /// </summary>
    public async Task<IReadOnlyList<string>> EntrarNaSemanaAsync(int casaId, int usuarioId)
    {
        var linha = await AtualAsync(_context, casaId, rastrear: true);
        if (linha is null || linha.VersaoBase != BaseNutricional.VersaoBase)
        {
            return [];
        }

        var semana = JsonSerializer.Deserialize<SemanaCasaDto>(linha.ConteudoJson)!;
        var contexto = await _contextoBuilder.ConstruirAsync(usuarioId);
        var lista = contexto.Lista!;
        var naoServem = PorcoesCasa.RefeicoesQueNaoServem(semana, lista);
        var mantidas = await CafeELanchesAtuaisAsync(usuarioId, lista, naoServem);
        var dieta = MontarDieta(semana, _gerador.CalcularMetas(contexto), lista, mantidas);
        var agora = DateTime.UtcNow;

        await using var transacao = await OperacoesCasa.IniciarTransacaoAsync(_context);
        if (naoServem.Count > 0)
        {
            linha.RefeicaoIndividualEm = agora;
            RegrasPedidosCasa.AvisarEntrada(_context, casaId, linha.Id, naoServem, agora);
        }

        await RepositorioDietaIA.AbrirEspacoAsync(_context, usuarioId);
        // Origem = casa: como a dieta que a semana da casa cria para cada membro, não gasta a cota de gerações da pessoa.
        await RepositorioDietaIA.GravarAsync(_context, usuarioId, dieta, agora, DietaIA.OrigemCasa, linha.Id);
        if (transacao is not null)
        {
            await transacao.CommitAsync();
        }

        return naoServem;
    }

    /// <summary>A semana atual da casa (a mais recente), ou nula.</summary>
    public static Task<SemanaCasa?> AtualAsync(ApplicationDbContext contexto, int casaId, bool rastrear = false) =>
        (rastrear ? contexto.SemanasCasa : contexto.SemanasCasa.AsNoTracking())
            .Where(s => s.CasaId == casaId)
            .OrderByDescending(s => s.GeradaEm).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync();
}
