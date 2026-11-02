using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsLamasoBookingLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM hotels.external_reservations WHERE "Provider" <> 'Lamasoo') THEN
                    RAISE EXCEPTION 'Unexpected provider in external_reservations; separate provider data before upgrading.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropPrimaryKey(
                name: "PK_external_reservations",
                schema: "hotels",
                table: "external_reservations");

            migrationBuilder.RenameTable(
                name: "external_reservations",
                schema: "hotels",
                newName: "lamaso_reservations",
                newSchema: "hotels");

            migrationBuilder.RenameIndex(
                name: "IX_external_reservations_State_UpdatedAtUtc",
                schema: "hotels",
                table: "lamaso_reservations",
                newName: "IX_lamaso_reservations_State_UpdatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_external_reservations_Provider_ReferenceId",
                schema: "hotels",
                table: "lamaso_reservations",
                newName: "IX_lamaso_reservations_Provider_ReferenceId");

            migrationBuilder.AddColumn<bool>(
                name: "ConfirmationAttempted",
                schema: "hotels",
                table: "lamaso_reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "HotelShareMinor",
                schema: "hotels",
                table: "lamaso_reservations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRecoveryAtUtc",
                schema: "hotels",
                table: "lamaso_reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "NoExternalLiability",
                schema: "hotels",
                table: "lamaso_reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentBlocked",
                schema: "hotels",
                table: "lamaso_reservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                schema: "hotels",
                table: "lamaso_reservations",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE hotels.lamaso_reservations SET
                  "HotelShareMinor" = GREATEST(0, "SupplyPriceMinor" - "FeeMinor"),
                  "ConfirmationAttempted" = "State" IN (3,4,5),
                  "NextRecoveryAtUtc" = now(),
                  "PaymentBlocked" = "SupplyPriceMinor" > COALESCE((SELECT r.total_price FROM hotels.hotel_requests r WHERE r.id = "HotelRequestId"), 0),
                  "ReviewReason" = CASE WHEN "State" IN (5,6,7) THEN 'LEGACY_OUTCOME_REQUIRES_REVIEW' ELSE NULL END,
                  "State" = CASE WHEN "State" IN (6,7) THEN 5 ELSE "State" END;
                """);
            migrationBuilder.AddPrimaryKey(
                name: "PK_lamaso_reservations",
                schema: "hotels",
                table: "lamaso_reservations",
                column: "HotelRequestId");

            migrationBuilder.CreateTable(
                name: "lamaso_operation_attempts",
                schema: "hotels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lamaso_operation_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lamaso_operation_attempts_lamaso_reservations_HotelRequestId",
                        column: x => x.HotelRequestId,
                        principalSchema: "hotels",
                        principalTable: "lamaso_reservations",
                        principalColumn: "HotelRequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lamaso_reservations_NextRecoveryAtUtc_State",
                schema: "hotels",
                table: "lamaso_reservations",
                columns: new[] { "NextRecoveryAtUtc", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_lamaso_operation_attempts_HotelRequestId_StartedAtUtc",
                schema: "hotels",
                table: "lamaso_operation_attempts",
                columns: new[] { "HotelRequestId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM hotels.lamaso_operation_attempts) THEN
                    RAISE EXCEPTION 'Cannot downgrade while operation evidence exists; restore a verified backup instead.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "lamaso_operation_attempts",
                schema: "hotels");

            migrationBuilder.DropPrimaryKey(
                name: "PK_lamaso_reservations",
                schema: "hotels",
                table: "lamaso_reservations");

            migrationBuilder.DropIndex(
                name: "IX_lamaso_reservations_NextRecoveryAtUtc_State",
                schema: "hotels",
                table: "lamaso_reservations");

            migrationBuilder.DropColumn(
                name: "ConfirmationAttempted",
                schema: "hotels",
                table: "lamaso_reservations");

            migrationBuilder.DropColumn(
                name: "HotelShareMinor",
                schema: "hotels",
                table: "lamaso_reservations");

            migrationBuilder.DropColumn(
                name: "NextRecoveryAtUtc",
                schema: "hotels",
                table: "lamaso_reservations");

            migrationBuilder.DropColumn(
                name: "NoExternalLiability",
                schema: "hotels",
                table: "lamaso_reservations");

            migrationBuilder.DropColumn(
                name: "PaymentBlocked",
                schema: "hotels",
                table: "lamaso_reservations");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                schema: "hotels",
                table: "lamaso_reservations");

            migrationBuilder.RenameTable(
                name: "lamaso_reservations",
                schema: "hotels",
                newName: "external_reservations",
                newSchema: "hotels");

            migrationBuilder.RenameIndex(
                name: "IX_lamaso_reservations_State_UpdatedAtUtc",
                schema: "hotels",
                table: "external_reservations",
                newName: "IX_external_reservations_State_UpdatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_lamaso_reservations_Provider_ReferenceId",
                schema: "hotels",
                table: "external_reservations",
                newName: "IX_external_reservations_Provider_ReferenceId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_external_reservations",
                schema: "hotels",
                table: "external_reservations",
                column: "HotelRequestId");
        }
    }
}
