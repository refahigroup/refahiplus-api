using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Orders_PromotionsAndOutboxLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lease_id",
                schema: "orders",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_until",
                schema: "orders",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                schema: "orders",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pending_promotion_operation_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pricing_version",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<Guid>(
                name: "promotion_campaign_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "promotion_code_hash",
                schema: "orders",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "promotion_expires_at",
                schema: "orders",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "promotion_funding_wallet_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "promotion_reservation_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "promotion_snapshot_json",
                schema: "orders",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "promotion_operations",
                schema: "orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    CodeCiphertext = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotion_operations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_promotion_operations_OrderId_IdempotencyKey",
                schema: "orders",
                table: "promotion_operations",
                columns: new[] { "OrderId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotion_operations",
                schema: "orders");

            migrationBuilder.DropColumn(
                name: "lease_id",
                schema: "orders",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "lease_until",
                schema: "orders",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                schema: "orders",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "pending_promotion_operation_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "pricing_version",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "promotion_campaign_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "promotion_code_hash",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "promotion_expires_at",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "promotion_funding_wallet_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "promotion_reservation_id",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "promotion_snapshot_json",
                schema: "orders",
                table: "orders");
        }
    }
}
