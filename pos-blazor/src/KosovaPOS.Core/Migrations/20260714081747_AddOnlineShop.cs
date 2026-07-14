using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KosovaPOS.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShopAcceptCashOnDelivery",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShopAcceptPayPal",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShopEnabled",
                table: "BusinessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ShopFreeShippingOver",
                table: "BusinessSettings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShopOrderEmail",
                table: "BusinessSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShopShippingFee",
                table: "BusinessSettings",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShopTagline",
                table: "BusinessSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArticlePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ArticleId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticlePhotos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ShippingFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    PaymentStatus = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayPalOrderId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PayPalCaptureId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReceiptId = table.Column<int>(type: "integer", nullable: true),
                    StockTaken = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebOrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WebOrderId = table.Column<int>(type: "integer", nullable: false),
                    ArticleId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebOrderItems_WebOrders_WebOrderId",
                        column: x => x.WebOrderId,
                        principalTable: "WebOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticlePhotos_ArticleId_SortOrder",
                table: "ArticlePhotos",
                columns: new[] { "ArticleId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WebOrderItems_WebOrderId",
                table: "WebOrderItems",
                column: "WebOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WebOrders_CreatedAt",
                table: "WebOrders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebOrders_OrderNumber",
                table: "WebOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebOrders_PayPalOrderId",
                table: "WebOrders",
                column: "PayPalOrderId",
                unique: true,
                filter: "\"PayPalOrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticlePhotos");

            migrationBuilder.DropTable(
                name: "WebOrderItems");

            migrationBuilder.DropTable(
                name: "WebOrders");

            migrationBuilder.DropColumn(
                name: "ShopAcceptCashOnDelivery",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "ShopAcceptPayPal",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "ShopEnabled",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "ShopFreeShippingOver",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "ShopOrderEmail",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "ShopShippingFee",
                table: "BusinessSettings");

            migrationBuilder.DropColumn(
                name: "ShopTagline",
                table: "BusinessSettings");
        }
    }
}
