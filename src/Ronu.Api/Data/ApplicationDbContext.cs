using Microsoft.EntityFrameworkCore;
using Ronu.Api.Models;

namespace Ronu.Api.Data;

/// <summary>
/// Contexto do Entity Framework Core que representa a conexão com o banco de dados
/// (PostgreSQL) e expõe cada tabela do sistema como um DbSet.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Modalidade> Modalidades { get; set; }
    public DbSet<UsuarioModalidade> UsuarioModalidades { get; set; }
    public DbSet<ObjetivoUsuario> ObjetivosUsuario { get; set; }
    public DbSet<PreferenciaAlimentar> PreferenciasAlimentares { get; set; }
    public DbSet<DietaIA> DietasIA { get; set; }
}