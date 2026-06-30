using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsAddProviderBookingCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hotel_provider_booking_cache",
                schema: "hotels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    saga_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    provider_booking_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    response_json = table.Column<string>(type: "jsonb", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hotel_provider_booking_cache", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hotel_provider_booking_cache_status_updated_at",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ux_hotel_provider_booking_cache_provider_code",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                columns: new[] { "provider_name", "provider_booking_code" },
                unique: true,
                filter: "\"provider_booking_code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_hotel_provider_booking_cache_provider_idem",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                columns: new[] { "provider_name", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hotel_provider_booking_cache",
                schema: "hotels");
        }
    }
}
