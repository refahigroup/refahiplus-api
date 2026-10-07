using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Cinema.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CinemaInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cinema");

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "cinema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScheduleId = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    SeatIdsJson = table.Column<string>(type: "text", nullable: false),
                    CategoryCode = table.Column<string>(type: "text", nullable: false),
                    ProviderOrderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TicketCode = table.Column<string>(type: "text", nullable: true),
                    SamfaCode = table.Column<string>(type: "text", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderNumber = table.Column<string>(type: "text", nullable: true),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    IssuanceStatus = table.Column<string>(type: "text", nullable: false),
                    CancellationStatus = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    SubtotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    DiscountMinor = table.Column<long>(type: "bigint", nullable: false),
                    FeeMinor = table.Column<long>(type: "bigint", nullable: false),
                    TaxMinor = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_attempts",
                schema: "cinema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CinemaOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_attempts_orders_CinemaOrderId",
                        column: x => x.CinemaOrderId,
                        principalSchema: "cinema",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orders_IssuanceStatus_UpdatedAt",
                schema: "cinema",
                table: "orders",
                columns: new[] { "IssuanceStatus", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrderId",
                schema: "cinema",
                table: "orders",
                column: "OrderId",
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ProviderKey_ProviderOrderId",
                schema: "cinema",
                table: "orders",
                columns: new[] { "ProviderKey", "ProviderOrderId" },
                unique: true,
                filter: "\"ProviderOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_UserId_IdempotencyKey",
                schema: "cinema",
                table: "orders",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_attempts_CinemaOrderId",
                schema: "cinema",
                table: "provider_attempts",
                column: "CinemaOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_attempts",
                schema: "cinema");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "cinema");
        }
    }
}
