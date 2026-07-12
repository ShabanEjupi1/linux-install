using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptCodePageAndLabelSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LabelHeightMm",
                table: "BusinessSettings",
                type: "integer",
                nullable: false,
                // 25mm, not 0: EF stamps this default onto the rows that already exist, and a
                // shop that has been printing labels for a week would suddenly be laying them
                // out on stock 0mm tall.
                defaultValue: 25);

            migrationBuilder.AddColumn<int>(
                name: "LabelWidthMm",
                table: "BusinessSettings",
                type: "integer",
                nullable: false,
                defaultValue: 55);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptCodePage",
                table: "BusinessSettings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "1252");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelHeightMm",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "LabelWidthMm",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "ReceiptCodePage",
                table: "BusinessSettings");
        }
    }
}
