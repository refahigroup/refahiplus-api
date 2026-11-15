using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Pools.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AabsarPools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndsAtUtc",
                schema: "pools",
                table: "requests",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "TicketType",
                schema: "pools",
                table: "requests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aabsar_carts",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aabsar_carts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "aabsar_fulfillments",
                schema: "pools",
                columns: table => new
                {
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactCiphertext = table.Column<string>(type: "text", nullable: true),
                    CartSnapshot = table.Column<string>(type: "text", nullable: false),
                    ReceiptCiphertext = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssueFingerprint = table.Column<string>(type: "text", nullable: true),
                    CancelFingerprint = table.Column<string>(type: "text", nullable: true),
                    EvidenceFingerprint = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aabsar_fulfillments", x => x.PurchaseId);
                    table.ForeignKey(
                        name: "FK_aabsar_fulfillments_purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalSchema: "pools",
                        principalTable: "purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aabsar_products",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aabsar_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "aabsar_venues",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CityId = table.Column<int>(type: "integer", nullable: false),
                    ProvinceId = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aabsar_venues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "aabsar_cart_items",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    SellerKey = table.Column<string>(type: "text", nullable: false),
                    ProductKey = table.Column<string>(type: "text", nullable: false),
                    OfferKey = table.Column<string>(type: "text", nullable: false),
                    PurchaseOptionKey = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    SellerTitle = table.Column<string>(type: "text", nullable: false),
                    ProductTitle = table.Column<string>(type: "text", nullable: false),
                    ProductImageUrl = table.Column<string>(type: "text", nullable: true),
                    OptionTitle = table.Column<string>(type: "text", nullable: false),
                    OfferTitle = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ExpectedUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    OriginalUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aabsar_cart_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_aabsar_cart_items_aabsar_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "pools",
                        principalTable: "aabsar_carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "aabsar_showtimes",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShowtimeId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VendorName = table.Column<string>(type: "text", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    AdultPrice = table.Column<long>(type: "bigint", nullable: false),
                    ChildPrice = table.Column<long>(type: "bigint", nullable: false),
                    AdultOldPrice = table.Column<long>(type: "bigint", nullable: true),
                    ChildOldPrice = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aabsar_showtimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_aabsar_showtimes_aabsar_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pools",
                        principalTable: "aabsar_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aabsar_cart_items_CartId_OfferKey_PurchaseOptionKey",
                schema: "pools",
                table: "aabsar_cart_items",
                columns: new[] { "CartId", "OfferKey", "PurchaseOptionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_aabsar_carts_UserId",
                schema: "pools",
                table: "aabsar_carts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_aabsar_products_EventId",
                schema: "pools",
                table: "aabsar_products",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_aabsar_showtimes_ProductId",
                schema: "pools",
                table: "aabsar_showtimes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_aabsar_showtimes_ShowtimeId",
                schema: "pools",
                table: "aabsar_showtimes",
                column: "ShowtimeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aabsar_cart_items",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "aabsar_fulfillments",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "aabsar_showtimes",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "aabsar_venues",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "aabsar_carts",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "aabsar_products",
                schema: "pools");

            migrationBuilder.DropColumn(
                name: "TicketType",
                schema: "pools",
                table: "requests");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndsAtUtc",
                schema: "pools",
                table: "requests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
