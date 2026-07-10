using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSellAndStockPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanManageStock",
                table: "POSUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanSell",
                table: "POSUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Both columns land false, which would lock existing users out of
            // screens they use daily. Backfill from the role presets on the
            // Përdoruesit screen — nobody gains access they did not already have.
            migrationBuilder.Sql("""
                UPDATE "POSUsers" SET "CanSell" = TRUE
                WHERE "Role" IN ('Admin', 'Manager', 'Cashier');
                """);
            migrationBuilder.Sql("""
                UPDATE "POSUsers" SET "CanManageStock" = TRUE
                WHERE "Role" IN ('Admin', 'Manager', 'Warehouse');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanManageStock",
                table: "POSUsers");

            migrationBuilder.DropColumn(
                name: "CanSell",
                table: "POSUsers");
        }
    }
}
