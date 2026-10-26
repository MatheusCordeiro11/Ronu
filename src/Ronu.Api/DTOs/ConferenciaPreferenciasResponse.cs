namespace Ronu.Api.DTOs;

/// <summary>
/// Como as preferências do usuário logado casaram com a base nutricional e se a
/// lista que sobra atende aos mínimos por papel no prato (fase 2b-3). É o que a
/// tela de Alimentação mostra: o chip sem casamento, o "tira 13" do evitar e o
/// aviso de lista pobre. Calculado no servidor (CasamentoPreferencias e
/// ListaAlimentosPessoa), para a tela nunca repetir a regra.
/// </summary>
public class ConferenciaPreferenciasResponse
{
    public required List<PreferenciaConferidaResponse> Preferencias { get; set; }
    public required MinimosListaResponse Lista { get; set; }
}

public class PreferenciaConferidaResponse
{
    public int Id { get; set; }
    public required string Alimento { get; set; }
    public required string Tipo { get; set; }
    public bool Casou { get; set; }

    // Os itens da base que a preferência casa: no "evitar", os que saem do cardápio.
    public required List<AlimentoCasadoResponse> Alimentos { get; set; }

    // Trechos do texto que não casaram com nenhum alimento (o aviso da tela usa o nome).
    public required List<string> SemCasamento { get; set; }

    // Como foi lido, quando casou só pela tolerância a 1 letra ("lactoze" -> "lactose").
    public required List<string> LidoComo { get; set; }
}

public class AlimentoCasadoResponse
{
    public required string Nome { get; set; }

    // Grupo de troca (GrupoDeTroca), para a tela agrupar listas longas.
    public required string Grupo { get; set; }
}

public class MinimosListaResponse
{
    public bool Atende { get; set; }

    // Papéis ("Proteina", "Carboidrato", "Fruta", "Hortalica") sem nenhum item: a dieta não pode ser gerada.
    public required List<string> Vazios { get; set; }

    // Papéis abaixo do mínimo, sem ficarem vazios: a dieta sai, com o aviso de lista pobre.
    public required List<string> AbaixoDoMinimo { get; set; }

    public required Dictionary<string, int> ItensPorPapel { get; set; }
    public int GruposDeProteina { get; set; }
}
