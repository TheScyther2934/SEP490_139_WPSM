using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// "Saved": the email was only written to the pickup folder (no SMTP server configured), so nobody received it.
    /// Before this, such emails were marked "Sent".
    /// </summary>
    public partial class EmailSavedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "EmailOutbox" DROP CONSTRAINT "CK_EmailOutbox_Status";
                ALTER TABLE "EmailOutbox" ADD CONSTRAINT "CK_EmailOutbox_Status" CHECK ("Status" IN ('Queued', 'Sent', 'Saved', 'Failed'));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "EmailOutbox" SET "Status" = 'Sent' WHERE "Status" = 'Saved';
                ALTER TABLE "EmailOutbox" DROP CONSTRAINT "CK_EmailOutbox_Status";
                ALTER TABLE "EmailOutbox" ADD CONSTRAINT "CK_EmailOutbox_Status" CHECK ("Status" IN ('Queued', 'Sent', 'Failed'));
                """);
        }
    }
}
