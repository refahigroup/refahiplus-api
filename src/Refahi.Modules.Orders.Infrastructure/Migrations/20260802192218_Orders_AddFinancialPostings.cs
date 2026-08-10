using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Orders_AddFinancialPostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "commission_amount_minor",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "commission_percent",
                schema: "orders",
                table: "orders",
                type: "numeric(7,4)",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "gross_amount_minor",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "recipient_net_amount_minor",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: true
            );

            migrationBuilder.AddColumn<long>(
                name: "vat_amount_minor",
                schema: "orders",
                table: "orders",
                type: "bigint",
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "vat_percent",
                schema: "orders",
                table: "orders",
                type: "numeric(7,4)",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "order_payment_postings",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<short>(type: "smallint", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    purpose = table.Column<string>(
                        type: "character varying(80)",
                        maxLength: 80,
                        nullable: false
                    ),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payment_postings", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_payment_postings_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "orders",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_postings_order_id_sort_order",
                schema: "orders",
                table: "order_payment_postings",
                columns: new[] { "order_id", "sort_order" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "order_payment_postings", schema: "orders");

            migrationBuilder.DropColumn(
                name: "commission_amount_minor",
                schema: "orders",
                table: "orders"
            );

            migrationBuilder.DropColumn(
                name: "commission_percent",
                schema: "orders",
                table: "orders"
            );

            migrationBuilder.DropColumn(
                name: "gross_amount_minor",
                schema: "orders",
                table: "orders"
            );

            migrationBuilder.DropColumn(
                name: "recipient_net_amount_minor",
                schema: "orders",
                table: "orders"
            );

            migrationBuilder.DropColumn(
                name: "vat_amount_minor",
                schema: "orders",
                table: "orders"
            );

            migrationBuilder.DropColumn(name: "vat_percent", schema: "orders", table: "orders");
        }
    }
}
