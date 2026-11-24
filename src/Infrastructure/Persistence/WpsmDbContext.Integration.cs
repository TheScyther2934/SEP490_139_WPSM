using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Infrastructure.Persistence;

// Kept apart from the scaffolded context so re-scaffolding does not drop the integration tables.
public partial class WpsmDbContext
{
    public virtual DbSet<StoredFile> StoredFiles { get; set; }

    public virtual DbSet<EmailOutboxMessage> EmailOutbox { get; set; }

    public virtual DbSet<ExceptionRequest> ExceptionRequests { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<ProjectManager> ProjectManagers { get; set; }

    public virtual DbSet<StageNote> StageNotes { get; set; }

    public virtual DbSet<AddonContract> AddonContracts { get; set; }

    public virtual DbSet<AddonContractItem> AddonContractItems { get; set; }

    public virtual DbSet<SupplierEvidence> SupplierEvidences { get; set; }

    public virtual DbSet<SettlementInvoice> SettlementInvoices { get; set; }

    public virtual DbSet<SettlementInvoiceLine> SettlementInvoiceLines { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectManager>(entity =>
        {
            entity.ToTable("ProjectManagers");
            entity.HasKey(e => e.ProjectManagerId);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.AssignedAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
            entity.Property(e => e.RemovedAt).HasPrecision(0);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.RemovalReason).HasMaxLength(500);
            entity.HasOne(d => d.Project).WithMany(p => p.ProjectManagers).HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.Manager).WithMany().HasForeignKey(d => d.ManagerId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.AssignedByNavigation).WithMany().HasForeignKey(d => d.AssignedBy).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.RemovedByNavigation).WithMany().HasForeignKey(d => d.RemovedBy).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs", table => table.HasTrigger("TR_AuditLogs_AppendOnly"));
            entity.HasKey(e => e.AuditId);
            entity.Property(e => e.OccurredAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
            entity.Property(e => e.AccountName).HasMaxLength(150);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.Action).HasMaxLength(20);
            entity.Property(e => e.Impact).HasMaxLength(30);
            entity.Property(e => e.EntityType).HasMaxLength(60);
            entity.Property(e => e.EntityId).HasMaxLength(60);
            entity.Property(e => e.ProjectCode).HasMaxLength(30);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
        });

        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.Property(e => e.EscalatedAt).HasPrecision(0);
            entity.Property(e => e.ResolvedAt).HasPrecision(0);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.AbsoluteExpiresAt).HasPrecision(0);
            entity.Property(e => e.RevokedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
            entity.Property(e => e.CreatedByIp).HasMaxLength(45);
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.ReplacedByToken).WithMany().HasForeignKey(d => d.ReplacedByTokenId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.Session).WithMany(p => p.RefreshTokens).HasForeignKey(d => d.SessionId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasIndex(e => e.SessionId);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("UserSessions");
            entity.HasKey(e => e.SessionId);
            entity.HasIndex(e => new { e.UserId, e.RevokedAt });
            entity.Property(e => e.DeviceName).HasMaxLength(100);
            entity.Property(e => e.UserAgent).HasMaxLength(300);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.RevokedReason).HasMaxLength(30);
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.LastSeenAt).HasPrecision(0);
            entity.Property(e => e.AbsoluteExpiresAt).HasPrecision(0);
            entity.Property(e => e.RevokedAt).HasPrecision(0);
            entity.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ExceptionRequest>(entity =>
        {
            entity.HasKey(e => e.ExceptionRequestId);
            entity.Property(e => e.RequestCode).HasMaxLength(30);
            entity.Property(e => e.SourceType).HasMaxLength(20);
            entity.Property(e => e.RequestNote).HasMaxLength(300);
            entity.Property(e => e.Status).HasMaxLength(10);
            entity.Property(e => e.DecisionNote).HasMaxLength(300);
            entity.Property(e => e.DecidedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
            entity.HasOne(d => d.Project).WithMany().HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.Allocation).WithMany().HasForeignKey(d => d.AllocationId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.RequestedByNavigation).WithMany().HasForeignKey(d => d.RequestedBy).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.DecisionByNavigation).WithMany().HasForeignKey(d => d.DecisionBy).OnDelete(DeleteBehavior.ClientSetNull);
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

        ConfigureEventFlow(modelBuilder);
    }

    private static void ConfigureEventFlow(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceCatalogItem>(entity => entity.Property(e => e.RequiresSupplier).HasDefaultValue(false));

        modelBuilder.Entity<WeddingProject>(entity =>
            entity.HasOne<User>().WithMany().HasForeignKey(d => d.CancelledBy).OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_Projects_CancelledBy"));

        modelBuilder.Entity<StageNote>(entity =>
        {
            entity.ToTable("StageNotes");
            entity.HasKey(e => e.StageNoteId);
            entity.Property(e => e.NoteType).HasMaxLength(20);
            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.ValueBefore).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ValueAfter).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
            entity.HasOne(d => d.Project).WithMany(p => p.StageNotes).HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.CreatedByNavigation).WithMany().HasForeignKey(d => d.CreatedBy).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<AddonContract>(entity =>
        {
            entity.ToTable("AddonContracts");
            entity.HasKey(e => e.AddonId);
            entity.HasIndex(e => e.AddonCode).IsUnique();
            entity.Property(e => e.AddonCode).HasMaxLength(30);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("PendingCustomer");
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
            entity.Property(e => e.ConfirmedAt).HasPrecision(0);
            entity.Property(e => e.CancelledAt).HasPrecision(0);
            entity.Property(e => e.RemindedAt).HasPrecision(0);
            entity.HasOne(d => d.Project).WithMany(p => p.AddonContracts).HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.CreatedByNavigation).WithMany().HasForeignKey(d => d.CreatedBy).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<AddonContractItem>(entity =>
        {
            entity.ToTable("AddonContractItems");
            entity.HasKey(e => e.AddonItemId);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.LineTotal).HasComputedColumnSql("\"Quantity\" * \"UnitPrice\"", true).HasColumnType("decimal(29, 2)");
            entity.HasOne(d => d.Addon).WithMany(p => p.Items).HasForeignKey(d => d.AddonId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(d => d.CatalogItem).WithMany().HasForeignKey(d => d.CatalogItemId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SupplierEvidence>(entity =>
        {
            entity.ToTable("SupplierEvidences");
            entity.HasKey(e => e.EvidenceId);
            entity.Property(e => e.SupplierName).HasMaxLength(150);
            entity.Property(e => e.EvidenceType).HasMaxLength(20);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.UploadedAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
            entity.HasOne(d => d.Project).WithMany(p => p.SupplierEvidences).HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.SpecItem).WithMany().HasForeignKey(d => d.SpecItemId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.File).WithMany().HasForeignKey(d => d.FileId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne(d => d.UploadedByNavigation).WithMany().HasForeignKey(d => d.UploadedBy).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SettlementInvoice>(entity =>
        {
            entity.ToTable("SettlementInvoices");
            entity.HasKey(e => e.InvoiceId);
            entity.HasIndex(e => e.InvoiceCode).IsUnique();
            entity.HasIndex(e => e.ProjectId).IsUnique();
            entity.Property(e => e.InvoiceCode).HasMaxLength(30);
            entity.Property(e => e.ContractTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AddonTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DepositPaid).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AmountDue).HasComputedColumnSql("\"ContractTotal\" + \"AddonTotal\" - \"DepositPaid\"", true).HasColumnType("decimal(20, 2)");
            entity.Property(e => e.Status).HasMaxLength(10).HasDefaultValue("Issued");
            entity.Property(e => e.IssuedAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
            entity.Property(e => e.PaidAt).HasPrecision(0);
            entity.HasOne(d => d.Project).WithOne(p => p.SettlementInvoice).HasForeignKey<SettlementInvoice>(d => d.ProjectId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<SettlementInvoiceLine>(entity =>
        {
            entity.ToTable("SettlementInvoiceLines");
            entity.HasKey(e => e.LineId);
            entity.Property(e => e.LineType).HasMaxLength(10);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.HasOne(d => d.Invoice).WithMany(p => p.Lines).HasForeignKey(d => d.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(e => e.SettingKey);
            entity.Property(e => e.SettingKey).HasMaxLength(50);
            entity.Property(e => e.SettingValue).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasPrecision(0).HasDefaultValueSql(UtcNowSql);
        });
    }
}
