namespace Ronu.Api.DTOs;

/// <summary>
/// A lista de compras da dieta atual (GET /api/dietas/atual/lista-compras), calculada na hora a partir da
/// dieta e da ConversaoCompras. Não leva peso, meta, kcal nem objetivo: é o que a tela mostra e o que o
/// "Compartilhar" manda. A exceção, desde a TACO 2c, é a marca de alergia de cada item (TemAlergeno e Alergias),
/// que é só para a tela e fica fora do "Compartilhar".
/// </summary>
public class ListaComprasResponse
{
    // Id da DietaIA de onde a lista saiu: a marcação "já tenho" (etapa 3) manda de volta para o servidor
    // conferir que a dieta não mudou com a lista aberta.
    public required int DietaId { get; set; }
    public required DateTime DataGeracao { get; set; }
    public required int VersaoConversao { get; set; }

    // Falso nas dietas geradas antes da lista fechada (sem AlimentoId): a tela pede uma dieta nova em vez
    // de adivinhar o item pelo nome. Motivo = "dieta-antiga".
    public required bool Disponivel { get; set; }
    public string? Motivo { get; set; }

    // A dieta tem refogado ou ovo frito: a TACO já conta o óleo do preparo no prato, e ele não vira item.
    public bool AvisoOleo { get; set; }

    public List<SecaoListaComprasDto> Secoes { get; set; } = [];
}

public class SecaoListaComprasDto
{
    public required string Chave { get; set; }
    public required string Nome { get; set; }
    public required List<ItemListaComprasDto> Itens { get; set; }
}

public class ItemListaComprasDto
{
    public required string Chave { get; set; }

    // O nome regional quando a UF da pessoa tem um (Aipim, Macaxeira).
    public required string Nome { get; set; }

    // Já arredondada para cima: unidades inteiras, ou passos de 10 g/ml até 100 e de 50 acima disso.
    public required decimal Quantidade { get; set; }

    // "g", "ml", "unidades", "fatias" ou "saches".
    public required string Unidade { get; set; }

    // A quantidade pronta para exibir e compartilhar: "7 bananas", "550 g", "1,25 kg", "1,05 L".
    public required string Texto { get; set; }

    // A quantidade é do prato pronto (etiqueta "pronto"): não há fonte para converter.
    public bool Pronto { get; set; }

    // Aviso que vai na própria linha ("de carne, sem osso · compre um pouco mais").
    public string? Nota { get; set; }

    // Marcado como "já tenho" nesta dieta (tabela ListaComprasMarcacoes). Preenchido pelo controller.
    public bool Tenho { get; set; }

    // TACO 2c: algum item da dieta que entra nesta linha tem uma alergia marcada pela pessoa; Alergias traz os
    // códigos dos chips ("soja"), na ordem da tela. Preenchidos pelo controller. São dado de saúde: a tela mostra
    // o aviso, mas o "Compartilhar" não deve levá-los.
    public bool TemAlergeno { get; set; }
    public List<string> Alergias { get; set; } = [];

    // Desde 2026-11-16: o que se compra em embalagens ("1 pacote de 1 kg"), quando o item tem um tamanho comum e
    // cabe em até 4 embalagens. Nulo nos outros casos (granel, contáveis, prontos, itens fora da v1, mais de 4
    // embalagens). Só a forma de mostrar: Quantidade, Unidade e Texto continuam iguais, e o "já tenho" compara
    // por eles.
    public EmbalagemListaComprasDto? Embalagem { get; set; }

    public required DetalheItemListaComprasDto Detalhe { get; set; }
}

/// <summary>
/// A quantidade em embalagens, calculada sobre a quantidade exata (antes do arredondamento de 10 em 10 g e
/// 50 em 50 g). Na lista da casa, sobre o total da casa.
/// </summary>
public class EmbalagemListaComprasDto
{
    // "embalagem" (pacote, caixa, lata, dúzia) ou "peca" (frango inteiro abaixo da peça mínima: a peça real
    // pesa isso ou mais, e a tela sugere trocar o alimento no painel).
    public required string Tipo { get; set; }

    // Pronto para mostrar e compartilhar: "1 pacote de 1 kg", "2 latas de 350 ml", "3 dúzias", "1 peça de 1 kg ou mais".
    public required string Texto { get; set; }

    // Quantas embalagens (1 a 4).
    public required int Quantidade { get; set; }

    // As duas partes do Texto, para a tela mostrar em duas camadas ("1 pacote" em cima, "de 1 kg" embaixo):
    // Nome já no número certo ("pacote", "pacotes", "dúzias", "peça") e Rotulo, o tamanho ("1 kg", "350 ml",
    // "1 kg ou mais"), vazio quando a embalagem não tem tamanho (a dúzia). Texto = "{Quantidade} {Nome}" + " de {Rotulo}".
    public required string Nome { get; set; }
    public required string Rotulo { get; set; }

    // Quantidade × tamanho, na unidade da linha (Unidade do item): 1000 no "1 pacote de 1 kg", 16 no pacote de
    // pão de forma de 400 g (fatias).
    public required decimal TamanhoTotal { get; set; }

    // TamanhoTotal − quantidade exata, com 2 casas: o que sobra da embalagem na semana. Não é guardada.
    public required decimal Sobra { get; set; }

    // A sobra pronta para mostrar depois de "cerca de": "390 g", "1,3 L", "4 fatias", "5 ovos". Gramas e ml
    // arredondados para os 10 mais próximos; contáveis, fatias e sachês para baixo (a unidade inteira que sobra).
    // "" quando não sobra nada que valha dizer. No frango ("peca"), é o mínimo: a peça pode pesar mais.
    public required string SobraTexto { get; set; }

    // De onde vem o tamanho, para o "Ver detalhe".
    public required string Fonte { get; set; }
}

/// <summary>O "Ver detalhe": a quantidade exata, de onde ela vem e a conta, com a fonte.</summary>
public class DetalheItemListaComprasDto
{
    // Antes do arredondamento, na mesma unidade de Quantidade, com 2 casas.
    public required decimal QuantidadeExata { get; set; }
    public required List<OrigemItemListaComprasDto> Origens { get; set; }
    public List<string> Observacoes { get; set; } = [];
}

/// <summary>Um item da dieta que entra nesta linha (ovo cozido, ovo frito e clara entram em "Ovos").</summary>
public class OrigemItemListaComprasDto
{
    public required string AlimentoId { get; set; }
    public required string Nome { get; set; }

    // Soma da semana, como está na dieta ("g" ou "ml").
    public required decimal QuantidadeNaDieta { get; set; }
    public required string Medida { get; set; }

    public required decimal Fator { get; set; }

    // QuantidadeNaDieta × Fator, na unidade da linha, com 2 casas.
    public required decimal Convertido { get; set; }

    public required List<PassoListaComprasDto> Passos { get; set; }

    // TACO 2c: os chips marcados pela pessoa que tiram este alimento, na ordem da tela (o "tem Soja" do "Ver
    // detalhe", para saber qual alimento trocar). Preenchido pelo controller; o "Compartilhar" não leva.
    public List<string> Alergias { get; set; } = [];
}

public class PassoListaComprasDto
{
    // "par-cru-cozido", "descarte", "unidade", "receita", "calibracao" ou "derivacao".
    public required string Tipo { get; set; }
    public required string Descricao { get; set; }
    public required decimal Fator { get; set; }
}
