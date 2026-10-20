using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Flights.Infrastructure.Migrations;

public partial class FlightGeographyMappings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $guard$ BEGIN
                IF to_regclass('"references".province_reorganization_history') IS NULL
                THEN RAISE EXCEPTION 'Reference geography V4 must be installed before FlightGeographyMappings'; END IF;
            END $guard$;
            """);
        foreach (var item in new[] { (Table: "airport_geography_mappings", Key: "AirportIataCode", Parent: "airports", ParentKey: "iata_code"),
            (Table: "snapptrip_city_mappings", Key: "ProviderCityCode", Parent: "search_cities", ParentKey: "city_code") })
        {
            migrationBuilder.Sql($"""
                CREATE TABLE flights.{item.Table} (
                    "{item.Key}" varchar(3) PRIMARY KEY REFERENCES flights.{item.Parent}("{item.ParentKey}") ON DELETE RESTRICT,
                    "CityId" integer NOT NULL REFERENCES "references".cities("Id") ON DELETE RESTRICT,
                    "SourceIdentity" text NOT NULL, "MatchMethod" text NOT NULL, "Evidence" jsonb NOT NULL,
                    "SourceVersion" text NOT NULL, "SourceSnapshotDate" date NOT NULL, "CreatedAtUtc" timestamptz NOT NULL);
                CREATE INDEX "IX_{item.Table}_CityId" ON flights.{item.Table}("CityId");
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $guard$ BEGIN
            RAISE EXCEPTION 'Cannot downgrade geography mappings; roll back the application and keep shared reference identities';
        END $guard$;
        """);
}
