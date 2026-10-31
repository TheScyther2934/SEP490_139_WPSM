using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Persistence;

public partial class WpsmDbContext : DbContext
{
    public WpsmDbContext(DbContextOptions<WpsmDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Beo> Beos { get; set; }

    public virtual DbSet<BeodepartmentSheet> BeodepartmentSheets { get; set; }

    public virtual DbSet<ChangeRequest> ChangeRequests { get; set; }

    public virtual DbSet<Complaint> Complaints { get; set; }

    public virtual DbSet<ContractAddendum> ContractAddenda { get; set; }

    public virtual DbSet<Crhistory> Crhistories { get; set; }

    public virtual DbSet<Critem> Critems { get; set; }

    public virtual DbSet<DayOfTask> DayOfTasks { get; set; }

    public virtual DbSet<ExecutionPhoto> ExecutionPhotos { get; set; }

    public virtual DbSet<Hall> Halls { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PolicyConfig> PolicyConfigs { get; set; }

    public virtual DbSet<PriceFrame> PriceFrames { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<Resource> Resources { get; set; }

    public virtual DbSet<ResourceAllocation> ResourceAllocations { get; set; }

    public virtual DbSet<RoadmapTask> RoadmapTasks { get; set; }

    public virtual DbSet<ServiceCatalogItem> ServiceCatalogItems { get; set; }

    public virtual DbSet<Slot> Slots { get; set; }

    public virtual DbSet<SpecItem> SpecItems { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WeddingProject> WeddingProjects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Beo>(entity =>
        {
            entity.ToTable("BEOs");

            entity.HasIndex(e => new { e.Beoid, e.ProjectId }, "UQ_BEO_Id_Project").IsUnique();

            entity.HasIndex(e => new { e.ProjectId, e.Version }, "UQ_BEO_Project_Version").IsUnique();

            entity.Property(e => e.Beoid).HasColumnName("BEOId");
            entity.Property(e => e.FrozenAt).HasPrecision(0);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Draft");
            entity.Property(e => e.Version).HasDefaultValue(1);

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.Beos)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_BEO_ApprovedBy");

            entity.HasOne(d => d.Project).WithMany(p => p.Beos)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BEO_Project");
        });

        modelBuilder.Entity<BeodepartmentSheet>(entity =>
        {
            entity.HasKey(e => e.SheetId);

            entity.ToTable("BEODepartmentSheets");

            entity.HasIndex(e => new { e.Beoid, e.Department }, "UQ_Sheet_BEO_Department").IsUnique();

            entity.Property(e => e.Beoid).HasColumnName("BEOId");
            entity.Property(e => e.Department).HasMaxLength(20);
            entity.Property(e => e.GeneratedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.PdfFilePath).HasMaxLength(500);

            entity.HasOne(d => d.Beo).WithMany(p => p.BeodepartmentSheets)
                .HasForeignKey(d => d.Beoid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sheet_BEO");
        });

        modelBuilder.Entity<ChangeRequest>(entity =>
        {
            entity.HasKey(e => e.Crid);

            entity.HasIndex(e => new { e.ProjectId, e.Status }, "IX_CR_Project_Status");

            entity.HasIndex(e => e.Crcode, "UQ_CR_Code").IsUnique();

            entity.Property(e => e.Crid).HasColumnName("CRId");
            entity.Property(e => e.CostImpact).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Crcode)
                .HasMaxLength(30)
                .HasColumnName("CRCode");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerApprovedAt).HasPrecision(0);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ManagerApprovedAt).HasPrecision(0);
            entity.Property(e => e.PenaltyFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PenaltyRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Draft");
            entity.Property(e => e.TotalVariance)
                .HasComputedColumnSql("([CostImpact]+[PenaltyFee])", true)
                .HasColumnType("decimal(19, 2)");
            entity.Property(e => e.Type).HasMaxLength(30);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ChangeRequests)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CR_CreatedBy");

            entity.HasOne(d => d.Project).WithMany(p => p.ChangeRequests)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CR_Project");
        });

        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.Status }, "IX_Complaint_Project");

            entity.HasIndex(e => e.ComplaintCode, "UQ_Complaint_Code").IsUnique();

            entity.Property(e => e.Beoid).HasColumnName("BEOId");
            entity.Property(e => e.ComplaintCode).HasMaxLength(30);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EscalationNote).HasMaxLength(500);
            entity.Property(e => e.ResolutionNote).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Open");

            entity.HasOne(d => d.Project).WithMany(p => p.Complaints)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Complaint_Project");

            entity.HasOne(d => d.Beo).WithMany(p => p.Complaints)
                .HasPrincipalKey(p => new { p.Beoid, p.ProjectId })
                .HasForeignKey(d => new { d.Beoid, d.ProjectId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Complaint_BEO");
        });

        modelBuilder.Entity<ContractAddendum>(entity =>
        {
            entity.HasKey(e => e.AddendumId);

            entity.HasIndex(e => e.Crid, "UQ_Addenda_CR").IsUnique();

            entity.HasIndex(e => e.AddendumCode, "UQ_Addenda_Code").IsUnique();

            entity.Property(e => e.AddendumCode).HasMaxLength(30);
            entity.Property(e => e.Crid).HasColumnName("CRId");
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.SignedAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Generated");
            entity.Property(e => e.TotalVariance).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cr).WithOne(p => p.ContractAddendum)
                .HasForeignKey<ContractAddendum>(d => d.Crid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Addenda_CR");

            entity.HasOne(d => d.Project).WithMany(p => p.ContractAddenda)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Addenda_Project");
        });

        modelBuilder.Entity<Crhistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId);

            entity.ToTable("CRHistories");

            entity.HasIndex(e => e.Crid, "IX_CRHistory_CR");

            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Crid).HasColumnName("CRId");
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.Actor).WithMany(p => p.Crhistories)
                .HasForeignKey(d => d.ActorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CRHistory_Actor");

            entity.HasOne(d => d.Cr).WithMany(p => p.Crhistories)
                .HasForeignKey(d => d.Crid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CRHistory_CR");
        });

        modelBuilder.Entity<Critem>(entity =>
        {
            entity.ToTable("CRItems");

            entity.HasIndex(e => new { e.Crid, e.TargetSpecItemId }, "UQ_CRItems_CR_SpecItem").IsUnique();

            entity.Property(e => e.CritemId).HasColumnName("CRItemId");
            entity.Property(e => e.Crid).HasColumnName("CRId");
            entity.Property(e => e.DeltaAmount)
                .HasComputedColumnSql("([NewUnitPrice]*[NewQuantity]-[OldUnitPrice]*[OldQuantity])", true)
                .HasColumnType("decimal(30, 2)");
            entity.Property(e => e.NewUnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldUnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cr).WithMany(p => p.Critems)
                .HasForeignKey(d => d.Crid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CRItems_CR");

            entity.HasOne(d => d.TargetSpecItem).WithMany(p => p.Critems)
                .HasForeignKey(d => d.TargetSpecItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CRItems_SpecItem");
        });

        modelBuilder.Entity<DayOfTask>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.DeadlineTime, e.Status }, "IX_DayOf_Project_Deadline");

            entity.Property(e => e.CompletedAt).HasPrecision(0);
            entity.Property(e => e.DeadlineTime).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TaskName).HasMaxLength(200);

            entity.HasOne(d => d.Project).WithMany(p => p.DayOfTasks)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DayOf_Project");
        });

        modelBuilder.Entity<ExecutionPhoto>(entity =>
        {
            entity.HasKey(e => e.PhotoId);

            entity.HasIndex(e => e.DayOfTaskId, "IX_Photo_Task");

            entity.Property(e => e.ImageFilePath).HasMaxLength(500);
            entity.Property(e => e.UploadedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.DayOfTask).WithMany(p => p.ExecutionPhotos)
                .HasForeignKey(d => d.DayOfTaskId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Photo_Task");

            entity.HasOne(d => d.Uploader).WithMany(p => p.ExecutionPhotos)
                .HasForeignKey(d => d.UploaderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Photo_Uploader");
        });

        modelBuilder.Entity<Hall>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_Halls_Name").IsUnique();

            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => new { e.RecipientId, e.IsRead, e.CreatedAt }, "IX_Notifications_Recipient").IsDescending(false, false, true);

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LinkUrl).HasMaxLength(255);
            entity.Property(e => e.Message).HasMaxLength(300);
            entity.Property(e => e.Type).HasMaxLength(30);

            entity.HasOne(d => d.Recipient).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RecipientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Notifications_Recipient");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.PaymentType }, "UQ_Payment_Project_Type").IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentMethod).HasMaxLength(10);
            entity.Property(e => e.PaymentType).HasMaxLength(10);
            entity.Property(e => e.ReceiptFilePath).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(10)
                .HasDefaultValue("Upcoming");

            entity.HasOne(d => d.Project).WithMany(p => p.Payments)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payment_Project");

            entity.HasOne(d => d.RecordedByNavigation).WithMany(p => p.Payments)
                .HasForeignKey(d => d.RecordedBy)
                .HasConstraintName("FK_Payment_RecordedBy");
        });

        modelBuilder.Entity<PolicyConfig>(entity =>
        {
            entity.HasKey(e => e.PolicyId);

            entity.HasIndex(e => new { e.PolicyType, e.TdayMin, e.TdayMax, e.EffectiveFrom }, "UQ_Policy_Type_Range_Effective").IsUnique();

            entity.Property(e => e.PolicyType).HasMaxLength(20);
            entity.Property(e => e.RatePercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.TdayMax).HasColumnName("TDayMax");
            entity.Property(e => e.TdayMin).HasColumnName("TDayMin");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PolicyConfigs)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Policy_UpdatedBy");
        });

        modelBuilder.Entity<PriceFrame>(entity =>
        {
            entity.HasIndex(e => new { e.ItemId, e.EffectiveFrom }, "UQ_PriceFrames_Item_Effective").IsUnique();

            entity.Property(e => e.MaxPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Item).WithMany(p => p.PriceFrames)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PriceFrames_Item");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PriceFrames)
                .HasForeignKey(d => d.UpdatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PriceFrames_UpdatedBy");
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.Module }, "UQ_Rating_Project_Module").IsUnique();

            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Module).HasMaxLength(10);

            entity.HasOne(d => d.Project).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Rating_Project");
        });

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.HasIndex(e => new { e.Type, e.Name }, "UQ_Resources_Type_Name").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Type).HasMaxLength(20);
        });

        modelBuilder.Entity<ResourceAllocation>(entity =>
        {
            entity.HasKey(e => e.AllocationId);

            entity.HasIndex(e => e.ProjectId, "IX_Alloc_Project");

            entity.HasIndex(e => new { e.ResourceId, e.StartTime, e.EndTime }, "IX_Alloc_Resource_Time");

            entity.Property(e => e.EndTime).HasPrecision(0);
            entity.Property(e => e.ExceptionApprovedAt).HasPrecision(0);
            entity.Property(e => e.ExceptionNote).HasMaxLength(300);
            entity.Property(e => e.StartTime).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Reserved");

            entity.HasOne(d => d.ExceptionApprovedByNavigation).WithMany(p => p.ResourceAllocations)
                .HasForeignKey(d => d.ExceptionApprovedBy)
                .HasConstraintName("FK_Alloc_ExceptionBy");

            entity.HasOne(d => d.Hall).WithMany(p => p.ResourceAllocations)
                .HasForeignKey(d => d.HallId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Alloc_Hall");

            entity.HasOne(d => d.Project).WithMany(p => p.ResourceAllocations)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Alloc_Project");

            entity.HasOne(d => d.Resource).WithMany(p => p.ResourceAllocations)
                .HasForeignKey(d => d.ResourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Alloc_Resource");
        });

        modelBuilder.Entity<RoadmapTask>(entity =>
        {
            entity.HasKey(e => e.TaskId);

            entity.HasIndex(e => new { e.ProjectId, e.Milestone }, "IX_Roadmap_Project_Milestone");

            entity.Property(e => e.AssigneeRole).HasMaxLength(20);
            entity.Property(e => e.CompletedAt).HasPrecision(0);
            entity.Property(e => e.Milestone).HasMaxLength(20);
            entity.Property(e => e.RemindedAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Project).WithMany(p => p.RoadmapTasks)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Roadmap_Project");
        });

        modelBuilder.Entity<ServiceCatalogItem>(entity =>
        {
            entity.HasKey(e => e.ItemId);

            entity.HasIndex(e => new { e.Category, e.Name }, "UQ_Catalog_Category_Name").IsUnique();

            entity.Property(e => e.Category).HasMaxLength(30);
            entity.Property(e => e.DefaultPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");
            entity.Property(e => e.Unit).HasMaxLength(20);
        });

        modelBuilder.Entity<Slot>(entity =>
        {
            entity.HasIndex(e => new { e.HallId, e.SlotName }, "UQ_Slots_Hall_SlotName").IsUnique();

            entity.HasIndex(e => new { e.SlotId, e.HallId }, "UQ_Slots_Slot_Hall").IsUnique();

            entity.Property(e => e.EndTime).HasPrecision(0);
            entity.Property(e => e.SlotName).HasMaxLength(20);
            entity.Property(e => e.StartTime).HasPrecision(0);

            entity.HasOne(d => d.Hall).WithMany(p => p.Slots)
                .HasForeignKey(d => d.HallId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Slots_Halls");
        });

        modelBuilder.Entity<SpecItem>(entity =>
        {
            entity.HasIndex(e => new { e.ProjectId, e.CatalogItemId, e.Version }, "UQ_Spec_Project_Item_Version").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsBaseline).HasDefaultValue(true);
            entity.Property(e => e.LineTotal)
                .HasComputedColumnSql("([Quantity]*[UnitPrice])", true)
                .HasColumnType("decimal(29, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Version).HasDefaultValue(1);

            entity.HasOne(d => d.CatalogItem).WithMany(p => p.SpecItems)
                .HasForeignKey(d => d.CatalogItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Spec_Catalog");

            entity.HasOne(d => d.Project).WithMany(p => p.SpecItems)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Spec_Project");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.MustChangePassword).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(11);
            entity.Property(e => e.Role).HasMaxLength(20);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");
        });

        modelBuilder.Entity<WeddingProject>(entity =>
        {
            entity.HasKey(e => e.ProjectId);

            entity.HasIndex(e => e.CustomerId, "IX_Projects_Customer");

            entity.HasIndex(e => e.ManagerId, "IX_Projects_Manager");

            entity.HasIndex(e => new { e.Status, e.EventDate }, "IX_Projects_Status_Date");

            entity.HasIndex(e => e.ProjectCode, "UQ_Projects_Code").IsUnique();

            entity.HasIndex(e => new { e.HallId, e.SlotId, e.EventDate }, "UQ_Projects_Hall_Slot_Date").IsUnique();

            entity.Property(e => e.BaseContractValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BaselineFrozenAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CurrentTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Nrdamount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("NRDAmount");
            entity.Property(e => e.ProjectCode).HasMaxLength(30);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active Baseline");

            entity.HasOne(d => d.Customer).WithMany(p => p.WeddingProjectCustomers)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Projects_Customer");

            entity.HasOne(d => d.Hall).WithMany(p => p.WeddingProjects)
                .HasForeignKey(d => d.HallId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Projects_Hall");

            entity.HasOne(d => d.Manager).WithMany(p => p.WeddingProjectManagers)
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Projects_Manager");

            entity.HasOne(d => d.Slot).WithMany(p => p.WeddingProjects)
                .HasPrincipalKey(p => new { p.SlotId, p.HallId })
                .HasForeignKey(d => new { d.SlotId, d.HallId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Projects_Slot");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
