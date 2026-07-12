using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <summary>
    /// Makes the courtesy receipt's lines switchable per shop.
    ///
    /// <b>The defaults here are hand-written, and must stay that way.</b> EF scaffolds
    /// <c>defaultValue: false</c> for a new bool column, because it defaults the CLR type and not
    /// the property initialiser — and a shop that is already selling has a BusinessSettings row
    /// ALREADY, so `false` is what that row would have got. The next receipt off the roll would
    /// have come out with no shop name, no address, no fiscal number, no cashier and no VAT
    /// breakdown, and nobody asked for any of that: they asked to be ABLE to turn those off.
    ///
    /// So every existing row is migrated to `true` — the receipt it was already printing — and
    /// the footer keeps the thank-you line it has always had. Turning any of it off is then a
    /// decision someone makes on /cilesimet, which is the whole point.
    /// </summary>
    public partial class AddReceiptLayoutOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptFooterText",
                table: "BusinessSettings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                defaultValue: "Faleminderit për blerjen!");

            migrationBuilder.AddColumn<string>(
                name: "ReceiptHeaderNote",
                table: "BusinessSettings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            foreach (var column in new[]
                     {
                         "ReceiptShowBusinessName",
                         "ReceiptShowAddress",
                         "ReceiptShowPhone",
                         "ReceiptShowFiscalNumber",
                         "ReceiptShowVatNumber",
                         "ReceiptShowCashier",
                         "ReceiptShowVatBreakdown",
                     })
            {
                migrationBuilder.AddColumn<bool>(
                    name: column,
                    table: "BusinessSettings",
                    type: "boolean",
                    nullable: false,
                    defaultValue: true);
            }

            // The one line the receipt did NOT carry before, so it stays off until it is asked for.
            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowPaidAndChange",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[]
                     {
                         "ReceiptFooterText",
                         "ReceiptHeaderNote",
                         "ReceiptShowBusinessName",
                         "ReceiptShowAddress",
                         "ReceiptShowPhone",
                         "ReceiptShowFiscalNumber",
                         "ReceiptShowVatNumber",
                         "ReceiptShowCashier",
                         "ReceiptShowVatBreakdown",
                         "ReceiptShowPaidAndChange",
                     })
            {
                migrationBuilder.DropColumn(name: column, table: "BusinessSettings");
            }
        }
    }
}
