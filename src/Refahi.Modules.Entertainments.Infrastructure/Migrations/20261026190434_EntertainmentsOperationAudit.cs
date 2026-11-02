using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Entertainments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntertainmentsOperationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                schema: "entertainments",
                table: "touristpanel_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceProtected",
                schema: "entertainments",
                table: "touristpanel_operations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorId",
                schema: "entertainments",
                table: "touristpanel_operations");

            migrationBuilder.DropColumn(
                name: "EvidenceProtected",
                schema: "entertainments",
                table: "touristpanel_operations");
        }
    }
}
