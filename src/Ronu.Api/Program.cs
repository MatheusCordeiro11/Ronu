using Microsoft.EntityFrameworkCore;
using Ronu.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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

app.UseAuthorization();

app.MapControllers();

app.Run();
