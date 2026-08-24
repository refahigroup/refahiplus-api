using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommerceInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "commerce");

            migrationBuilder.CreateTable(
                name: "carts",
                schema: "commerce",
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
                    table.PrimaryKey("PK_carts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    TotalAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    RecipientNameProtected = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RecipientMobileProtected = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SellerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OfferKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PurchaseOptionKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OfferTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ExpectedUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cart_items_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "commerce",
                        principalTable: "carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommerceOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SellerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OfferKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PurchaseOptionKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OfferTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CategoryCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    ProviderPayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_CommerceOrderId",
                        column: x => x.CommerceOrderId,
                        principalSchema: "commerce",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_fulfillments",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommerceOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ProviderOrderCode = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_fulfillments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_fulfillments_orders_CommerceOrderId",
                        column: x => x.CommerceOrderId,
                        principalSchema: "commerce",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_operation_attempts",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderFulfillmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<short>(type: "smallint", nullable: false),
                    SanitizedError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_operation_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_operation_attempts_provider_fulfillments_ProviderF~",
                        column: x => x.ProviderFulfillmentId,
                        principalSchema: "commerce",
                        principalTable: "provider_fulfillments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_tickets",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderFulfillmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CodeProtected = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsChild = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_tickets_provider_fulfillments_ProviderFulfillmentId",
                        column: x => x.ProviderFulfillmentId,
                        principalSchema: "commerce",
                        principalTable: "provider_fulfillments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_CartId_ProviderKey_ProductKey_OfferKey_PurchaseO~",
                schema: "commerce",
                table: "cart_items",
                columns: new[] { "CartId", "ProviderKey", "ProductKey", "OfferKey", "PurchaseOptionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_carts_UserId",
                schema: "commerce",
                table: "carts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_CommerceOrderId",
                schema: "commerce",
                table: "order_items",
                column: "CommerceOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrderId",
                schema: "commerce",
                table: "orders",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserId_IdempotencyKey",
                schema: "commerce",
                table: "orders",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_fulfillments_CommerceOrderId_ProviderKey",
                schema: "commerce",
                table: "provider_fulfillments",
                columns: new[] { "CommerceOrderId", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_operation_attempts_ProviderFulfillmentId_Idempoten~",
                schema: "commerce",
                table: "provider_operation_attempts",
                columns: new[] { "ProviderFulfillmentId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_tickets_CodeHash",
                schema: "commerce",
                table: "provider_tickets",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_tickets_ProviderFulfillmentId",
                schema: "commerce",
                table: "provider_tickets",
                column: "ProviderFulfillmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_items",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "order_items",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "provider_operation_attempts",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "provider_tickets",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "carts",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "provider_fulfillments",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "commerce");
        }
    }
}
