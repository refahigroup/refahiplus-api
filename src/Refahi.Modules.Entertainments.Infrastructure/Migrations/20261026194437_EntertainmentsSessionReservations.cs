using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Entertainments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntertainmentsSessionReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "touristpanel_session_reservations",
                schema: "entertainments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationProtected = table.Column<string>(type: "text", nullable: true),
                    ReservationReference = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_session_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_touristpanel_session_reservations_checkout_sessions_Id",
                        column: x => x.Id,
                        principalSchema: "entertainments",
                        principalTable: "checkout_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.Sql("""
                INSERT INTO entertainments.touristpanel_session_reservations ("Id", "ReservationProtected", "ReservationReference")
                SELECT "Id", "ReservationProtected", "ReservationReference" FROM entertainments.checkout_sessions;
                """);
            migrationBuilder.DropColumn(
                name: "ReservationProtected",
                schema: "entertainments",
                table: "checkout_sessions");

            migrationBuilder.DropColumn(
                name: "ReservationReference",
                schema: "entertainments",
                table: "checkout_sessions");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReservationProtected",
                schema: "entertainments",
                table: "checkout_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationReference",
                schema: "entertainments",
                table: "checkout_sessions",
                type: "text",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE entertainments.checkout_sessions AS target
                SET "ReservationProtected" = source."ReservationProtected", "ReservationReference" = source."ReservationReference"
                FROM entertainments.touristpanel_session_reservations AS source WHERE source."Id" = target."Id";
                """);
            migrationBuilder.DropTable(
                name: "touristpanel_session_reservations",
                schema: "entertainments");

        }
    }
}
