using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddBmdLedgerTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kartela_Subjektit",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SUBJEKTI_ID = table.Column<int>(type: "integer", nullable: true),
                    Metoda_ID = table.Column<int>(type: "integer", nullable: true),
                    Dokumenti_NR = table.Column<int>(type: "integer", nullable: true),
                    Dokumenti_ID = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TIPI = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Pershkrimi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: true),
                    PerPagese = table.Column<double>(type: "double precision", nullable: true),
                    Pagoi = table.Column<double>(type: "double precision", nullable: true),
                    Mbeti = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kartela_Subjektit", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "tbl_Stoku",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArtikulliID = table.Column<int>(type: "integer", nullable: true),
                    Nr_Rendor = table.Column<int>(type: "integer", nullable: true),
                    Dokumenti_NR = table.Column<int>(type: "integer", nullable: true),
                    Dokumenti_ID = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TIPI = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TIPI_ID = table.Column<int>(type: "integer", nullable: true),
                    Data = table.Column<DateOnly>(type: "date", nullable: true),
                    SasiaBlerje = table.Column<double>(type: "double precision", nullable: true),
                    KostojaBlerje = table.Column<double>(type: "double precision", nullable: true),
                    KostojaShitje = table.Column<double>(type: "double precision", nullable: true),
                    SasiaKorigjimi = table.Column<double>(type: "double precision", nullable: true),
                    SasiaProdhimi = table.Column<double>(type: "double precision", nullable: true),
                    SasiaShitje = table.Column<double>(type: "double precision", nullable: true),
                    LineID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_Stoku", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kartela_Subjektit_Data",
                table: "Kartela_Subjektit",
                column: "Data");

            migrationBuilder.CreateIndex(
                name: "IX_Kartela_Subjektit_SUBJEKTI_ID",
                table: "Kartela_Subjektit",
                column: "SUBJEKTI_ID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Stoku_ArtikulliID",
                table: "tbl_Stoku",
                column: "ArtikulliID");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Stoku_Data",
                table: "tbl_Stoku",
                column: "Data");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Kartela_Subjektit");

            migrationBuilder.DropTable(
                name: "tbl_Stoku");
        }
    }
}
