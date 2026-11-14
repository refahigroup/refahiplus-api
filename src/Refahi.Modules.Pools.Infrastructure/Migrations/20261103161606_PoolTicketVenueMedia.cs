using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Pools.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PoolTicketVenueMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                schema: "pools",
                table: "poolticket_contents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ImagesJson",
                schema: "pools",
                table: "poolticket_contents",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttributesJson",
                schema: "pools",
                table: "poolticket_contents");

            migrationBuilder.DropColumn(
                name: "ImagesJson",
                schema: "pools",
                table: "poolticket_contents");
        }
    }
}
