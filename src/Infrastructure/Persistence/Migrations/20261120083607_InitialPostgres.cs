using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    AccountName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Impact = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    ProjectCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Changes = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "EmailOutbox",
                columns: table => new
                {
                    EmailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ToEmail = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", unicode: false, maxLength: 10, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    LastError = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RelatedProjectId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutbox", x => x.EmailId);
                });

            migrationBuilder.CreateTable(
                name: "Halls",
                columns: table => new
                {
                    HallId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MinCapacity = table.Column<int>(type: "integer", nullable: false),
                    MaxCapacity = table.Column<int>(type: "integer", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Halls", x => x.HallId);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    ResourceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.ResourceId);
                });

            migrationBuilder.CreateTable(
                name: "ServiceCatalogItems",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    RequiresSupplier = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCatalogItems", x => x.ItemId);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    SettingKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SettingValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.SettingKey);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Slots",
                columns: table => new
                {
                    SlotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HallId = table.Column<int>(type: "integer", nullable: false),
                    SlotName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0) without time zone", precision: 0, nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0) without time zone", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slots", x => x.SlotId);
                    table.UniqueConstraint("AK_Slots_SlotId_HallId", x => new { x.SlotId, x.HallId });
                    table.ForeignKey(
                        name: "FK_Slots_Halls",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "HallId");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipientId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Recipient",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "PolicyConfigs",
                columns: table => new
                {
                    PolicyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PolicyType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyConfigs", x => x.PolicyId);
                    table.ForeignKey(
                        name: "FK_Policy_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "PriceFrames",
                columns: table => new
                {
                    PriceFrameId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    MinPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    UpdatedBy = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceFrames", x => x.PriceFrameId);
                    table.ForeignKey(
                        name: "FK_PriceFrames_Item",
                        column: x => x.ItemId,
                        principalTable: "ServiceCatalogItems",
                        principalColumn: "ItemId");
                    table.ForeignKey(
                        name: "FK_PriceFrames_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    RefreshTokenId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    AbsoluteExpiresAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    ReplacedByTokenId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    CreatedByIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.RefreshTokenId);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokens_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalTable: "RefreshTokens",
                        principalColumn: "RefreshTokenId");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "WeddingProjects",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    ManagerId = table.Column<int>(type: "integer", nullable: false),
                    HallId = table.Column<int>(type: "integer", nullable: false),
                    SlotId = table.Column<int>(type: "integer", nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseContractValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrentTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Adjusting"),
                    LockedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    CancelledBy = table.Column<int>(type: "integer", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingProjects", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_Projects_CancelledBy",
                        column: x => x.CancelledBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_Projects_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_Projects_Customer",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_Projects_Hall",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "HallId");
                    table.ForeignKey(
                        name: "FK_Projects_Manager",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_Projects_Slot",
                        columns: x => new { x.SlotId, x.HallId },
                        principalTable: "Slots",
                        principalColumns: new[] { "SlotId", "HallId" });
                });

            migrationBuilder.CreateTable(
                name: "AddonContracts",
                columns: table => new
                {
                    AddonId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddonCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PendingCustomer"),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RemindedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddonContracts", x => x.AddonId);
                    table.ForeignKey(
                        name: "FK_AddonContracts_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_AddonContracts_WeddingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "BEOs",
                columns: table => new
                {
                    BEOId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    FrozenAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    ApprovedBy = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BEOs", x => x.BEOId);
                    table.UniqueConstraint("AK_BEOs_BEOId_ProjectId", x => new { x.BEOId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_BEO_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_BEO_Project",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "DayOfTasks",
                columns: table => new
                {
                    DayOfTaskId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    TaskName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeadlineTime = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    CompletedBy = table.Column<int>(type: "integer", nullable: true),
                    OverdueFlaggedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    PhotoRequired = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Department = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Service"),
                    SourceKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DayOfTasks", x => x.DayOfTaskId);
                    table.ForeignKey(
                        name: "FK_DayOf_CompletedBy",
                        column: x => x.CompletedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_DayOf_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_DayOf_Project",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    PaymentType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Upcoming"),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PaymentMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReceiptFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceiptContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    ReceiptFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ReceiptContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordedBy = table.Column<int>(type: "integer", nullable: true),
                    PaidDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.ForeignKey(
                        name: "FK_Payment_Project",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                    table.ForeignKey(
                        name: "FK_Payment_RecordedBy",
                        column: x => x.RecordedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ProjectManagers",
                columns: table => new
                {
                    ProjectManagerId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ManagerId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedBy = table.Column<int>(type: "integer", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RemovedBy = table.Column<int>(type: "integer", nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    RemovalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectManagers", x => x.ProjectManagerId);
                    table.ForeignKey(
                        name: "FK_ProjectManagers_Users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_ProjectManagers_Users_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_ProjectManagers_Users_RemovedBy",
                        column: x => x.RemovedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_ProjectManagers_WeddingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    RatingId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Module = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.RatingId);
                    table.ForeignKey(
                        name: "FK_Rating_Project",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "ResourceAllocations",
                columns: table => new
                {
                    AllocationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResourceId = table.Column<int>(type: "integer", nullable: false),
                    HallId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    AllocatedQuantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Reserved"),
                    IsExceptionApproved = table.Column<bool>(type: "boolean", nullable: false),
                    ExceptionApprovedBy = table.Column<int>(type: "integer", nullable: true),
                    ExceptionApprovedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    ExceptionNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceAllocations", x => x.AllocationId);
                    table.ForeignKey(
                        name: "FK_Alloc_ExceptionBy",
                        column: x => x.ExceptionApprovedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_Alloc_Hall",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "HallId");
                    table.ForeignKey(
                        name: "FK_Alloc_Project",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                    table.ForeignKey(
                        name: "FK_Alloc_Resource",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "ResourceId");
                });

            migrationBuilder.CreateTable(
                name: "SettlementInvoices",
                columns: table => new
                {
                    InvoiceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ContractTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AddonTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DepositPaid = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(20,2)", nullable: true, computedColumnSql: "\"ContractTotal\" + \"AddonTotal\" - \"DepositPaid\"", stored: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "Issued"),
                    IssuedBy = table.Column<int>(type: "integer", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    PaidAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementInvoices", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_SettlementInvoices_WeddingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "SpecItems",
                columns: table => new
                {
                    SpecItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    CatalogItemId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(29,2)", nullable: true, computedColumnSql: "\"Quantity\" * \"UnitPrice\"", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecItems", x => x.SpecItemId);
                    table.ForeignKey(
                        name: "FK_Spec_Catalog",
                        column: x => x.CatalogItemId,
                        principalTable: "ServiceCatalogItems",
                        principalColumn: "ItemId");
                    table.ForeignKey(
                        name: "FK_Spec_Project",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "StageNotes",
                columns: table => new
                {
                    StageNoteId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<byte>(type: "smallint", nullable: false),
                    NoteType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ValueBefore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ValueAfter = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreatedBy = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageNotes", x => x.StageNoteId);
                    table.ForeignKey(
                        name: "FK_StageNotes_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_StageNotes_WeddingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OriginalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBy = table.Column<int>(type: "integer", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.FileId);
                    table.ForeignKey(
                        name: "FK_StoredFiles_WeddingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "AddonContractItems",
                columns: table => new
                {
                    AddonItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddonId = table.Column<int>(type: "integer", nullable: false),
                    CatalogItemId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(29,2)", nullable: true, computedColumnSql: "\"Quantity\" * \"UnitPrice\"", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddonContractItems", x => x.AddonItemId);
                    table.ForeignKey(
                        name: "FK_AddonContractItems_AddonContracts_AddonId",
                        column: x => x.AddonId,
                        principalTable: "AddonContracts",
                        principalColumn: "AddonId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AddonContractItems_ServiceCatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "ServiceCatalogItems",
                        principalColumn: "ItemId");
                });

            migrationBuilder.CreateTable(
                name: "BEODepartmentSheets",
                columns: table => new
                {
                    SheetId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BEOId = table.Column<int>(type: "integer", nullable: false),
                    Department = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PdfFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BEODepartmentSheets", x => x.SheetId);
                    table.ForeignKey(
                        name: "FK_Sheet_BEO",
                        column: x => x.BEOId,
                        principalTable: "BEOs",
                        principalColumn: "BEOId");
                });

            migrationBuilder.CreateTable(
                name: "Complaints",
                columns: table => new
                {
                    ComplaintId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComplaintCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    BEOId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    EscalationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    EscalatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    ResolvedBy = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Complaints", x => x.ComplaintId);
                    table.ForeignKey(
                        name: "FK_Complaint_BEO",
                        columns: x => new { x.BEOId, x.ProjectId },
                        principalTable: "BEOs",
                        principalColumns: new[] { "BEOId", "ProjectId" });
                    table.ForeignKey(
                        name: "FK_Complaint_Project",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "ExecutionPhotos",
                columns: table => new
                {
                    PhotoId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayOfTaskId = table.Column<int>(type: "integer", nullable: false),
                    UploaderId = table.Column<int>(type: "integer", nullable: false),
                    ImageFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPhotos", x => x.PhotoId);
                    table.ForeignKey(
                        name: "FK_Photo_Task",
                        column: x => x.DayOfTaskId,
                        principalTable: "DayOfTasks",
                        principalColumn: "DayOfTaskId");
                    table.ForeignKey(
                        name: "FK_Photo_Uploader",
                        column: x => x.UploaderId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ExceptionRequests",
                columns: table => new
                {
                    ExceptionRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    AllocationId = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<int>(type: "integer", nullable: false),
                    RequestNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DecisionBy = table.Column<int>(type: "integer", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExceptionRequests", x => x.ExceptionRequestId);
                    table.ForeignKey(
                        name: "FK_ExceptionRequests_ResourceAllocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "ResourceAllocations",
                        principalColumn: "AllocationId");
                    table.ForeignKey(
                        name: "FK_ExceptionRequests_Users_DecisionBy",
                        column: x => x.DecisionBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_ExceptionRequests_Users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_ExceptionRequests_WeddingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateTable(
                name: "SettlementInvoiceLines",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    LineType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AddonId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementInvoiceLines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_SettlementInvoiceLines_SettlementInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "SettlementInvoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierEvidences",
                columns: table => new
                {
                    EvidenceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    SpecItemId = table.Column<int>(type: "integer", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EvidenceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedBy = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierEvidences", x => x.EvidenceId);
                    table.ForeignKey(
                        name: "FK_SupplierEvidences_SpecItems_SpecItemId",
                        column: x => x.SpecItemId,
                        principalTable: "SpecItems",
                        principalColumn: "SpecItemId");
                    table.ForeignKey(
                        name: "FK_SupplierEvidences_StoredFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "StoredFiles",
                        principalColumn: "FileId");
                    table.ForeignKey(
                        name: "FK_SupplierEvidences_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_SupplierEvidences_WeddingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddonContractItems_AddonId",
                table: "AddonContractItems",
                column: "AddonId");

            migrationBuilder.CreateIndex(
                name: "IX_AddonContractItems_CatalogItemId",
                table: "AddonContractItems",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AddonContracts_AddonCode",
                table: "AddonContracts",
                column: "AddonCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddonContracts_CreatedBy",
                table: "AddonContracts",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AddonContracts_ProjectId",
                table: "AddonContracts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "UQ_Sheet_BEO_Department",
                table: "BEODepartmentSheets",
                columns: new[] { "BEOId", "Department" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BEOs_ApprovedBy",
                table: "BEOs",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_BEO_Id_Project",
                table: "BEOs",
                columns: new[] { "BEOId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_BEO_Project_Version",
                table: "BEOs",
                columns: new[] { "ProjectId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaint_Project",
                table: "Complaints",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_BEOId_ProjectId",
                table: "Complaints",
                columns: new[] { "BEOId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Complaint_Code",
                table: "Complaints",
                column: "ComplaintCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DayOf_Project_Deadline",
                table: "DayOfTasks",
                columns: new[] { "ProjectId", "DeadlineTime", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DayOfTasks_CompletedBy",
                table: "DayOfTasks",
                column: "CompletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DayOfTasks_CreatedBy",
                table: "DayOfTasks",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DayOfTasks_Status_Deadline",
                table: "DayOfTasks",
                columns: new[] { "Status", "DeadlineTime" });

            migrationBuilder.CreateIndex(
                name: "UQ_DayOf_Project_Source",
                table: "DayOfTasks",
                columns: new[] { "ProjectId", "SourceKey" },
                unique: true,
                filter: "\"SourceKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionRequests_AllocationId",
                table: "ExceptionRequests",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionRequests_DecisionBy",
                table: "ExceptionRequests",
                column: "DecisionBy");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionRequests_ProjectId",
                table: "ExceptionRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionRequests_RequestedBy",
                table: "ExceptionRequests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPhotos_UploaderId",
                table: "ExecutionPhotos",
                column: "UploaderId");

            migrationBuilder.CreateIndex(
                name: "IX_Photo_Task",
                table: "ExecutionPhotos",
                column: "DayOfTaskId");

            migrationBuilder.CreateIndex(
                name: "UQ_Halls_Name",
                table: "Halls",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Recipient",
                table: "Notifications",
                columns: new[] { "RecipientId", "IsRead", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RecordedBy",
                table: "Payments",
                column: "RecordedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Payment_Project_Type",
                table: "Payments",
                columns: new[] { "ProjectId", "PaymentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyConfigs_UpdatedBy",
                table: "PolicyConfigs",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Policy_Type_Effective",
                table: "PolicyConfigs",
                columns: new[] { "PolicyType", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceFrames_UpdatedBy",
                table: "PriceFrames",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_PriceFrames_Item_Effective",
                table: "PriceFrames",
                columns: new[] { "ItemId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagers_AssignedBy",
                table: "ProjectManagers",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagers_ManagerId",
                table: "ProjectManagers",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagers_ProjectId",
                table: "ProjectManagers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectManagers_RemovedBy",
                table: "ProjectManagers",
                column: "RemovedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Rating_Project_Module",
                table: "Ratings",
                columns: new[] { "ProjectId", "Module" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ReplacedByTokenId",
                table: "RefreshTokens",
                column: "ReplacedByTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Alloc_Project",
                table: "ResourceAllocations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Alloc_Resource_Time",
                table: "ResourceAllocations",
                columns: new[] { "ResourceId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_ExceptionApprovedBy",
                table: "ResourceAllocations",
                column: "ExceptionApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceAllocations_HallId",
                table: "ResourceAllocations",
                column: "HallId");

            migrationBuilder.CreateIndex(
                name: "UQ_Resources_Type_Name",
                table: "Resources",
                columns: new[] { "Type", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Catalog_Category_Name",
                table: "ServiceCatalogItems",
                columns: new[] { "Category", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementInvoiceLines_InvoiceId",
                table: "SettlementInvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementInvoices_InvoiceCode",
                table: "SettlementInvoices",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementInvoices_ProjectId",
                table: "SettlementInvoices",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Slots_Hall_SlotName",
                table: "Slots",
                columns: new[] { "HallId", "SlotName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Slots_Slot_Hall",
                table: "Slots",
                columns: new[] { "SlotId", "HallId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spec_Project",
                table: "SpecItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecItems_CatalogItemId",
                table: "SpecItems",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StageNotes_CreatedBy",
                table: "StageNotes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_StageNotes_ProjectId",
                table: "StageNotes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_ProjectId",
                table: "StoredFiles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvidences_FileId",
                table: "SupplierEvidences",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvidences_ProjectId",
                table: "SupplierEvidences",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvidences_SpecItemId",
                table: "SupplierEvidences",
                column: "SpecItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvidences_UploadedBy",
                table: "SupplierEvidences",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Customer",
                table: "WeddingProjects",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Manager",
                table: "WeddingProjects",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_Date",
                table: "WeddingProjects",
                columns: new[] { "Status", "EventDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingProjects_CancelledBy",
                table: "WeddingProjects",
                column: "CancelledBy");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingProjects_CreatedBy",
                table: "WeddingProjects",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_WeddingProjects_Manager_Status",
                table: "WeddingProjects",
                columns: new[] { "ManagerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingProjects_SlotId_HallId",
                table: "WeddingProjects",
                columns: new[] { "SlotId", "HallId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Projects_Code",
                table: "WeddingProjects",
                column: "ProjectCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Projects_HallSlotDate_Active",
                table: "WeddingProjects",
                columns: new[] { "HallId", "SlotId", "EventDate" },
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");

            migrationBuilder.Sql(InitialPostgresExtras.Up);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(InitialPostgresExtras.Down);

            migrationBuilder.DropTable(
                name: "AddonContractItems");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BEODepartmentSheets");

            migrationBuilder.DropTable(
                name: "Complaints");

            migrationBuilder.DropTable(
                name: "EmailOutbox");

            migrationBuilder.DropTable(
                name: "ExceptionRequests");

            migrationBuilder.DropTable(
                name: "ExecutionPhotos");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PolicyConfigs");

            migrationBuilder.DropTable(
                name: "PriceFrames");

            migrationBuilder.DropTable(
                name: "ProjectManagers");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "SettlementInvoiceLines");

            migrationBuilder.DropTable(
                name: "StageNotes");

            migrationBuilder.DropTable(
                name: "SupplierEvidences");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "AddonContracts");

            migrationBuilder.DropTable(
                name: "BEOs");

            migrationBuilder.DropTable(
                name: "ResourceAllocations");

            migrationBuilder.DropTable(
                name: "DayOfTasks");

            migrationBuilder.DropTable(
                name: "SettlementInvoices");

            migrationBuilder.DropTable(
                name: "SpecItems");

            migrationBuilder.DropTable(
                name: "StoredFiles");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "ServiceCatalogItems");

            migrationBuilder.DropTable(
                name: "WeddingProjects");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Slots");

            migrationBuilder.DropTable(
                name: "Halls");
        }
    }
}
