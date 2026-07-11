using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceAndPrinterSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccount",
                table: "BusinessSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BarcodePrinter",
                table: "BusinessSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptPrinter",
                table: "BusinessSettings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatNumber",
                table: "BusinessSettings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAccount",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "BarcodePrinter",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "ReceiptPrinter",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "VatNumber",
                table: "BusinessSettings");
        }
    }
}
