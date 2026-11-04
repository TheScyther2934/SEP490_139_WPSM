using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence;

// Kept apart from the scaffolded context so re-scaffolding does not drop the integration tables.
public partial class WpsmDbContext
{
    public virtual DbSet<StoredFile> StoredFiles { get; set; }

    public virtual DbSet<EmailOutboxMessage> EmailOutbox { get; set; }

    public virtual DbSet<ExceptionRequest> ExceptionRequests { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SpecItem>().ToTable(table => table.HasTrigger("TR_SpecItems_BaselineImmutable"));

        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.Property(e => e.EscalatedAt).HasPrecision(0);
            entity.Property(e => e.ResolvedAt).HasPrecision(0);
        });

        modelBuilder.Entity<ExceptionRequest>(entity =>
        {
            entity.HasKey(e => e.ExceptionRequestId);
            entity.Property(e => e.RequestCode).HasMaxLength(30);
            entity.Property(e => e.SourceType).HasMaxLength(20);
            entity.Property(e => e.Crid).HasColumnName("CRId");
            entity.Property(e => e.RequestNote).HasMaxLength(300);
            entity.Property(e => e.Status).HasMaxLength(10);
            entity.Property(e => e.DecisionNote).HasMaxLength(300);
            entity.Property(e => e.DecidedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt).HasPrecision(0).HasDefaultValueSql("(sysutcdatetime())");
            entity.HasOne(d => d.Project).WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.Cr).WithMany().HasForeignKey(d => d.Crid).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.Allocation).WithMany().HasForeignKey(d => d.AllocationId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.RequestedByNavigation).WithMany().HasForeignKey(d => d.RequestedBy).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<StoredFile>(entity =>
        {
            entity.HasKey(e => e.FileId);
            entity.Property(e => e.Purpose).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.StorageKey).HasMaxLength(300);
            entity.Property(e => e.OriginalName).HasMaxLength(200);
            entity.Property(e => e.ContentType).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.UploadedAt).HasPrecision(0);
            entity.HasOne(d => d.Project).WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<EmailOutboxMessage>(entity =>
        {
            entity.ToTable("EmailOutbox");
            entity.HasKey(e => e.EmailId);
            entity.Property(e => e.ToEmail).HasMaxLength(100);
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.Kind).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(10).IsUnicode(false);
            entity.Property(e => e.ProviderMessageId).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.LastError).HasMaxLength(300);
            entity.Property(e => e.NextAttemptAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.SentAt).HasPrecision(0);
            entity.Property(e => e.DeliveredAt).HasPrecision(0);
        });
    }
}
