using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsAddProviderCancellationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "external_unresolved_at",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_idempotency_key",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_completed_at",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_requested_at",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "external_unresolved_at",
                schema: "hotels",
                table: "hotel_booking_sagas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "provider_cancellation_completed_at",
                schema: "hotels",
                table: "hotel_booking_sagas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_cancellation_idempotency_key",
                schema: "hotels",
                table: "hotel_booking_sagas",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_cancellation_reason",
                schema: "hotels",
                table: "hotel_booking_sagas",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "provider_cancellation_requested_at",
                schema: "hotels",
                table: "hotel_booking_sagas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_hotel_provider_booking_cache_cancel_idem",
                schema: "hotels",
                table: "hotel_provider_booking_cache",
                column: "cancellation_idempotency_key",
                filter: "\"cancellation_idempotency_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_hotel_booking_sagas_provider_status_updated_at",
                schema: "hotels",
                table: "hotel_booking_sagas",
                columns: new[] { "provider_booking_status", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hotel_provider_booking_cache_cancel_idem",
                schema: "hotels",
                table: "hotel_provider_booking_cache");

            migrationBuilder.DropIndex(
                name: "ix_hotel_booking_sagas_provider_status_updated_at",
                schema: "hotels",
                table: "hotel_booking_sagas");

            migrationBuilder.DropColumn(
                name: "external_unresolved_at",
                schema: "hotels",
                table: "hotel_provider_booking_cache");

            migrationBuilder.DropColumn(
                name: "cancellation_idempotency_key",
                schema: "hotels",
                table: "hotel_provider_booking_cache");

            migrationBuilder.DropColumn(
                name: "cancellation_completed_at",
                schema: "hotels",
                table: "hotel_provider_booking_cache");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                schema: "hotels",
                table: "hotel_provider_booking_cache");

            migrationBuilder.DropColumn(
                name: "cancellation_requested_at",
                schema: "hotels",
                table: "hotel_provider_booking_cache");

            migrationBuilder.DropColumn(
                name: "external_unresolved_at",
                schema: "hotels",
                table: "hotel_booking_sagas");

            migrationBuilder.DropColumn(
                name: "provider_cancellation_completed_at",
                schema: "hotels",
                table: "hotel_booking_sagas");

            migrationBuilder.DropColumn(
                name: "provider_cancellation_idempotency_key",
                schema: "hotels",
                table: "hotel_booking_sagas");

            migrationBuilder.DropColumn(
                name: "provider_cancellation_reason",
                schema: "hotels",
                table: "hotel_booking_sagas");

            migrationBuilder.DropColumn(
                name: "provider_cancellation_requested_at",
                schema: "hotels",
                table: "hotel_booking_sagas");
        }
    }
}
