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
    public DbSet<MetaDieta> MetasDieta { get; set; }
    public DbSet<PedidoRedefinicaoSenha> PedidosRedefinicaoSenha { get; set; }
    public DbSet<ListaComprasMarcacao> ListaComprasMarcacoes { get; set; }
    public DbSet<Consentimento> Consentimentos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Uma conta por email. O email é sempre gravado normalizado (minúsculas,
        // sem espaços nas pontas: NormalizacaoEmail), então o índice comum já
        // compara sem diferenciar maiúsculas.
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // O token é procurado pelo hash; o limite por conta conta os pedidos
        // recentes de cada usuário. Apagar o usuário apaga os pedidos.
        modelBuilder.Entity<PedidoRedefinicaoSenha>(pedido =>
        {
            pedido.HasIndex(p => p.TokenHash).IsUnique();
            pedido.HasIndex(p => new { p.UsuarioId, p.CriadoEm });
            pedido.HasOne(p => p.Usuario).WithMany().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        // A marcação "já tenho" vive com a dieta: quando a regra das 3 dietas apaga a DietaIA, ou quando
        // a conta é apagada, ela sai junto. O PostgreSQL aceita os dois caminhos de cascata.
        modelBuilder.Entity<ListaComprasMarcacao>(marcacao =>
        {
            marcacao.HasOne(m => m.Usuario).WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            marcacao.HasOne(m => m.DietaIA).WithMany().HasForeignKey(m => m.DietaIAId).OnDelete(DeleteBehavior.Cascade);
        });

        // Apagar a conta apaga os aceites. O índice único parcial barra dois aceites valendo da mesma versão
        // (duas abas aceitando ao mesmo tempo); os revogados ficam fora dele, para o histórico poder repetir.
        modelBuilder.Entity<Consentimento>(consentimento =>
        {
            consentimento.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            consentimento.HasIndex(c => new { c.UsuarioId, c.Tipo, c.Versao })
                .IsUnique()
                .HasFilter("\"RevogadoEm\" IS NULL");
        });
    }
}