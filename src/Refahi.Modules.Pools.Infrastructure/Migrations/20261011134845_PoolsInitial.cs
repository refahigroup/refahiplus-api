using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Pools.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PoolsInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pools");

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ProviderReference = table.Column<string>(type: "text", nullable: true),
                    IsMock = table.Column<bool>(type: "boolean", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderNumber = table.Column<string>(type: "text", nullable: true),
                    TotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    PayableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentStatus = table.Column<string>(type: "text", nullable: false),
                    ReservationStatus = table.Column<string>(type: "text", nullable: false),
                    IssuanceStatus = table.Column<string>(type: "text", nullable: false),
                    CancellationStatus = table.Column<string>(type: "text", nullable: false),
                    TicketCode = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_snapshots",
                schema: "pools",
                columns: table => new
                {
                    ProviderKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_snapshots", x => new { x.ProviderKey, x.ExternalId });
                });

            migrationBuilder.CreateTable(
                name: "mock_reservations",
                schema: "pools",
                columns: table => new
                {
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    OperationId = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TicketCode = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_reservations", x => x.Reference);
                });

            migrationBuilder.CreateTable(
                name: "provider_attempts",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_attempts_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "pools",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_CreatedAt_Id",
                schema: "pools",
                table: "bookings",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_OrderId",
                schema: "pools",
                table: "bookings",
                column: "OrderId",
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_ProviderKey_ProviderReference",
                schema: "pools",
                table: "bookings",
                columns: new[] { "ProviderKey", "ProviderReference" },
                unique: true,
                filter: "\"ProviderReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId_IdempotencyKey",
                schema: "pools",
                table: "bookings",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mock_reservations_ProviderKey_OperationId",
                schema: "pools",
                table: "mock_reservations",
                columns: new[] { "ProviderKey", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_attempts_BookingId",
                schema: "pools",
                table: "provider_attempts",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_snapshots",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "mock_reservations",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "provider_attempts",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "bookings",
                schema: "pools");
        }
    }
}

