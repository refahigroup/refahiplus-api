using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Entertainments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntertainmentsReservationRecoveryAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResolutionActorId",
                schema: "entertainments",
                table: "touristpanel_session_reservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionEvidenceProtected",
                schema: "entertainments",
                table: "touristpanel_session_reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionFingerprint",
                schema: "entertainments",
                table: "touristpanel_session_reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionKey",
                schema: "entertainments",
                table: "touristpanel_session_reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionOutcome",
                schema: "entertainments",
                table: "touristpanel_session_reservations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAtUtc",
                schema: "entertainments",
                table: "touristpanel_session_reservations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolutionActorId",
                schema: "entertainments",
                table: "touristpanel_session_reservations");

            migrationBuilder.DropColumn(
                name: "ResolutionEvidenceProtected",
                schema: "entertainments",
                table: "touristpanel_session_reservations");

            migrationBuilder.DropColumn(
                name: "ResolutionFingerprint",
                schema: "entertainments",
                table: "touristpanel_session_reservations");

            migrationBuilder.DropColumn(
                name: "ResolutionKey",
                schema: "entertainments",
                table: "touristpanel_session_reservations");

            migrationBuilder.DropColumn(
                name: "ResolutionOutcome",
                schema: "entertainments",
                table: "touristpanel_session_reservations");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                schema: "entertainments",
                table: "touristpanel_session_reservations");
        }
    }
}
