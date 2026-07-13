using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <summary>
    /// Which documents the till offers, and which of them start ticked.
    ///
    /// The defaults here are NOT the ones EF scaffolded. EF cannot see a C# property
    /// initializer, so it wrote <c>defaultValue: false</c> for all eight — which would have
    /// backfilled the live shop's existing settings row with every document switched OFF and
    /// left the cashier a till that prints nothing. A shop that has been selling since before
    /// this column existed must come out of the migration behaving exactly as it did going in:
    /// all four documents available, the fiscal receipt ticked.
    /// </summary>
    public partial class AddSellDocumentOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SellDefaultFiscal",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SellDefaultInvoiceA4",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SellDefaultReceipt80",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SellDefaultWaybillA4",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SellOfferFiscal",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SellOfferInvoiceA4",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SellOfferReceipt80",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SellOfferWaybillA4",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellDefaultFiscal",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "SellDefaultInvoiceA4",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "SellDefaultReceipt80",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "SellDefaultWaybillA4",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "SellOfferFiscal",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "SellOfferInvoiceA4",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "SellOfferReceipt80",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "SellOfferWaybillA4",
                table: "BusinessSettings");
        }
    }
}
