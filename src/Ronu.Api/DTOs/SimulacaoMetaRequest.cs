using System.ComponentModel.DataAnnotations;
using Ronu.Api.Validacao;

namespace Ronu.Api.DTOs;

/// <summary>
/// Entrada do POST /api/metas/simulacao. Todos os campos são opcionais: o que
/// não vier usa o dado salvo da pessoa (o peso, o peso de tendência). Assim o
/// onboarding manda o que ainda não salvou, o Perfil manda só o campo que está
/// sendo editado e o Treino manda a lista de modalidades em edição.
/// Faixas iguais às que o frontend já aplica no perfil, no onboarding e no
/// registro de peso (altura 1,00–2,50 m; peso 20–300 kg). A data de
/// nascimento é conferida no MetasController, porque depende do dia de hoje.
/// </summary>
public class SimulacaoMetaRequest
{
    // "Masculino" ou "Feminino" (o mesmo valor do PerfilRequest).
    public string? Sexo { get; set; }

    // Em centímetros. Limites em double: com int, o RangeAttribute converteria
    // o valor para inteiro antes de comparar (19,9 kg viraria 20 e passaria).
    [Range(100.0, 250.0, ErrorMessage = RegrasDadosCorporais.MensagemAltura)]
    public decimal? Altura { get; set; }

    public DateOnly? DataNascimento { get; set; }

    [Range(20.0, 300.0, ErrorMessage = RegrasDadosCorporais.MensagemPeso)]
    public decimal? Peso { get; set; }

    // "perder peso", "manter peso" ou "ganhar peso".
    public string? Objetivo { get; set; }

    // "sentado", "em_pe", "andando" ou "bracal"; ausente usa o nível salvo
    // (e, sem nível salvo, o fator padrão de antes da pergunta).
    public string? NivelAtividade { get; set; }

    // Se vier (mesmo vazia), substitui a lista salva. Mesmas regras do
    // POST /api/usuarios/modalidades.
    public List<UsuarioModalidadeRequest>? Modalidades { get; set; }
}
