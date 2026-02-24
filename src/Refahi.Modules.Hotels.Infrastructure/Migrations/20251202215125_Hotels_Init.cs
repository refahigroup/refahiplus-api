using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Hotels_Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Hotels");

            migrationBuilder.CreateTable(
                name: "hotel_bookings",
                schema: "Hotels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ProviderBookingCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderHotelId = table.Column<long>(type: "bigint", nullable: false),
                    ProviderRoomId = table.Column<long>(type: "bigint", nullable: false),
                    CheckIn = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOut = table.Column<DateOnly>(type: "date", nullable: false),
                    RoomsCount = table.Column<int>(type: "integer", nullable: false),
                    BoardType = table.Column<int>(type: "integer", nullable: false),
                    BasePriceAmount = table.Column<long>(type: "bigint", nullable: false),
                    BasePriceCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MarginAmount = table.Column<long>(type: "bigint", nullable: false),
                    MarginCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CustomerPriceAmount = table.Column<long>(type: "bigint", nullable: false),
                    CustomerPriceCurrency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoucherNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VoucherUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hotel_bookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hotel_booking_guests",
                schema: "Hotels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Age = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hotel_booking_guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hotel_booking_guests_hotel_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "Hotels",
                        principalTable: "hotel_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hotel_booking_guests_BookingId",
                schema: "Hotels",
                table: "hotel_booking_guests",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hotel_booking_guests",
                schema: "Hotels");

            migrationBuilder.DropTable(
                name: "hotel_bookings",
                schema: "Hotels");
        }
    }
}
