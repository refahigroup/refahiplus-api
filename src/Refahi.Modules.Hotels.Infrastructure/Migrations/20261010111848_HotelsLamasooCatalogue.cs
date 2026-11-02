using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsLamasooCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NativeHotelId",
                schema: "hotels",
                table: "hotel_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NativeRoomId",
                schema: "hotels",
                table: "hotel_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfferToken",
                schema: "hotels",
                table: "hotel_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                schema: "hotels",
                table: "hotel_requests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalogue_city_references",
                schema: "hotels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CityKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    NativeId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LegacyId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_city_references", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalogue_listings",
                schema: "hotels",
                columns: table => new
                {
                    HotelKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CityKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    NativeId = table.Column<string>(type: "text", nullable: false),
                    NativeCityId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_listings", x => x.HotelKey);
                });

            migrationBuilder.CreateTable(
                name: "external_reservations",
                schema: "hotels",
                columns: table => new
                {
                    HotelRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ReferenceId = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    SubmitJson = table.Column<string>(type: "jsonb", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    SupplyPriceMinor = table.Column<long>(type: "bigint", nullable: false),
                    FeeMinor = table.Column<long>(type: "bigint", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReservationId = table.Column<string>(type: "text", nullable: true),
                    ResponseSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_reservations", x => x.HotelRequestId);
                });

            migrationBuilder.CreateTable(
                name: "offer_snapshots",
                schema: "hotels",
                columns: table => new
                {
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HotelKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    OfferJson = table.Column<string>(type: "jsonb", nullable: false),
                    TravelJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offer_snapshots", x => x.Token);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_city_references_CityKey",
                schema: "hotels",
                table: "catalogue_city_references",
                column: "CityKey");

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_city_references_Provider_NativeId",
                schema: "hotels",
                table: "catalogue_city_references",
                columns: new[] { "Provider", "NativeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalogue_listings_Provider_NativeId",
                schema: "hotels",
                table: "catalogue_listings",
                columns: new[] { "Provider", "NativeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_reservations_Provider_ReferenceId",
                schema: "hotels",
                table: "external_reservations",
                columns: new[] { "Provider", "ReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_reservations_State_UpdatedAtUtc",
                schema: "hotels",
                table: "external_reservations",
                columns: new[] { "State", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_offer_snapshots_ExpiresAtUtc",
                schema: "hotels",
                table: "offer_snapshots",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalogue_city_references",
                schema: "hotels");

            migrationBuilder.DropTable(
                name: "catalogue_listings",
                schema: "hotels");

            migrationBuilder.DropTable(
                name: "external_reservations",
                schema: "hotels");

            migrationBuilder.DropTable(
                name: "offer_snapshots",
                schema: "hotels");

            migrationBuilder.DropColumn(
                name: "NativeHotelId",
                schema: "hotels",
                table: "hotel_requests");

            migrationBuilder.DropColumn(
                name: "NativeRoomId",
                schema: "hotels",
                table: "hotel_requests");

            migrationBuilder.DropColumn(
                name: "OfferToken",
                schema: "hotels",
                table: "hotel_requests");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                schema: "hotels",
                table: "hotel_requests");
        }
    }
}
