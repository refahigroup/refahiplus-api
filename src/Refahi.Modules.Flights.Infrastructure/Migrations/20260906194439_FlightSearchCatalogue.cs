using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Flights.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FlightSearchCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE flights.catalog_imports (
                    version varchar(50) PRIMARY KEY,
                    applied_at_utc timestamptz NOT NULL,
                    report jsonb NOT NULL
                );
                CREATE TABLE flights.catalog_backups (
                    version varchar(50) NOT NULL,
                    table_name varchar(50) NOT NULL,
                    row_key text NOT NULL,
                    row_data jsonb NULL,
                    PRIMARY KEY (version, table_name, row_key)
                );
                """);
            migrationBuilder.CreateTable(
                name: "search_cities",
                schema: "flights",
                columns: table => new
                {
                    city_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    city_name_fa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    city_name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    country_name_fa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_cities", x => x.city_code);
                });

            migrationBuilder.CreateTable(
                name: "search_memberships",
                schema: "flights",
                columns: table => new
                {
                    city_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    airport_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_domestic = table.Column<bool>(type: "boolean", nullable: false),
                    city_rank = table.Column<int>(type: "integer", nullable: false),
                    airport_rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_memberships", x => new { x.is_domestic, x.city_code, x.airport_code });
                    table.ForeignKey(
                        name: "FK_search_memberships_airports_airport_code",
                        column: x => x.airport_code,
                        principalSchema: "flights",
                        principalTable: "airports",
                        principalColumn: "iata_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_search_memberships_search_cities_city_code",
                        column: x => x.city_code,
                        principalSchema: "flights",
                        principalTable: "search_cities",
                        principalColumn: "city_code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_search_memberships_airport_code",
                schema: "flights",
                table: "search_memberships",
                column: "airport_code");

            migrationBuilder.CreateIndex(
                name: "IX_search_memberships_city_code",
                schema: "flights",
                table: "search_memberships",
                column: "city_code");

            migrationBuilder.CreateIndex(
                name: "IX_search_memberships_is_domestic_airport_code",
                schema: "flights",
                table: "search_memberships",
                columns: new[] { "is_domestic", "airport_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE flights.catalog_backups; DROP TABLE flights.catalog_imports;");
            migrationBuilder.DropTable(
                name: "search_memberships",
                schema: "flights");

            migrationBuilder.DropTable(
                name: "search_cities",
                schema: "flights");
        }
    }
}
