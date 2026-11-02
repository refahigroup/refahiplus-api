using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lamaso_discovery_hotels",
                schema: "hotels",
                columns: table => new
                {
                    HotelKey = table.Column<Guid>(type: "uuid", nullable: false),
                    NativeId = table.Column<string>(type: "text", nullable: false),
                    NativeCityId = table.Column<string>(type: "text", nullable: false),
                    ReferenceCityId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    ImagesJson = table.Column<string>(type: "text", nullable: false),
                    FacilitiesJson = table.Column<string>(type: "text", nullable: false),
                    Policies = table.Column<string>(type: "text", nullable: true),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    SortTitle = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lamaso_discovery_hotels", x => x.HotelKey);
                });

            migrationBuilder.CreateTable(
                name: "lamaso_discovery_sync_states",
                schema: "hotels",
                columns: table => new
                {
                    NativeCityId = table.Column<string>(type: "text", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lamaso_discovery_sync_states", x => x.NativeCityId);
                });

            migrationBuilder.CreateTable(
                name: "snapptrip_discovery_hotels",
                schema: "hotels",
                columns: table => new
                {
                    HotelKey = table.Column<Guid>(type: "uuid", nullable: false),
                    NativeId = table.Column<string>(type: "text", nullable: false),
                    NativeCityId = table.Column<string>(type: "text", nullable: false),
                    ReferenceCityId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    ImagesJson = table.Column<string>(type: "text", nullable: false),
                    FacilitiesJson = table.Column<string>(type: "text", nullable: false),
                    Policies = table.Column<string>(type: "text", nullable: true),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    SortTitle = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapptrip_discovery_hotels", x => x.HotelKey);
                });

            migrationBuilder.CreateTable(
                name: "snapptrip_discovery_sync_states",
                schema: "hotels",
                columns: table => new
                {
                    NativeCityId = table.Column<string>(type: "text", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapptrip_discovery_sync_states", x => x.NativeCityId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lamaso_discovery_hotels_NativeId",
                schema: "hotels",
                table: "lamaso_discovery_hotels",
                column: "NativeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lamaso_discovery_hotels_ReferenceCityId_Published_SortTitle",
                schema: "hotels",
                table: "lamaso_discovery_hotels",
                columns: new[] { "ReferenceCityId", "Published", "SortTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_snapptrip_discovery_hotels_NativeId",
                schema: "hotels",
                table: "snapptrip_discovery_hotels",
                column: "NativeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_snapptrip_discovery_hotels_ReferenceCityId_Published_SortTi~",
                schema: "hotels",
                table: "snapptrip_discovery_hotels",
                columns: new[] { "ReferenceCityId", "Published", "SortTitle" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lamaso_discovery_hotels",
                schema: "hotels");

            migrationBuilder.DropTable(
                name: "lamaso_discovery_sync_states",
                schema: "hotels");

            migrationBuilder.DropTable(
                name: "snapptrip_discovery_hotels",
                schema: "hotels");

            migrationBuilder.DropTable(
                name: "snapptrip_discovery_sync_states",
                schema: "hotels");
        }
    }
}
