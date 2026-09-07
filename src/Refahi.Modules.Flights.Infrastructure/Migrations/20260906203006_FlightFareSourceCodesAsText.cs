using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Flights.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FlightFareSourceCodesAsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "provider_fare_source_code",
                schema: "flights",
                table: "flight_search_offer_snapshots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "provider_fare_id",
                schema: "flights",
                table: "flight_offer_snapshots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL's varchar cast can truncate opaque provider tokens on downgrade.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    LOCK TABLE flights.flight_search_offer_snapshots, flights.flight_offer_snapshots
                        IN ACCESS EXCLUSIVE MODE;
                    IF EXISTS (SELECT 1 FROM flights.flight_search_offer_snapshots WHERE length(provider_fare_source_code) > 1000)
                       OR EXISTS (SELECT 1 FROM flights.flight_offer_snapshots WHERE length(provider_fare_id) > 200) THEN
                        RAISE EXCEPTION 'بازگشت این تغییر با وجود کدهای بلند تأمین‌کننده امکان‌پذیر نیست.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "provider_fare_source_code",
                schema: "flights",
                table: "flight_search_offer_snapshots",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "provider_fare_id",
                schema: "flights",
                table: "flight_offer_snapshots",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
