using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    AbsoluteExpiresAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp(0) without time zone", precision: 0, nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_UserSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_SessionId",
                table: "RefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_RevokedAt",
                table: "UserSessions",
                columns: new[] { "UserId", "RevokedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_UserSessions_SessionId",
                table: "RefreshTokens",
                column: "SessionId",
                principalTable: "UserSessions",
                principalColumn: "SessionId");

            // Same protection as every other table (no policies: only the API, as table owner, reads it).
            migrationBuilder.Sql("""ALTER TABLE "UserSessions" ENABLE ROW LEVEL SECURITY;""");
            migrationBuilder.Sql("""ALTER TABLE "UserSessions" ADD CONSTRAINT "CK_UserSessions_Expiry" CHECK ("LastSeenAt" >= "CreatedAt" AND "AbsoluteExpiresAt" > "CreatedAt");""");
            // Refresh tokens from before device sessions cannot be rotated any more: end them so everyone signs in once again.
            migrationBuilder.Sql("""UPDATE "RefreshTokens" SET "RevokedAt" = (now() at time zone 'utc') WHERE "RevokedAt" IS NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_UserSessions_SessionId",
                table: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_SessionId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "RefreshTokens");
        }
    }
}
