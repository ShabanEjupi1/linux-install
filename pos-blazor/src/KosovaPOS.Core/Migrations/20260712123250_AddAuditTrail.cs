using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImpersonatedBy",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ImpersonatedBy",
                table: "AuditLogs");
        }
    }
}
