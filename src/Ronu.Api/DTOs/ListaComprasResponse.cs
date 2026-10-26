namespace Ronu.Api.DTOs;

/// <summary>
/// A lista de compras da dieta atual (GET /api/dietas/atual/lista-compras), calculada na hora a partir da
/// dieta e da ConversaoCompras. Não leva nenhum dado de saúde (peso, meta, kcal, objetivo): é o que a tela
/// mostra e o que o "Compartilhar" manda.
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

    public required DetalheItemListaComprasDto Detalhe { get; set; }
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
}

public class PassoListaComprasDto
{
    // "par-cru-cozido", "descarte", "unidade", "receita", "calibracao" ou "derivacao".
    public required string Tipo { get; set; }
    public required string Descricao { get; set; }
    public required decimal Fator { get; set; }
}
