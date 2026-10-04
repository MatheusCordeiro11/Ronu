using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Controllers;
using Ronu.Api.Data;
using Ronu.Api.DTOs;
using Ronu.Api.Validacao;

namespace Ronu.Api.Tests;

/// <summary>
/// O 400 da validação automática no formato { mensagem }, sempre em português
/// (RespostaValidacao e os atributos dos DTOs), e o tipo das preferências
/// alimentares (PreferenciasAlimentaresController).
/// </summary>
public class ValidacaoRequisicaoTests
{
    private static readonly string[] ParametrosDaAction = { "request" };

    private static string Mensagem(ModelStateDictionary modelState) =>
        RespostaValidacao.PrimeiraMensagem(modelState, ParametrosDaAction);

    // ---------- RespostaValidacao ----------

    [Fact]
    public void Erro_De_Atributo_Devolve_A_Mensagem_Do_Atributo()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Senha", "A senha deve ter pelo menos 8 caracteres.");

        Assert.Equal("A senha deve ter pelo menos 8 caracteres.", Mensagem(modelState));
    }

    [Fact]
    public void Com_Varios_Erros_Devolve_O_Primeiro()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Nome", "Informe seu nome.");
        modelState.AddModelError("Senha", "A senha deve ter pelo menos 8 caracteres.");

        Assert.Equal("Informe seu nome.", Mensagem(modelState));
    }

    [Theory]
    // Tipo errado / JSON malformado (chave com o caminho do System.Text.Json).
    [InlineData("$.peso", "The JSON value could not be converted to System.Decimal. Path: $.peso")]
    // Campo `required` ausente no JSON.
    [InlineData("$", "JSON deserialization for type 'Ronu.Api.DTOs.ObjetivoRequest' was missing required properties including: 'peso'.")]
    // Corpo vazio.
    [InlineData("", "A non-empty request body is required.")]
    // Corpo "null": erro no parâmetro inteiro.
    [InlineData("request", "The request field is required.")]
    public void Erro_Do_Framework_Vira_Mensagem_Generica_Em_Portugues(string chave, string mensagemEmIngles)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(chave, mensagemEmIngles);

        Assert.Equal(RespostaValidacao.MensagemGenerica, Mensagem(modelState));
    }

    [Fact]
    public void Erro_Do_Framework_Tem_Prioridade_Sobre_Erro_De_Atributo()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Nome", "Informe seu nome.");
        modelState.AddModelError("$.peso", "The JSON value could not be converted to System.Decimal.");

        Assert.Equal(RespostaValidacao.MensagemGenerica, Mensagem(modelState));
    }

    [Fact]
    public void Parametro_De_Rota_Invalido_Vira_Mensagem_Generica()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("id", "The value 'abc' is not valid.");

        Assert.Equal(RespostaValidacao.MensagemGenerica, RespostaValidacao.PrimeiraMensagem(modelState, new[] { "id" }));
    }

    // Mensagem do framework com o nome do campo como chave (ex.: o [Required]
    // implícito, se algum DTO novo ficar sem o explícito): não é uma das
    // mensagens dos DTOs, então não passa.
    [Fact]
    public void Mensagem_Fora_Dos_Dtos_Vira_Mensagem_Generica()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Nome", "The Nome field is required.");

        Assert.Equal(RespostaValidacao.MensagemGenerica, Mensagem(modelState));
    }

    [Fact]
    public void Erro_Com_Excecao_Vira_Mensagem_Generica()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Peso", new FormatException("Input string was not in a correct format."), new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal)));

        Assert.Equal(RespostaValidacao.MensagemGenerica, Mensagem(modelState));
    }

    // ---------- Atributos dos DTOs ----------

    private static IEnumerable<Type> TiposRequest() =>
        typeof(CadastroRequest).Assembly.GetTypes()
            .Where(t => t.Namespace == "Ronu.Api.DTOs" && t.Name.EndsWith("Request"));

    // Toda string (ou array) obrigatória precisa de [Required] explícito com
    // ErrorMessage: sem ele, o ASP.NET usa o implícito, em inglês.
    [Fact]
    public void Toda_Referencia_Nao_Anulavel_Dos_Requests_Tem_Required_Em_Portugues()
    {
        var nulabilidade = new NullabilityInfoContext();
        var faltando = new List<string>();

        foreach (var tipo in TiposRequest())
        {
            foreach (var propriedade in tipo.GetProperties())
            {
                if (propriedade.PropertyType.IsValueType) continue;
                if (nulabilidade.Create(propriedade).WriteState == NullabilityState.Nullable) continue;

                var required = propriedade.GetCustomAttribute<RequiredAttribute>();
                if (required is null || string.IsNullOrWhiteSpace(required.ErrorMessage))
                {
                    faltando.Add($"{tipo.Name}.{propriedade.Name}");
                }
            }
        }

        Assert.Empty(faltando);
    }

    [Fact]
    public void Todo_Atributo_De_Validacao_Dos_Requests_Tem_ErrorMessage()
    {
        var semMensagem = TiposRequest()
            .SelectMany(tipo => tipo.GetProperties().Select(p => (tipo, p)))
            .SelectMany(x => x.p.GetCustomAttributes<ValidationAttribute>()
                .Where(a => string.IsNullOrWhiteSpace(a.ErrorMessage))
                .Select(a => $"{x.tipo.Name}.{x.p.Name} [{a.GetType().Name}]"))
            .ToList();

        Assert.Empty(semMensagem);
    }

    private static List<string> Validar(object request)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), resultados, validateAllProperties: true);
        return resultados.Select(r => r.ErrorMessage!).ToList();
    }

    [Fact]
    public void Campo_Vazio_E_Senha_Curta_Dao_Mensagens_Em_Portugues()
    {
        var mensagens = Validar(new CadastroRequest { Nome = "", Email = "a@b.c", Senha = "123", Estado = "SP" });

        Assert.Equal(new[] { "Informe seu nome.", "A senha deve ter pelo menos 8 caracteres." }, mensagens);
    }

    [Fact]
    public void Dias_Da_Semana_Nulo_Da_Mensagem_Em_Portugues()
    {
        var mensagens = Validar(new UsuarioModalidadeRequest { ModalidadeId = 1, DiasSemana = null!, DuracaoMediaHoras = 1 });

        Assert.Equal(new[] { "Selecione pelo menos um dia da semana." }, mensagens);
    }

    // ---------- Tipo da preferência alimentar ----------

    // O contexto nunca chega a conectar: o tipo é conferido antes de qualquer
    // acesso ao banco.
    private static PreferenciasAlimentaresController Controller() =>
        new(new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=banco-que-nao-existe;Database=x")
            .Options));

    [Theory]
    [InlineData("Evitar")]
    [InlineData("PREFERIDO")]
    [InlineData("evitar ")]
    [InlineData("gosto")]
    [InlineData("")]
    public async Task Tipo_Fora_Dos_Dois_Valores_Recusa_Com_400(string tipo)
    {
        var resultado = await Controller().Criar(new PreferenciaAlimentarRequest { Alimento = "Arroz", Tipo = tipo });

        var badRequest = Assert.IsType<BadRequestObjectResult>(resultado);
        var mensagem = badRequest.Value!.GetType().GetProperty("mensagem")!.GetValue(badRequest.Value);
        Assert.Equal("Tipo de preferência inválido.", mensagem);
    }
}
