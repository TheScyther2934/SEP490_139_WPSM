using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilesInStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptContent",
                table: "Payments");

            migrationBuilder.AddColumn<Guid>(
                name: "FileId",
                table: "ExecutionPhotos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPhotos_FileId",
                table: "ExecutionPhotos",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Photo_File",
                table: "ExecutionPhotos",
                column: "FileId",
                principalTable: "StoredFiles",
                principalColumn: "FileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Photo_File",
                table: "ExecutionPhotos");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionPhotos_FileId",
                table: "ExecutionPhotos");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "ExecutionPhotos");

            migrationBuilder.AddColumn<byte[]>(
                name: "ReceiptContent",
                table: "Payments",
                type: "bytea",
                nullable: true);
        }
    }
}
