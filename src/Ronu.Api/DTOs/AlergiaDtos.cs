namespace Ronu.Api.DTOs;

/// <summary>
/// PUT /api/perfil/alergias/{codigo}: a versão do texto de autorização que a tela mostrou, só quando a pessoa acabou de
/// marcar a caixa. Sem ela, o chip só é gravado se já houver um aceite vigente do tipo "alergias". Sem data nem usuário:
/// a data é a do servidor, e o usuário vem do token.
/// </summary>
public class AlergiaRequest
{
    public int? VersaoConsentimento { get; set; }
}

/// <summary>
/// As alergias da pessoa logada (GET, e a resposta do PUT e do DELETE, para a tela redesenhar com o que está gravado
/// mesmo com duas abas abertas).
/// </summary>
public class AlergiasResponse
{
    // Os códigos dos chips marcados, na ordem da tela.
    public required List<string> Alergias { get; set; }

    // Há aceite da versão vigente do tipo "alergias", não revogado: marcar um chip novo não pede a caixa.
    public bool AceiteVigente { get; set; }

    // Quando esse aceite foi dado (a linha "Uso autorizado em ..."); nulo sem aceite vigente.
    public DateTime? AceitoEm { get; set; }
}
