using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TouristPanelReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckCount",
                schema: "commerce",
                table: "provider_fulfillments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryProtected",
                schema: "commerce",
                table: "provider_fulfillments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextCheckAt",
                schema: "commerce",
                table: "provider_fulfillments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderInvoiceId",
                schema: "commerce",
                table: "provider_fulfillments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentId",
                schema: "commerce",
                table: "provider_fulfillments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReconcileStartedAt",
                schema: "commerce",
                table: "provider_fulfillments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CartSelectionProtected",
                schema: "commerce",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutSessionId",
                schema: "commerce",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PayableUntil",
                schema: "commerce",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationContextProtected",
                schema: "commerce",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationReference",
                schema: "commerce",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalog_snapshots",
                schema: "commerce",
                columns: table => new
                {
                    AccountKey = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_snapshots", x => x.AccountKey);
                });

            migrationBuilder.CreateTable(
                name: "checkout_sessions",
                schema: "commerce",
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
                    CommerceOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkout_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_receipts",
                schema: "commerce",
                columns: table => new
                {
                    OperationId = table.Column<string>(type: "text", nullable: false),
                    AccountKey = table.Column<string>(type: "text", nullable: false),
                    PayloadProtected = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_receipts", x => new { x.AccountKey, x.OperationId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_checkout_sessions_Status_PayableUntil",
                schema: "commerce",
                table: "checkout_sessions",
                columns: new[] { "Status", "PayableUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_checkout_sessions_UserId_IdempotencyKey",
                schema: "commerce",
                table: "checkout_sessions",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_snapshots",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "checkout_sessions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "provider_receipts",
                schema: "commerce");

            migrationBuilder.DropColumn(
                name: "CheckCount",
                schema: "commerce",
                table: "provider_fulfillments");

            migrationBuilder.DropColumn(
                name: "DeliveryProtected",
                schema: "commerce",
                table: "provider_fulfillments");

            migrationBuilder.DropColumn(
                name: "NextCheckAt",
                schema: "commerce",
                table: "provider_fulfillments");

            migrationBuilder.DropColumn(
                name: "ProviderInvoiceId",
                schema: "commerce",
                table: "provider_fulfillments");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentId",
                schema: "commerce",
                table: "provider_fulfillments");

            migrationBuilder.DropColumn(
                name: "ReconcileStartedAt",
                schema: "commerce",
                table: "provider_fulfillments");

            migrationBuilder.DropColumn(
                name: "CartSelectionProtected",
                schema: "commerce",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CheckoutSessionId",
                schema: "commerce",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PayableUntil",
                schema: "commerce",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ReservationContextProtected",
                schema: "commerce",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ReservationReference",
                schema: "commerce",
                table: "orders");
        }
    }
}
