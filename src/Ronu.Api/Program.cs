using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
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
            .WithOrigins("http://localhost:5500", "http://127.0.0.1:5500")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Registra o ApplicationDbContext usando PostgreSQL (via Npgsql) como provedor,
// lendo a string de conexão da configuração (appsettings/variáveis de ambiente).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var chaveJwt = builder.Configuration["Jwt:ChaveSecreta"]!;

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

var app = builder.Build();

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
