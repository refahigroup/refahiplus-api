using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsReferenceCitySearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF to_regclass('hotels.snapptrip_city_mappings') IS NULL
                       OR to_regclass('hotels.lamaso_city_mappings') IS NULL
                       OR NOT EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema='hotels' AND table_name='lamaso_city_mappings' AND column_name='IsEnabled')
                    THEN RAISE EXCEPTION 'Hotel geography V4 must be installed before HotelsReferenceCitySearch.';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.AlterColumn<Guid>(
                name: "CityKey",
                schema: "hotels",
                table: "catalogue_listings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "ReferenceCityId",
                schema: "hotels",
                table: "catalogue_listings",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE hotels.catalogue_listings l SET "ReferenceCityId"=m."CityId"
                FROM hotels.snapptrip_city_mappings m
                WHERE l."Provider"='SnappTrip' AND l."NativeCityId"=m."ProviderCityId";
                UPDATE hotels.catalogue_listings l SET "ReferenceCityId"=m."CityId"
                FROM hotels.lamaso_city_mappings m
                WHERE l."Provider"='Lamasoo' AND l."NativeCityId"=m."ProviderCityId";
                """);
            migrationBuilder.CreateIndex(
                name: "IX_catalogue_listings_ReferenceCityId",
                schema: "hotels",
                table: "catalogue_listings",
                column: "ReferenceCityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Roll back applications while retaining additive data and stable identifiers.
            throw new NotSupportedException("Keep the expanded hotel schema when rolling back the application.");
        }
    }
}
