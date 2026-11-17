using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Bus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BusInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bus");

            migrationBuilder.CreateTable(
                name: "hiholiday_city_mappings",
                schema: "bus",
                columns: table => new
                {
                    CityId = table.Column<int>(type: "integer", nullable: false),
                    ProviderRegionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hiholiday_city_mappings", x => x.CityId);
                });

            migrationBuilder.CreateTable(
                name: "hiholiday_offers",
                schema: "bus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginCityId = table.Column<int>(type: "integer", nullable: false),
                    DestinationCityId = table.Column<int>(type: "integer", nullable: false),
                    DepartureDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Service_Code = table.Column<string>(type: "text", nullable: false),
                    Service_Company = table.Column<string>(type: "text", nullable: false),
                    Service_BusType = table.Column<string>(type: "text", nullable: false),
                    Service_DepartureTerminal = table.Column<string>(type: "text", nullable: false),
                    Service_ArrivalTerminal = table.Column<string>(type: "text", nullable: false),
                    Service_DepartureTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Service_AvailableSeats = table.Column<int>(type: "integer", nullable: false),
                    Service_PriceMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hiholiday_offers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hiholiday_city_mappings_ProviderRegionId",
                schema: "bus",
                table: "hiholiday_city_mappings",
                column: "ProviderRegionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hiholiday_offers_ExpiresAt",
                schema: "bus",
                table: "hiholiday_offers",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hiholiday_city_mappings",
                schema: "bus");

            migrationBuilder.DropTable(
                name: "hiholiday_offers",
                schema: "bus");
        }
    }
}
