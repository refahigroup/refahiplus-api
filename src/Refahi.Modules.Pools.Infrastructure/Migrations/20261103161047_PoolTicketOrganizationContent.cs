using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Pools.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PoolTicketOrganizationContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "poolticket_contents",
                schema: "pools",
                columns: table => new
                {
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    NativeCityId = table.Column<string>(type: "text", nullable: false),
                    NativeStateId = table.Column<string>(type: "text", nullable: false),
                    DescriptionHtml = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Telephone = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poolticket_contents", x => x.VenueId);
                    table.ForeignKey(
                        name: "FK_poolticket_contents_venues_VenueId",
                        column: x => x.VenueId,
                        principalSchema: "pools",
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poolticket_geographies",
                schema: "pools",
                columns: table => new
                {
                    CityId = table.Column<string>(type: "text", nullable: false),
                    StateId = table.Column<string>(type: "text", nullable: false),
                    CityName = table.Column<string>(type: "text", nullable: false),
                    StateName = table.Column<string>(type: "text", nullable: false),
                    ReferenceProvinceId = table.Column<int>(type: "integer", nullable: true),
                    ReferenceCityId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poolticket_geographies", x => x.CityId);
                });

            migrationBuilder.CreateTable(
                name: "poolticket_sync_states",
                schema: "pools",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poolticket_sync_states", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_poolticket_geographies_ReferenceProvinceId_ReferenceCityId",
                schema: "pools",
                table: "poolticket_geographies",
                columns: new[] { "ReferenceProvinceId", "ReferenceCityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poolticket_contents",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "poolticket_geographies",
                schema: "pools");

            migrationBuilder.DropTable(
                name: "poolticket_sync_states",
                schema: "pools");
        }
    }
}
