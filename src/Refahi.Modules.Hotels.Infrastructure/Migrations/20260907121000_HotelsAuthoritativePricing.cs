using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Refahi.Modules.Hotels.Infrastructure.Persistence;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations;

[DbContext(typeof(HotelsDbContext))]
[Migration("20260907121000_HotelsAuthoritativePricing")]
public sealed class HotelsAuthoritativePricing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "pricing_version",
            schema: "hotels",
            table: "hotel_requests",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "pricing_version",
            schema: "hotels",
            table: "hotel_requests");
    }
}
