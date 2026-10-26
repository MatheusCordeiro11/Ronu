using System.Globalization;
using System.Text;
using Ronu.Api.Data;

namespace Ronu.Api.Services.IA;

/// <summary>
/// Casa o texto livre de uma preferência ("gosto" ou "evitar") com os itens da
/// base nutricional (fase 2b, 2026-10-25). Serve para tirar da lista do Gemini o
/// que a pessoa quer evitar e para saber se um "gosto" existe na base.
///
/// Como casa, em cada trecho do texto (separado por vírgula, ponto e vírgula,
/// barra, " e " ou " ou "):
/// 1. Expressões da Tabela, sem acento, sem maiúsculas, por palavra inteira e
///    aceitando o plural. A expressão mais longa vence: "carne vermelha" não
///    aciona também "carne", e "batata doce" não aciona "doce".
/// 2. As palavras que sobram da tabela (sem as palavras vazias) casam pelo nome
///    dos itens: primeiro o trecho inteiro como nome de um item ("pão de queijo",
///    "leite integral"), depois a sobra junta ("arroz integral" pega só o arroz
///    integral; "frango com batata", as batatas) e, se nada casar, cada palavra
///    separada ("banana maçã" pega as bananas e a maçã).
///
/// A tabela foi aprovada linha a linha em 2026-10-25. Na dúvida, ela tira mais,
/// porque é segurança: "carne" sozinha tira também aves e peixes, "leite" tira
/// tudo o que tem lactose. Um nome composto da base que contém uma palavra da
/// tabela ganha linha própria quando a linha da palavra não tem a ver com ele
/// ("batata doce" e "doce"); nos alérgenos (leite, ovo, amendoim e castanhas),
/// o nome composto continua tirando a família inteira, por segurança.
/// </summary>
public static class CasamentoPreferencias
{
    public record LinhaDaTabela(string[] Expressoes, Func<AlimentoBase, bool> Tira);

    private static bool Carne(AlimentoBase i) => i.Categoria is CategoriaAlimento.CarneBovina or CategoriaAlimento.CarneSuina
        or CategoriaAlimento.Aves or CategoriaAlimento.Peixes;
    private static bool Bovino(AlimentoBase i) => i.Categoria == CategoriaAlimento.CarneBovina;
    private static bool Suino(AlimentoBase i) => i.Categoria == CategoriaAlimento.CarneSuina;
    private static bool Ave(AlimentoBase i) => i.Categoria == CategoriaAlimento.Aves;
    private static bool Peixe(AlimentoBase i) => i.Categoria == CategoriaAlimento.Peixes;
    private static bool Tem(AlimentoBase i, Alergenos a) => i.Alergenos.HasFlag(a);
    private static Func<AlimentoBase, bool> Ids(params string[] ids) => i => ids.Contains(i.Id);

    private static readonly string[] Acucares = ["acucar-refinado", "acucar-mascavo", "mel", "refrigerante-de-cola", "refrigerante-de-guarana"];

    public static readonly IReadOnlyList<LinhaDaTabela> Tabela =
    [
        // Sozinha, "carne" pode ser só a vermelha ou toda carne (como diz um vegetariano): tira todas.
        new(["carne", "vegetariano", "vegetariana"], Carne),
        // OMS/IARC (2015): carne vermelha é o músculo de mamífero, porco incluído.
        new(["carne vermelha"], i => Bovino(i) || Suino(i)),
        // Há quem chame porco de carne branca: entra.
        new(["carne branca"], i => Ave(i) || Peixe(i) || Suino(i)),
        new(["carne bovina", "carne de vaca", "carne de boi", "boi", "bovino", "bovina", "vaca"], Bovino),
        // Nome composto da base ("Carne moída de acém"): sem esta linha, "carne" tiraria aves e peixes.
        new(["carne moída"], Bovino),
        new(["porco", "carne de porco", "suíno", "suína", "carne suína"], Suino),
        new(["frango", "ave", "carne de frango"], Ave),
        // "Ovo de galinha" casaria pela metade; os ovos saem inteiros.
        new(["galinha"], i => Ave(i) || i.Categoria == CategoriaAlimento.Ovos),
        new(["peixe", "pescado"], Peixe),
        // A base não tem crustáceo nem molusco; quem escreve "frutos do mar" pode querer dizer "nada do mar".
        new(["frutos do mar", "fruto do mar", "marisco", "camarão", "crustáceo"], Peixe),
        // Leite pode ser gosto, intolerância ou alergia à proteína do leite: sai tudo o que tem lactose.
        new(["leite", "lactose", "laticínio", "lácteo", "derivados do leite", "derivados de leite"], i => Tem(i, Alergenos.Lactose)),
        new(["queijo"], Ids("queijo-minas-frescal", "queijo-mucarela", "queijo-prato", "queijo-parmesao", "ricota", "requeijao-cremoso", "pao-de-queijo-assado")),
        new(["iogurte"], Ids("iogurte-natural", "iogurte-natural-desnatado")),
        // "Trigo" tira também a aveia (contaminação cruzada) e o cuscuz.
        new(["glúten", "celíaco", "celíaca", "trigo", "cevada", "centeio"], i => Tem(i, Alergenos.Gluten)),
        // "Ovo de galinha" é nome composto da base: sem esta linha, "galinha" tiraria também as aves.
        new(["ovo", "ovo de galinha"], i => Tem(i, Alergenos.Ovo)),
        // As castanhas estão marcadas como amendoim na base (falta marca própria, fase 2c); por segurança, nos
        // dois sentidos, e a amêndoa vai junto com as castanhas.
        new(["amendoim", "castanha", "noz", "oleaginosa", "amêndoa", "caju", "castanha de caju", "castanha do pará", "castanha do brasil"],
            i => Tem(i, Alergenos.Amendoim)),
        new(["semente"], Ids("linhaca")),
        // O óleo refinado quase não tem a proteína alergênica da soja, mas na dúvida sai.
        new(["soja"], Ids("oleo-de-soja")),
        new(["milho"], Ids("cuscuz-de-milho-cozido", "milho-verde-em-conserva")),
        // Quem evita açúcar pode ter diabetes; frutas e suco natural ficam.
        new(["açúcar", "doce"], Ids(Acucares)),
        // Nomes compostos da base com uma palavra da linha acima.
        new(["batata doce"], Ids("batata-doce-cozida")),
        new(["açaí sem açúcar"], Ids("acai-sem-acucar")),
        new(["cafeína"], Ids("cafe-coado", "cha-mate", "cha-preto", "refrigerante-de-cola", "refrigerante-de-guarana")),
        new(["café"], Ids("cafe-coado")),
        new(["refrigerante"], Ids("refrigerante-de-cola", "refrigerante-de-guarana")),
        new(["álcool", "bebida alcoólica", "bebida"], i => i.Categoria == CategoriaAlimento.BebidasAlcoolicas),
        new(["vegano", "vegana"], i => Carne(i) || Tem(i, Alergenos.Ovo) || Tem(i, Alergenos.Lactose) || i.Id == "mel"),
    ];

    // Palavras que não identificam alimento: ficam de fora do casamento por nome. Os preparos entram aqui
    // para "frango grelhado" não casar com todo item grelhado.
    private static readonly HashSet<string> PalavrasVazias =
    [
        "de", "do", "da", "dos", "das", "com", "sem", "em", "no", "na", "nos", "nas", "a", "o", "as", "os", "um", "uma",
        "nao", "como", "gosto", "evito", "evitar", "odeio", "muito", "pouco", "tipo", "qualquer", "todo", "toda", "tudo",
        "comer", "sou", "tenho", "alergia", "alergico", "alergica", "intolerancia", "intolerante", "ao", "pra", "para",
        "nem", "tambem", "nada", "mais", "menos", "so", "apenas", "eu", "me",
        "cozido", "cozida", "cru", "crua", "grelhado", "grelhada", "assado", "assada", "refogado", "refogada",
        "frito", "frita", "torrado", "torrada"
    ];

    private static readonly string[] Separadores = [",", ";", "/", " e ", " ou "];

    // Cada expressão já normalizada e em palavras, da mais longa para a mais curta. O texto original (com
    // acento) vai para o "Lido como" da tela quando a palavra casou pela tolerância a 1 letra.
    private static readonly List<(string[] Palavras, string Texto, LinhaDaTabela Linha)> Expressoes = Tabela
        .SelectMany(l => l.Expressoes.Select(e => (Palavras: Palavras(e), Texto: e, Linha: l)))
        .OrderByDescending(e => e.Palavras.Length)
        .ToList();

    /// <summary>Os itens que a preferência casa e, se nada casou, o texto sem casamento.</summary>
    public static ResultadoCasamento Casar(string texto, IEnumerable<AlimentoBase> itens)
    {
        var lista = itens.ToList();
        var casados = new HashSet<string>();
        var semCasamento = new List<string>();
        var lidoComo = new List<string>();

        foreach (var trecho in Trechos(texto))
        {
            var palavras = Palavras(trecho);
            var (linhas, sobra, lidos) = LinhasDaTabela(palavras);
            lidoComo.AddRange(lidos);
            casados.UnionWith(lista.Where(i => linhas.Any(l => l.Tira(i))).Select(i => i.Id));

            var sobraConteudo = sobra.Where(p => !PalavrasVazias.Contains(p)).ToArray();
            if (sobraConteudo.Length == 0)
            {
                if (linhas.Count == 0 && palavras.Any(p => !PalavrasVazias.Contains(p)))
                {
                    semCasamento.Add(trecho.Trim());
                }

                continue;
            }

            // 1. O trecho inteiro como nome de um item: "pão de queijo" casa com o pão de queijo, e não
            //    também com todos os pães pela sobra "pão"; "leite integral", com o leite integral, e não com
            //    tudo o que é integral.
            var conteudo = palavras.Where(p => !PalavrasVazias.Contains(p)).ToArray();
            var porNome = ItensComTodas(lista, conteudo);

            // 2. Só a sobra, junta: "frango com batata" -> as batatas (frango já casou pela tabela).
            if (porNome.Count == 0)
            {
                porNome = ItensComTodas(lista, sobraConteudo);
            }

            // 3. Cada palavra da sobra separada: "banana maçã" -> as bananas e a maçã.
            if (porNome.Count == 0)
            {
                var semItem = new List<string>();
                foreach (var palavra in sobraConteudo)
                {
                    var daPalavra = ItensComTodas(lista, [palavra]);
                    porNome.AddRange(daPalavra);
                    if (daPalavra.Count == 0)
                    {
                        semItem.Add(palavra);
                    }
                }

                // Nada casou no trecho: volta o trecho como foi escrito. Casou em parte: voltam as palavras
                // que sobraram sem item ("carne de cordeiro" tira as carnes e devolve "cordeiro").
                if (linhas.Count == 0 && porNome.Count == 0)
                {
                    semCasamento.Add(trecho.Trim());
                }
                else
                {
                    semCasamento.AddRange(semItem);
                }
            }

            casados.UnionWith(porNome);
        }

        return new ResultadoCasamento(casados, semCasamento) { LidoComo = lidoComo.Distinct().ToList() };
    }

    /// <summary>
    /// Uma palavra do texto casa com uma palavra da tabela se for igual ou, quando
    /// a da tabela tem 5 letras ou mais, se estiver a 1 letra dela: uma letra a
    /// mais, a menos, trocada ou duas vizinhas invertidas ("lactoze", "glutem",
    /// "gluetn", "amendoin"). É segurança: um erro de digitação num alérgeno
    /// deixaria os itens na lista. Palavras com menos de 5 letras ("ovo", "soja",
    /// "doce") e as palavras vazias ("pouco" está a 1 letra de "porco") só casam
    /// iguais. As palavras dos nomes da base que ficam a 1 letra de alguma da
    /// tabela foram conferidas uma a uma (docs/medicoes/evitar-2b1.md).
    /// </summary>
    public static bool PalavraCasa(string doTexto, string daTabela)
    {
        if (doTexto == daTabela)
        {
            return true;
        }

        return daTabela.Length >= MinimoLetrasTolerancia && !PalavrasVazias.Contains(doTexto) && UmaLetraDeDistancia(doTexto, daTabela);
    }

    public const int MinimoLetrasTolerancia = 5;

    // Distância de edição de no máximo 1, contando a troca de duas letras vizinhas como uma só (Damerau).
    private static bool UmaLetraDeDistancia(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
        {
            return false;
        }

        if (a.Length == b.Length)
        {
            var diferencas = Enumerable.Range(0, a.Length).Where(k => a[k] != b[k]).ToList();
            return diferencas.Count == 1
                || (diferencas.Count == 2 && diferencas[1] == diferencas[0] + 1
                    && a[diferencas[0]] == b[diferencas[1]] && a[diferencas[1]] == b[diferencas[0]]);
        }

        // Uma letra a mais em um dos lados: tirar uma letra da maior deixa as duas iguais.
        var (maior, menor) = a.Length > b.Length ? (a, b) : (b, a);
        return Enumerable.Range(0, maior.Length).Any(k => maior.Remove(k, 1) == menor);
    }

    private static List<string> ItensComTodas(List<AlimentoBase> lista, string[] palavras) =>
        lista.Where(i => NomesDoItem(i).Any(nome => palavras.All(nome.Contains))).Select(i => i.Id).ToList();

    // Percorre as palavras procurando, em cada posição, a expressão mais longa que casa ali. Devolve as
    // linhas casadas, as palavras que não fizeram parte de nenhuma expressão e as expressões que só casaram
    // pela tolerância a 1 letra ("lactoze" -> "lactose"), para a tela mostrar como foi lido.
    private static (List<LinhaDaTabela> Linhas, List<string> Sobra, List<string> LidoComo) LinhasDaTabela(string[] palavras)
    {
        var linhas = new List<LinhaDaTabela>();
        var sobra = new List<string>();
        var lidoComo = new List<string>();
        for (var i = 0; i < palavras.Length;)
        {
            var achada = Expressoes.FirstOrDefault(e =>
                i + e.Palavras.Length <= palavras.Length && e.Palavras.Select((p, k) => PalavraCasa(palavras[i + k], p)).All(x => x));
            if (achada.Linha is null)
            {
                sobra.Add(palavras[i]);
                i++;
                continue;
            }

            linhas.Add(achada.Linha);
            if (achada.Palavras.Where((p, k) => palavras[i + k] != p).Any())
            {
                lidoComo.Add(achada.Texto);
            }

            i += achada.Palavras.Length;
        }

        return (linhas, sobra, lidoComo);
    }

    private static IEnumerable<HashSet<string>> NomesDoItem(AlimentoBase item) =>
        new[] { item.Nome }.Concat(item.Apelidos.Select(a => a.Nome)).Select(n => Palavras(n).ToHashSet());

    private static IEnumerable<string> Trechos(string texto)
    {
        var normalizado = " " + SemAcento(texto.ToLowerInvariant()) + " ";
        return normalizado.Split(Separadores, StringSplitOptions.RemoveEmptyEntries).Where(t => t.Trim().Length > 0);
    }

    // Sem acento, só letras e números (o hífen de "batata-doce" vira espaço), cada palavra no singular.
    public static string[] Palavras(string texto)
    {
        var limpo = new StringBuilder();
        foreach (var c in SemAcento(texto.ToLowerInvariant()))
        {
            limpo.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return limpo.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Singular).ToArray();
    }

    // Plural simples do português, aplicado igual dos dois lados (texto e nomes): feijões -> feijao,
    // pães -> pao, açúcares -> acucar, nozes -> noz, ovos -> ovo.
    private static string Singular(string palavra)
    {
        if (palavra.Length <= 3)
        {
            return palavra;
        }

        if (palavra.EndsWith("oes") || palavra.EndsWith("aes"))
        {
            return palavra[..^3] + "ao";
        }

        if (palavra.EndsWith("es") && palavra[^3] is 'r' or 'z' or 's')
        {
            return palavra[..^2];
        }

        return palavra.EndsWith('s') ? palavra[..^1] : palavra;
    }

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>Ids dos itens casados e os trechos do texto que não casaram com nada.</summary>
public record ResultadoCasamento(IReadOnlySet<string> ItensIds, IReadOnlyList<string> TrechosSemCasamento)
{
    public bool Casou => ItensIds.Count > 0;

    /// <summary>Expressões da tabela que casaram só pela tolerância a 1 letra, como estão na tabela ("lactose").</summary>
    public IReadOnlyList<string> LidoComo { get; init; } = [];
}
