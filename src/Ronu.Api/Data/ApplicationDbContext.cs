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
    public DbSet<AlergiaUsuario> AlergiasUsuario { get; set; }
    public DbSet<Casa> Casas { get; set; }
    public DbSet<MembroCasa> MembrosCasa { get; set; }
    public DbSet<ConviteCasa> ConvitesCasa { get; set; }
    public DbSet<SemanaCasa> SemanasCasa { get; set; }

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

        // Apagar a conta apaga as alergias (o índice único por pessoa e código está no modelo).
        modelBuilder.Entity<AlergiaUsuario>()
            .HasOne(a => a.Usuario).WithMany().HasForeignKey(a => a.UsuarioId).OnDelete(DeleteBehavior.Cascade);

        // Plano família. O índice único em UsuarioId é o "uma casa por pessoa": barra dois aceites de convites
        // diferentes ao mesmo tempo, que a checagem no controller sozinha deixaria passar. O parcial em CasaId é o
        // "um dono por casa" (a transferência rebaixa o dono antes de promover o outro, por isso em dois passos).
        modelBuilder.Entity<MembroCasa>(membro =>
        {
            membro.HasIndex(m => m.UsuarioId).IsUnique();
            membro.HasIndex(m => m.CasaId)
                .IsUnique()
                .HasFilter($"\"Papel\" = '{MembroCasa.PapelDono}'");
            membro.HasOne(m => m.Casa).WithMany().HasForeignKey(m => m.CasaId).OnDelete(DeleteBehavior.Cascade);
            membro.HasOne(m => m.Usuario).WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        // O token é procurado pelo hash; os abertos da casa, pela data. Apagar a casa ou quem gerou apaga o convite.
        modelBuilder.Entity<ConviteCasa>(convite =>
        {
            convite.HasIndex(c => c.TokenHash).IsUnique();
            convite.HasIndex(c => new { c.CasaId, c.CriadoEm });
            convite.HasOne(c => c.Casa).WithMany().HasForeignKey(c => c.CasaId).OnDelete(DeleteBehavior.Cascade);
            convite.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        });

        // A semana atual é a mais recente da casa (CasaId, GeradaEm). Acaba com a casa.
        modelBuilder.Entity<SemanaCasa>(semana =>
        {
            semana.HasIndex(s => new { s.CasaId, s.GeradaEm });
            semana.HasOne(s => s.Casa).WithMany().HasForeignKey(s => s.CasaId).OnDelete(DeleteBehavior.Cascade);
        });

        // As dietas que existem antes da casa são todas de geração individual.
        modelBuilder.Entity<DietaIA>()
            .Property(d => d.Origem)
            .HasDefaultValue(DietaIA.OrigemPessoa);
    }
}