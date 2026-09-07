using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Flights.Infrastructure.Persistence;

#nullable disable

namespace Refahi.Modules.Flights.Infrastructure.Migrations;

[DbContext(typeof(FlightsDbContext))]
[Migration("20260907120000_FlightCustomerPayablePricing")]
public sealed class FlightCustomerPayablePricing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "commission_amount",
            schema: "flights",
            table: "flight_search_offer_snapshots",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "customer_payable_amount",
            schema: "flights",
            table: "flight_search_offer_snapshots",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<int>(
            name: "pricing_version",
            schema: "flights",
            table: "flight_search_offer_snapshots",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "commission_amount",
            schema: "flights",
            table: "flight_search_offer_snapshots");

        migrationBuilder.DropColumn(
            name: "customer_payable_amount",
            schema: "flights",
            table: "flight_search_offer_snapshots");

        migrationBuilder.DropColumn(
            name: "pricing_version",
            schema: "flights",
            table: "flight_search_offer_snapshots");
    }
}
