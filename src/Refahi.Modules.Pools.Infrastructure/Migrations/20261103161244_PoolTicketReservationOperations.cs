using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Pools.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PoolTicketReservationOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "poolticket_reservations",
                schema: "pools",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Mobile = table.Column<string>(type: "text", nullable: false),
                    ReservationId = table.Column<string>(type: "text", nullable: true),
                    VoucherCiphertext = table.Column<string>(type: "text", nullable: true),
                    Enter = table.Column<int>(type: "integer", nullable: true),
                    ErrorCode = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poolticket_reservations", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_poolticket_reservations_requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "pools",
                        principalTable: "requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_poolticket_reservations_ReservationId",
                schema: "pools",
                table: "poolticket_reservations",
                column: "ReservationId",
                unique: true,
                filter: "\"ReservationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poolticket_reservations",
                schema: "pools");
        }
    }
}
