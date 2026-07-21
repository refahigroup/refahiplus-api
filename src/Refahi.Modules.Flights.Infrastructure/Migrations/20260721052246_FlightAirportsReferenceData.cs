using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Flights.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FlightAirportsReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.CreateTable(
                name: "airports",
                schema: "flights",
                columns: table => new
                {
                    iata_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    icao_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    city_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    airport_name_fa = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    airport_name_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    city_name_fa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    city_name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    country_name_fa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    is_popular = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    source_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    translation_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    imported_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_airports", x => x.iata_code);
                });

            migrationBuilder.CreateIndex(
                name: "ix_flight_airports_active_popular",
                schema: "flights",
                table: "airports",
                columns: new[] { "is_active", "is_popular" });

            migrationBuilder.CreateIndex(
                name: "ix_flight_airports_icao_code",
                schema: "flights",
                table: "airports",
                column: "icao_code");

            migrationBuilder.Sql(
                "CREATE INDEX ix_flight_airports_search_text_trgm ON flights.airports USING gin (search_text gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "airports",
                schema: "flights");
        }
    }
}
