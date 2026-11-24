using Microsoft.EntityFrameworkCore;
using ramii.dev.Core.Entities;

namespace ramii.dev.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<ContactThread> Threads => Set<ContactThread>();
    public DbSet<ThreadMessage> Messages => Set<ThreadMessage>();

    public DbSet<LabProject> LabProjects => Set<LabProject>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<ContactThread>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Subject).HasMaxLength(200).IsRequired();
            entity.Property(e => e.VisitorName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.VisitorEmail).HasMaxLength(255).IsRequired();

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastActivityAt);

            // 1 tråd har många meddelanden
            entity.HasMany(e => e.Messages)
                  .WithOne(m => m.Thread)
                  .HasForeignKey(m => m.ThreadId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ThreadMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Body).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasIndex(e => e.SentAt);
        });

        // Konfiguration för LabProject
        modelBuilder.Entity<LabProject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.TechTags).HasMaxLength(200).IsRequired();
            entity.Property(e => e.GitHubUrl).HasMaxLength(300);
            entity.Property(e => e.DemoUrl).HasMaxLength(300);

            entity.HasIndex(e => e.IsPublished);
            entity.HasIndex(e => e.SortOrder);
        });
    }
}
