using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsAddHotelRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hotel_requests",
                schema: "hotels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    expire_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provider_hotel_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_room_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_booking_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    search_criteria_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    selected_hotel_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    selected_room_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    total_price = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "IRR"),
                    breakdown = table.Column<string>(type: "jsonb", nullable: false),
                    fees = table.Column<string>(type: "jsonb", nullable: true),
                    guest_info_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hotel_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hotel_requests_order_id",
                schema: "hotels",
                table: "hotel_requests",
                column: "order_id",
                unique: true,
                filter: "\"order_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_hotel_requests_status_expire_at",
                schema: "hotels",
                table: "hotel_requests",
                columns: new[] { "status", "expire_at" });

            migrationBuilder.CreateIndex(
                name: "ix_hotel_requests_user_id",
                schema: "hotels",
                table: "hotel_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_hotel_requests_user_id_idempotency_key",
                schema: "hotels",
                table: "hotel_requests",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hotel_requests",
                schema: "hotels");
        }
    }
}
