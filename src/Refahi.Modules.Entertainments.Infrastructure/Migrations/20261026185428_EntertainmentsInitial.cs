using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Entertainments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntertainmentsInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "entertainments");

            migrationBuilder.CreateTable(
                name: "carts",
                schema: "entertainments",
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
                name: "checkout_sessions",
                schema: "entertainments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    RequestProtected = table.Column<string>(type: "text", nullable: false),
                    ReservationProtected = table.Column<string>(type: "text", nullable: true),
                    ReservationReference = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    EntertainmentPurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkout_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                schema: "entertainments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckoutSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_purchases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_location_mappings",
                schema: "entertainments",
                columns: table => new
                {
                    AccountKey = table.Column<string>(type: "text", nullable: false),
                    LocationCode = table.Column<string>(type: "text", nullable: false),
                    CityId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_location_mappings", x => new { x.AccountKey, x.LocationCode });
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_programs",
                schema: "entertainments",
                columns: table => new
                {
                    ProgramKey = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountKey = table.Column<string>(type: "text", nullable: false),
                    NativeId = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    SupplierTitle = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Rules = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    LocationCode = table.Column<string>(type: "text", nullable: true),
                    NativeCityName = table.Column<string>(type: "text", nullable: true),
                    CategoryCode = table.Column<int>(type: "integer", nullable: true),
                    CategoryTitle = table.Column<string>(type: "text", nullable: true),
                    ProgramType = table.Column<int>(type: "integer", nullable: false),
                    RequiresManifest = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresIdentityNumber = table.Column<bool>(type: "boolean", nullable: false),
                    Images = table.Column<string[]>(type: "text[]", nullable: false),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    IsListed = table.Column<bool>(type: "boolean", nullable: false),
                    ListedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextDetailAttemptUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_programs", x => x.ProgramKey);
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_receipts",
                schema: "entertainments",
                columns: table => new
                {
                    OperationId = table.Column<string>(type: "text", nullable: false),
                    AccountKey = table.Column<string>(type: "text", nullable: false),
                    PayloadProtected = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_receipts", x => new { x.AccountKey, x.OperationId });
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_sync_states",
                schema: "entertainments",
                columns: table => new
                {
                    AccountKey = table.Column<string>(type: "text", nullable: false),
                    LastSuccessUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_sync_states", x => x.AccountKey);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                schema: "entertainments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SupplierKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProgramKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OfferKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PurchaseOptionKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SupplierTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ProgramTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProgramImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OptionTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OfferTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ExpectedUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    OriginalUnitPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cart_items_carts_CartId",
                        column: x => x.CartId,
                        principalSchema: "entertainments",
                        principalTable: "carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_reservations",
                schema: "entertainments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationReference = table.Column<string>(type: "text", nullable: true),
                    ReservationContextProtected = table.Column<string>(type: "text", nullable: true),
                    CartSelectionProtected = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_touristpanel_reservations_purchases_Id",
                        column: x => x.Id,
                        principalSchema: "entertainments",
                        principalTable: "purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_items",
                schema: "entertainments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntertainmentPurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SupplierKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProgramKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OfferKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PurchaseOptionKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OfferTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CategoryCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_items_touristpanel_reservations_EntertainmentPurch~",
                        column: x => x.EntertainmentPurchaseId,
                        principalSchema: "entertainments",
                        principalTable: "touristpanel_reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_issuances",
                schema: "entertainments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntertainmentPurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ProviderOrderCode = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProviderInvoiceId = table.Column<string>(type: "text", nullable: true),
                    ProviderPaymentId = table.Column<string>(type: "text", nullable: true),
                    DeliveryProtected = table.Column<string>(type: "text", nullable: true),
                    ReconcileStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextCheckAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_issuances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_touristpanel_issuances_touristpanel_reservations_Entertainm~",
                        column: x => x.EntertainmentPurchaseId,
                        principalSchema: "entertainments",
                        principalTable: "touristpanel_reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_purchase_item_contexts",
                schema: "entertainments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderPayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_purchase_item_contexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_touristpanel_purchase_item_contexts_purchase_items_Id",
                        column: x => x.Id,
                        principalSchema: "entertainments",
                        principalTable: "purchase_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_operations",
                schema: "entertainments",
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
                    table.PrimaryKey("PK_touristpanel_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_touristpanel_operations_touristpanel_issuances_ProviderFulf~",
                        column: x => x.ProviderFulfillmentId,
                        principalSchema: "entertainments",
                        principalTable: "touristpanel_issuances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "touristpanel_tickets",
                schema: "entertainments",
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
                    table.PrimaryKey("PK_touristpanel_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_touristpanel_tickets_touristpanel_issuances_ProviderFulfill~",
                        column: x => x.ProviderFulfillmentId,
                        principalSchema: "entertainments",
                        principalTable: "touristpanel_issuances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_CartId_ProviderKey_ProgramKey_OfferKey_PurchaseO~",
                schema: "entertainments",
                table: "cart_items",
                columns: new[] { "CartId", "ProviderKey", "ProgramKey", "OfferKey", "PurchaseOptionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_carts_UserId",
                schema: "entertainments",
                table: "carts",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_checkout_sessions_Status_PayableUntil",
                schema: "entertainments",
                table: "checkout_sessions",
                columns: new[] { "Status", "PayableUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_checkout_sessions_UserId_IdempotencyKey",
                schema: "entertainments",
                table: "checkout_sessions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_items_EntertainmentPurchaseId",
                schema: "entertainments",
                table: "purchase_items",
                column: "EntertainmentPurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_OrderId",
                schema: "entertainments",
                table: "purchases",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchases_UserId_IdempotencyKey",
                schema: "entertainments",
                table: "purchases",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_touristpanel_issuances_EntertainmentPurchaseId_ProviderKey",
                schema: "entertainments",
                table: "touristpanel_issuances",
                columns: new[] { "EntertainmentPurchaseId", "ProviderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_touristpanel_operations_ProviderFulfillmentId_IdempotencyKey",
                schema: "entertainments",
                table: "touristpanel_operations",
                columns: new[] { "ProviderFulfillmentId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_touristpanel_programs_AccountKey_IsListed_NextDetailAttempt~",
                schema: "entertainments",
                table: "touristpanel_programs",
                columns: new[] { "AccountKey", "IsListed", "NextDetailAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_touristpanel_programs_AccountKey_NativeId",
                schema: "entertainments",
                table: "touristpanel_programs",
                columns: new[] { "AccountKey", "NativeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_touristpanel_tickets_CodeHash",
                schema: "entertainments",
                table: "touristpanel_tickets",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_touristpanel_tickets_ProviderFulfillmentId",
                schema: "entertainments",
                table: "touristpanel_tickets",
                column: "ProviderFulfillmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cart_items",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "checkout_sessions",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_location_mappings",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_operations",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_programs",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_purchase_item_contexts",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_receipts",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_sync_states",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_tickets",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "carts",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "purchase_items",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_issuances",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "touristpanel_reservations",
                schema: "entertainments");

            migrationBuilder.DropTable(
                name: "purchases",
                schema: "entertainments");
        }
    }
}
