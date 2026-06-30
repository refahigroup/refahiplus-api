using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsAddBookingSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hotel_booking_sagas",
                schema: "hotels",
                columns: table => new
                {
                    saga_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hotel_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    provider_booking_status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hotel_booking_sagas", x => x.saga_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hotel_booking_sagas_hotel_request_id",
                schema: "hotels",
                table: "hotel_booking_sagas",
                column: "hotel_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hotel_booking_sagas_order_id",
                schema: "hotels",
                table: "hotel_booking_sagas",
                column: "order_id",
                unique: true,
                filter: "\"order_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_hotel_booking_sagas_status_updated_at",
                schema: "hotels",
                table: "hotel_booking_sagas",
                columns: new[] { "status", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hotel_booking_sagas",
                schema: "hotels");
        }
    }
}
