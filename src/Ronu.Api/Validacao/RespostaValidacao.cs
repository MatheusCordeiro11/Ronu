using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ronu.Api.Validacao;

/// <summary>
/// Monta a mensagem do 400 da validação automática do [ApiController] no
/// mesmo formato dos demais erros da API ({ mensagem }), que é o que o
/// frontend lê. Usada pelo InvalidModelStateResponseFactory (Program.cs).
/// </summary>
public static class RespostaValidacao
{
    // Para os erros que não vêm de um atributo dos DTOs (todos com
    // ErrorMessage em português): JSON malformado ou com tipo errado, campo
    // obrigatório ausente no JSON, corpo vazio, parâmetro de rota inválido.
    // As mensagens desses vêm do framework, em inglês, e nunca vão para a tela.
    public const string MensagemGenerica = "Requisição inválida. Confira os dados enviados.";

    // As mensagens escritas nos atributos de validação dos DTOs — as únicas
    // que podem ir para a resposta. Qualquer outra (as do framework variam:
    // "The supplied value is invalid.", "The value 'x' is not valid." etc.,
    // às vezes com o nome do campo como chave) vira a genérica, então nada
    // em inglês chega à tela, mesmo que surja um caso não previsto aqui.
    private static readonly HashSet<string> MensagensDosDtos = typeof(RespostaValidacao).Assembly
        .GetTypes()
        .Where(tipo => tipo.Namespace == "Ronu.Api.DTOs")
        .SelectMany(tipo => tipo.GetProperties())
        .SelectMany(propriedade => propriedade.GetCustomAttributes(typeof(ValidationAttribute), inherit: true))
        .Select(atributo => ((ValidationAttribute)atributo).ErrorMessage)
        .OfType<string>()
        .ToHashSet();

    /// <summary>
    /// A primeira mensagem de erro do ModelState, se for uma das mensagens dos
    /// DTOs. Se houver qualquer erro do framework (chave "$..." do
    /// System.Text.Json, chave vazia de corpo ausente, chave igual ao nome de
    /// um parâmetro da action, ou erro com exceção), devolve a genérica: nesses
    /// casos a mensagem de um atributo, se houver, seria só consequência do
    /// corpo inválido.
    /// </summary>
    public static string PrimeiraMensagem(ModelStateDictionary modelState, IEnumerable<string> nomesParametros)
    {
        var comErro = modelState
            .Where(entrada => entrada.Value is { Errors.Count: > 0 })
            .ToList();

        if (comErro.Count == 0)
        {
            return MensagemGenerica;
        }

        var parametros = new HashSet<string>(nomesParametros, StringComparer.OrdinalIgnoreCase);

        var erroDoFramework = comErro.Any(entrada =>
            entrada.Key.Length == 0
            || entrada.Key.StartsWith('$')
            || parametros.Contains(entrada.Key)
            || entrada.Value!.Errors.Any(erro => erro.Exception is not null));

        if (erroDoFramework)
        {
            return MensagemGenerica;
        }

        var mensagem = comErro[0].Value!.Errors[0].ErrorMessage;
        return MensagensDosDtos.Contains(mensagem) ? mensagem : MensagemGenerica;
    }
}
