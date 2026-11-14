using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Pools.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PoolsCataloguePurchasesAndVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nonempty legacy rows remain until the explicit financial cleanup operation.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF to_regclass('pools.mock_reservations') IS NOT NULL THEN
                    IF NOT EXISTS (SELECT 1 FROM pools.mock_reservations) THEN
                      DROP TABLE pools.mock_reservations;
                    END IF;
                  END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "purchases",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderNumber = table.Column<string>(type: "text", nullable: true),
                    TotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    PayableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Area = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<decimal>(type: "numeric", nullable: true),
                    MinimumSaleMinor = table.Column<long>(type: "bigint", nullable: true),
                    IsSelling = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TermsHtml = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SessionsFetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "requests",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ExternalPoolId = table.Column<string>(type: "text", nullable: false),
                    ExternalRowId = table.Column<string>(type: "text", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    PoolName = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    Time = table.Column<string>(type: "text", nullable: false),
                    AdultCount = table.Column<int>(type: "integer", nullable: false),
                    ChildCount = table.Column<int>(type: "integer", nullable: false),
                    TotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReservationStatus = table.Column<int>(type: "integer", nullable: false),
                    VoucherStatus = table.Column<int>(type: "integer", nullable: true),
                    CodeHash = table.Column<string>(type: "text", nullable: true),
                    CodeCiphertext = table.Column<string>(type: "text", nullable: true),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RedeemedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RedeemedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RedemptionKey = table.Column<string>(type: "text", nullable: true),
                    RedemptionFingerprint = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_requests_purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalSchema: "pools",
                        principalTable: "purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "pools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalGroupId = table.Column<string>(type: "text", nullable: false),
                    ExternalRowId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<string>(type: "text", nullable: false),
                    Time = table.Column<string>(type: "text", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AdultMinor = table.Column<long>(type: "bigint", nullable: false),
                    ChildMinor = table.Column<long>(type: "bigint", nullable: false),
                    AdultEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ChildEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsSelling = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DescriptionHtml = table.Column<string>(type: "text", nullable: false),
                    TermsHtml = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sessions_venues_PoolId",
                        column: x => x.PoolId,
                        principalSchema: "pools",
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_CreatedAt_Id",
                schema: "pools",
                table: "purchases",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_OrderId",
                schema: "pools",
                table: "purchases",
                column: "OrderId",
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_UserId_IdempotencyKey",
                schema: "pools",
                table: "purchases",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_requests_CodeHash",
                schema: "pools",
                table: "requests",
                column: "CodeHash",
                unique: true,
                filter: "\"CodeHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_requests_PurchaseId",
                schema: "pools",
                table: "requests",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_requests_RedeemedBy_RedemptionKey",
                schema: "pools",
                table: "requests",
                columns: new[] { "RedeemedBy", "RedemptionKey" },
                unique: true,
                filter: "\"RedemptionKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_IsActive_StartsAtUtc_Id",
                schema: "pools",
                table: "sessions",
                columns: new[] { "IsActive", "StartsAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_PoolId_ExternalGroupId_ExternalRowId",
                schema: "pools",
                table: "sessions",
                columns: new[] { "PoolId", "ExternalGroupId", "ExternalRowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_venues_IsActive_State_City_Name",
                schema: "pools",
                table: "venues",
                columns: new[] { "IsActive", "State", "City", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_venues_ProviderKey_ExternalId",
                schema: "pools",
                table: "venues",
                columns: new[] { "ProviderKey", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "requests",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "purchases",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "venues",
                schema: "pools");

            migrationBuilder.CreateTable(
                name: "mock_reservations",
                schema: "pools",
                columns: table => new
                {
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    OperationId = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TicketCode = table.Column<string>(type: "text", nullable: true),
                    TotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mock_reservations", x => x.Reference);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mock_reservations_ProviderKey_OperationId",
                schema: "pools",
                table: "mock_reservations",
                columns: new[] { "ProviderKey", "OperationId" },
                unique: true);
        }
    }
}
