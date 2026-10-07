using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ronu.Api.Middleware;
using Ronu.Api.Services;
using Ronu.Api.Services.Email;
using Ronu.Api.Services.IA;
using Ronu.Api.Validacao;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

const string FrontendCorsPolicy = "FrontendLocal";

// O frontend é servido como arquivos estáticos (fora do ASP.NET), então o navegador
// trata cada porta como uma origem diferente. Liberamos só as origens usadas em
// desenvolvimento local (ex.: Live Server), nunca "*", para não abrir a API geral.
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:5500", "http://127.0.0.1:5500", "https://ronu-frontend.vercel.app")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// O 400 automático do [ApiController] (DTO inválido) sai no formato dos demais
// erros da API, { mensagem }, em vez do ValidationProblemDetails padrão, que o
// frontend não lê. Ver RespostaValidacao.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = contexto => new BadRequestObjectResult(new
        {
            mensagem = RespostaValidacao.PrimeiraMensagem(
                contexto.ModelState,
                contexto.ActionDescriptor.Parameters.Select(parametro => parametro.Name))
        });
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Registra o ApplicationDbContext usando PostgreSQL (via Npgsql) como provedor,
// lendo a string de conexão da configuração (appsettings/variáveis de ambiente).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var chaveJwt = builder.Configuration["Jwt:ChaveSecreta"]!;

var geminiOptions = new GeminiOptions { ApiKey = builder.Configuration["Gemini:ApiKey"]! };
builder.Services.AddSingleton(geminiOptions);
builder.Services.AddHttpClient<IGeradorDietaIA, GeradorDietaGemini>();
builder.Services.AddScoped<ICalculadoraGastoCalorico, CalculadoraGastoCalorico>();
builder.Services.AddScoped<ICalculadoraPesoTendencia, CalculadoraPesoTendencia>();
builder.Services.AddScoped<ICalculadoraAjusteAdaptativo, CalculadoraAjusteAdaptativo>();
builder.Services.AddScoped<IContextoDietaBuilder, ContextoDietaBuilder>();
builder.Services.AddScoped<IRepositorioDietaIA, RepositorioDietaIA>();

// Envio de email em segundo plano (FilaEmail + ServicoEnvioEmail). Com
// credencial (User Secrets ou Application settings), SMTP do Gmail; sem ela,
// em desenvolvimento o email vai para o log, e fora dele vira só um erro no log.
var emailOptions = builder.Configuration.GetSection(EmailOptions.Secao).Get<EmailOptions>() ?? new EmailOptions();
builder.Services.AddSingleton(emailOptions);
if (emailOptions.Configurado)
{
    builder.Services.AddSingleton<IEnviadorEmail, EnviadorEmailSmtp>();
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEnviadorEmail, EnviadorEmailLog>();
}
else
{
    builder.Services.AddSingleton<IEnviadorEmail, EnviadorEmailNaoConfigurado>();
}
builder.Services.AddSingleton<FilaEmail>();
builder.Services.AddSingleton<IFilaEmail>(servicos => servicos.GetRequiredService<FilaEmail>());
builder.Services.AddHostedService<ServicoEnvioEmail>();

// Configura a autenticação baseada em JWT Bearer: o servidor valida a assinatura
// do token (com a mesma chave usada para gerá-lo) e a expiração, mas não valida
// issuer/audience pois a API ainda não distingue múltiplos emissores/consumidores.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chaveJwt)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Precisa vir antes de qualquer outro middleware, para capturar exceções
// que aconteçam em qualquer ponto do pipeline abaixo (CORS, autenticação,
// controllers).
app.UseExceptionHandler();

// CORS precisa vir antes de autenticação/autorização para que o navegador já
// receba os headers liberando a origem na resposta (inclusive no preflight).
app.UseCors(FrontendCorsPolicy);

// A ordem importa: autenticação (identifica quem é o usuário a partir do token)
// precisa vir antes da autorização (decide se esse usuário pode acessar a rota).
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
