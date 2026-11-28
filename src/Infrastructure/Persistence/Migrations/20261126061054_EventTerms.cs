using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventTerms",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    DepositRatio = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    MaxDecreaseRatio = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    BookingMinDays = table.Column<int>(type: "integer", nullable: false),
                    BookingMaxDays = table.Column<int>(type: "integer", nullable: false),
                    LockDays = table.Column<int>(type: "integer", nullable: false),
                    SupplierDeadlineDays = table.Column<int>(type: "integer", nullable: false),
                    SettlementDueDays = table.Column<int>(type: "integer", nullable: false),
                    PolicyEffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTerms", x => x.ProjectId);
                    table.CheckConstraint("CK_EventTerms_Days", "\"SupplierDeadlineDays\" >= 1 AND \"LockDays\" >= \"SupplierDeadlineDays\" AND \"SettlementDueDays\" >= 0");
                    table.CheckConstraint("CK_EventTerms_Ratios", "\"DepositRatio\" > 0 AND \"DepositRatio\" <= 100 AND \"MaxDecreaseRatio\" >= 0 AND \"MaxDecreaseRatio\" < 100");
                    table.ForeignKey(
                        name: "FK_EventTerms_WeddingProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "WeddingProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""ALTER TABLE "EventTerms" ENABLE ROW LEVEL SECURITY;""");

            // Existing events: the policy in force on the day each was signed (CreatedAt as a Vietnam date); defaults for missing keys.
            migrationBuilder.Sql("""
                INSERT INTO "EventTerms" ("ProjectId", "DepositRatio", "MaxDecreaseRatio", "BookingMinDays", "BookingMaxDays", "LockDays",
                    "SupplierDeadlineDays", "SettlementDueDays", "PolicyEffectiveFrom", "CreatedAt")
                SELECT s."ProjectId",
                    COALESCE((SELECT c."Value" FROM "PolicyConfigs" c WHERE c."PolicyType" = 'DEPOSIT_RATIO' AND c."EffectiveFrom" <= s."SignedOn" ORDER BY c."EffectiveFrom" DESC, c."PolicyId" DESC LIMIT 1), 30),
                    COALESCE((SELECT c."Value" FROM "PolicyConfigs" c WHERE c."PolicyType" = 'MAX_DECREASE_RATIO' AND c."EffectiveFrom" <= s."SignedOn" ORDER BY c."EffectiveFrom" DESC, c."PolicyId" DESC LIMIT 1), 30),
                    COALESCE((SELECT c."Value" FROM "PolicyConfigs" c WHERE c."PolicyType" = 'BOOKING_MIN_DAYS' AND c."EffectiveFrom" <= s."SignedOn" ORDER BY c."EffectiveFrom" DESC, c."PolicyId" DESC LIMIT 1), 15)::int,
                    COALESCE((SELECT c."Value" FROM "PolicyConfigs" c WHERE c."PolicyType" = 'BOOKING_MAX_DAYS' AND c."EffectiveFrom" <= s."SignedOn" ORDER BY c."EffectiveFrom" DESC, c."PolicyId" DESC LIMIT 1), 90)::int,
                    COALESCE((SELECT c."Value" FROM "PolicyConfigs" c WHERE c."PolicyType" = 'LOCK_DAYS' AND c."EffectiveFrom" <= s."SignedOn" ORDER BY c."EffectiveFrom" DESC, c."PolicyId" DESC LIMIT 1), 10)::int,
                    COALESCE((SELECT c."Value" FROM "PolicyConfigs" c WHERE c."PolicyType" = 'SUPPLIER_DEADLINE_DAYS' AND c."EffectiveFrom" <= s."SignedOn" ORDER BY c."EffectiveFrom" DESC, c."PolicyId" DESC LIMIT 1), 8)::int,
                    COALESCE((SELECT c."Value" FROM "PolicyConfigs" c WHERE c."PolicyType" = 'SETTLEMENT_DUE_DAYS' AND c."EffectiveFrom" <= s."SignedOn" ORDER BY c."EffectiveFrom" DESC, c."PolicyId" DESC LIMIT 1), 7)::int,
                    (SELECT MAX(c."EffectiveFrom") FROM "PolicyConfigs" c WHERE c."EffectiveFrom" <= s."SignedOn"
                        AND c."PolicyType" IN ('DEPOSIT_RATIO', 'MAX_DECREASE_RATIO', 'BOOKING_MIN_DAYS', 'BOOKING_MAX_DAYS', 'LOCK_DAYS', 'SUPPLIER_DEADLINE_DAYS', 'SETTLEMENT_DUE_DAYS')),
                    (now() at time zone 'utc')
                FROM (SELECT p."ProjectId", ((p."CreatedAt" AT TIME ZONE 'UTC') AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS "SignedOn"
                      FROM "WeddingProjects" p) s
                WHERE NOT EXISTS (SELECT 1 FROM "EventTerms" t WHERE t."ProjectId" = s."ProjectId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventTerms");
        }
    }
}
