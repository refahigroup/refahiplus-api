using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Hotels.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsCompleteStaticContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s';");
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastContentSuccessAtUtc",
                schema: "hotels",
                table: "snapptrip_discovery_sync_states",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextContentAttemptAtUtc",
                schema: "hotels",
                table: "snapptrip_discovery_sync_states",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccommodationType",
                schema: "hotels",
                table: "snapptrip_discovery_hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentJson",
                schema: "hotels",
                table: "snapptrip_discovery_hotels",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContentReady",
                schema: "hotels",
                table: "snapptrip_discovery_hotels",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "FacilityKeys",
                schema: "hotels",
                table: "snapptrip_discovery_hotels",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                schema: "hotels",
                table: "snapptrip_discovery_hotels",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastContentSuccessAtUtc",
                schema: "hotels",
                table: "lamaso_discovery_sync_states",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextContentAttemptAtUtc",
                schema: "hotels",
                table: "lamaso_discovery_sync_states",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccommodationType",
                schema: "hotels",
                table: "lamaso_discovery_hotels",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentJson",
                schema: "hotels",
                table: "lamaso_discovery_hotels",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContentReady",
                schema: "hotels",
                table: "lamaso_discovery_hotels",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "FacilityKeys",
                schema: "hotels",
                table: "lamaso_discovery_hotels",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                schema: "hotels",
                table: "lamaso_discovery_hotels",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastContentSuccessAtUtc",
                schema: "hotels",
                table: "snapptrip_discovery_sync_states");

            migrationBuilder.DropColumn(
                name: "NextContentAttemptAtUtc",
                schema: "hotels",
                table: "snapptrip_discovery_sync_states");

            migrationBuilder.DropColumn(
                name: "AccommodationType",
                schema: "hotels",
                table: "snapptrip_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "ContentJson",
                schema: "hotels",
                table: "snapptrip_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "ContentReady",
                schema: "hotels",
                table: "snapptrip_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "FacilityKeys",
                schema: "hotels",
                table: "snapptrip_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "Rating",
                schema: "hotels",
                table: "snapptrip_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "LastContentSuccessAtUtc",
                schema: "hotels",
                table: "lamaso_discovery_sync_states");

            migrationBuilder.DropColumn(
                name: "NextContentAttemptAtUtc",
                schema: "hotels",
                table: "lamaso_discovery_sync_states");

            migrationBuilder.DropColumn(
                name: "AccommodationType",
                schema: "hotels",
                table: "lamaso_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "ContentJson",
                schema: "hotels",
                table: "lamaso_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "ContentReady",
                schema: "hotels",
                table: "lamaso_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "FacilityKeys",
                schema: "hotels",
                table: "lamaso_discovery_hotels");

            migrationBuilder.DropColumn(
                name: "Rating",
                schema: "hotels",
                table: "lamaso_discovery_hotels");
        }
    }
}
