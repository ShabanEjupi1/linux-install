using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Core.Migrations.Control
{
    /// <inheritdoc />
    public partial class AddShopDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShopDomain",
                table: "Businesses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_ShopDomain",
                table: "Businesses",
                column: "ShopDomain",
                unique: true,
                filter: "\"ShopDomain\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Businesses_ShopDomain",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "ShopDomain",
                table: "Businesses");
        }
    }
}
