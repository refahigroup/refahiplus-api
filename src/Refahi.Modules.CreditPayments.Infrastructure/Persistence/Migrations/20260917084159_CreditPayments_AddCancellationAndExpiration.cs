using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddCancellationAndExpiration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_terminations",
                schema: "credit_payments",
                columns: table => new
                {
                    payment_termination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    release_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_terminations", x => x.payment_termination_id);
                    table.CheckConstraint("ck_payment_terminations_kind", "kind IN (1,2)");
                    table.CheckConstraint("ck_payment_terminations_reason", "reason IS NULL OR char_length(reason) <= 250");
                    table.CheckConstraint("ck_payment_terminations_resolution", "(status IN (1,2) AND resolved_at IS NULL) OR (status IN (3,4) AND resolved_at IS NOT NULL)");
                    table.CheckConstraint("ck_payment_terminations_retry", "retry_count >= 0");
                    table.CheckConstraint("ck_payment_terminations_source", "source IN (1,2,3)");
                    table.CheckConstraint("ck_payment_terminations_status", "status IN (1,2,3,4)");
                    table.CheckConstraint("ck_payment_terminations_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_payment_terminations_gateway_payments_gateway_payment_id",
                        column: x => x.gateway_payment_id,
                        principalSchema: "credit_payments",
                        principalTable: "gateway_payments",
                        principalColumn: "gateway_payment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gateway_payments_expiration",
                schema: "credit_payments",
                table: "gateway_payments",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_payment_terminations_recovery",
                schema: "credit_payments",
                table: "payment_terminations",
                columns: new[] { "status", "next_retry_at" });

            migrationBuilder.CreateIndex(
                name: "ux_payment_terminations_payment",
                schema: "credit_payments",
                table: "payment_terminations",
                column: "gateway_payment_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_terminations",
                schema: "credit_payments");

            migrationBuilder.DropIndex(
                name: "ix_gateway_payments_expiration",
                schema: "credit_payments",
                table: "gateway_payments");
        }
    }
}
