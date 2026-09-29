using CentralChamados.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace CentralChamados.Persistence;

public sealed class ChamadosDbContext(DbContextOptions<ChamadosDbContext> options) : IdentityDbContext<Conta, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Chamado> Chamados => Set<Chamado>();
    public DbSet<EventoChamado> Eventos => Set<EventoChamado>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Conta>().HasOne<Usuario>().WithOne().HasForeignKey<Conta>(c => c.ParticipanteId).OnDelete(DeleteBehavior.Restrict);
        var usuario = modelBuilder.Entity<Usuario>();
        usuario.HasKey(u => u.Id);
        usuario.Property(u => u.Id).ValueGeneratedNever();
        usuario.Property(u => u.Nome).HasMaxLength(100).IsRequired();
        usuario.ToTable("Usuarios", t => t.HasCheckConstraint("CK_Usuarios_Nome", "length(trim(Nome)) BETWEEN 1 AND 100"));

        var chamado = modelBuilder.Entity<Chamado>();
        chamado.HasKey(c => c.Id);
        chamado.Property(c => c.Id).ValueGeneratedNever();
        chamado.Property(c => c.Titulo).HasMaxLength(100).IsRequired();
        chamado.Property(c => c.Descricao).HasMaxLength(2000).IsRequired();
        chamado.Property(c => c.Solucao).HasMaxLength(2000);
        chamado.Property(c => c.Prioridade).HasConversion<int>();
        chamado.HasIndex(c => c.Prioridade);
        chamado.Property(c => c.Status).HasConversion<string>();
        chamado.HasOne<Usuario>().WithMany().HasForeignKey(c => c.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
        chamado.HasOne<Usuario>().WithMany().HasForeignKey(c => c.TecnicoId).OnDelete(DeleteBehavior.Restrict);
        chamado.HasMany(c => c.Historico).WithOne().HasForeignKey("ChamadoId").OnDelete(DeleteBehavior.Restrict);
        chamado.Navigation(c => c.Historico).HasField("historico").UsePropertyAccessMode(PropertyAccessMode.Field);
        chamado.HasIndex(c => c.Status);
        chamado.ToTable("Chamados", t => {
            t.HasCheckConstraint("CK_Chamados_Prioridade", "Prioridade BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_Chamados_Titulo", "length(trim(Titulo)) BETWEEN 1 AND 100");
            t.HasCheckConstraint("CK_Chamados_Descricao", "length(trim(Descricao)) BETWEEN 1 AND 2000");
            t.HasCheckConstraint("CK_Chamados_Estado",
                "(Status = 'Aberto' AND TecnicoId IS NULL AND Tecnico IS NULL AND Solucao IS NULL) OR " +
                "(Status = 'EmAtendimento' AND TecnicoId IS NOT NULL AND Tecnico IS NOT NULL AND Solucao IS NULL) OR " +
                "(Status = 'Resolvido' AND TecnicoId IS NOT NULL AND Tecnico IS NOT NULL AND Solucao IS NOT NULL AND length(trim(Solucao)) BETWEEN 1 AND 2000)");
        });

        var evento = modelBuilder.Entity<EventoChamado>();
        evento.HasKey(e => e.Id);
        evento.Property(e => e.Id).ValueGeneratedNever();
        evento.Property<Guid>("ChamadoId");
        evento.HasIndex("ChamadoId", nameof(EventoChamado.Sequencia)).IsUnique();
        evento.HasOne<Usuario>().WithMany().HasForeignKey(e => e.AutorId).OnDelete(DeleteBehavior.Restrict);
        evento.Property(e => e.Autor).HasMaxLength(100).IsRequired();
        evento.Property(e => e.Descricao).HasMaxLength(2100).IsRequired();
        evento.Property(e => e.Em).HasConversion(new ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)));
        evento.ToTable("Eventos", t => t.HasCheckConstraint("CK_Eventos_Sequencia", "Sequencia >= 0"));
    }
}
