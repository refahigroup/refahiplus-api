using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrdersAddSagaCorrelationAndOutboxRetries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                schema: "orders",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "orders",
                table: "outbox_messages",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<Guid>(
                name: "saga_id",
                schema: "orders",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_occurred_at",
                schema: "orders",
                table: "outbox_messages",
                columns: new[] { "status", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_status_occurred_at",
                schema: "orders",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "retry_count",
                schema: "orders",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "orders",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "saga_id",
                schema: "orders",
                table: "orders");
        }
    }
}
