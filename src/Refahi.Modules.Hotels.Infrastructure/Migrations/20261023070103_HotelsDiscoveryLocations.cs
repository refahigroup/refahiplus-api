using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    public partial class HotelsDiscoveryLocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE hotels.snapptrip_discovery_sync_states SET \"NextAttemptAtUtc\" = CURRENT_TIMESTAMP; UPDATE hotels.lamaso_discovery_sync_states SET \"NextAttemptAtUtc\" = CURRENT_TIMESTAMP;");
            migrationBuilder.AddColumn<double>(name: "Latitude", schema: "hotels", table: "snapptrip_discovery_hotels", type: "double precision", nullable: true);
            migrationBuilder.AddColumn<double>(name: "Longitude", schema: "hotels", table: "snapptrip_discovery_hotels", type: "double precision", nullable: true);
            migrationBuilder.AddColumn<double>(name: "Latitude", schema: "hotels", table: "lamaso_discovery_hotels", type: "double precision", nullable: true);
            migrationBuilder.AddColumn<double>(name: "Longitude", schema: "hotels", table: "lamaso_discovery_hotels", type: "double precision", nullable: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Latitude", schema: "hotels", table: "snapptrip_discovery_hotels");
            migrationBuilder.DropColumn(name: "Longitude", schema: "hotels", table: "snapptrip_discovery_hotels");
            migrationBuilder.DropColumn(name: "Latitude", schema: "hotels", table: "lamaso_discovery_hotels");
            migrationBuilder.DropColumn(name: "Longitude", schema: "hotels", table: "lamaso_discovery_hotels");
        }
    }
}
