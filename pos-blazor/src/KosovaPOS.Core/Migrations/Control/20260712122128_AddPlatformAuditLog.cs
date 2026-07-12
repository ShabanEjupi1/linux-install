using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KosovaPOS.Core.Migrations.Control
{
    /// <inheritdoc />
    public partial class AddPlatformAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BusinessId = table.Column<int>(type: "integer", nullable: true),
                    BusinessCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TargetUser = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Details = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuditLogs_Timestamp",
                table: "PlatformAuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAuditLogs");
        }
    }
}
