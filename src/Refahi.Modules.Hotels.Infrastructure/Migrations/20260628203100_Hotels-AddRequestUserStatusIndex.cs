using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsAddRequestUserStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_hotel_requests_user_id_status",
                schema: "hotels",
                table: "hotel_requests",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hotel_requests_user_id_status",
                schema: "hotels",
                table: "hotel_requests");
        }
    }
}
