using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddWalletFundingContinuation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "funding_continuations",
                schema: "credit_payments",
                columns: table => new
                {
                    funding_continuation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    personal_wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_deficit_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    client_reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wallet_funding_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    auto_resume_consent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    funding_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_continuations", x => x.funding_continuation_id);
                    table.CheckConstraint("ck_funding_continuations_amount", "required_deficit_minor > 0");
                    table.CheckConstraint("ck_funding_continuations_completion", "(status IN (1,2,3) AND completed_at IS NULL) OR (status IN (4,5,6) AND completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_funding_continuations_consent", "auto_resume_consent_at IS NOT NULL");
                    table.CheckConstraint("ck_funding_continuations_currency", "currency = 'IRR'");
                    table.CheckConstraint("ck_funding_continuations_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
                    table.CheckConstraint("ck_funding_continuations_retry", "retry_count >= 0");
                    table.CheckConstraint("ck_funding_continuations_status", "status IN (1,2,3,4,5,6)");
                    table.CheckConstraint("ck_funding_continuations_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_funding_continuations_checkout_sessions_checkout_session_id",
                        column: x => x.checkout_session_id,
                        principalSchema: "credit_payments",
                        principalTable: "checkout_sessions",
                        principalColumn: "checkout_session_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_funding_continuations_gateway_payments_gateway_payment_id",
                        column: x => x.gateway_payment_id,
                        principalSchema: "credit_payments",
                        principalTable: "gateway_payments",
                        principalColumn: "gateway_payment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_funding_continuations_checkout_session_id",
                schema: "credit_payments",
                table: "funding_continuations",
                column: "checkout_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_funding_continuations_recovery",
                schema: "credit_payments",
                table: "funding_continuations",
                columns: new[] { "status", "next_retry_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "ux_funding_continuations_active_payment",
                schema: "credit_payments",
                table: "funding_continuations",
                column: "gateway_payment_id",
                unique: true,
                filter: "\"status\" IN (1,2,3)");

            migrationBuilder.CreateIndex(
                name: "ux_funding_continuations_client_reference",
                schema: "credit_payments",
                table: "funding_continuations",
                column: "client_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_funding_continuations_wallet_funding_session",
                schema: "credit_payments",
                table: "funding_continuations",
                column: "wallet_funding_session_id",
                unique: true,
                filter: "\"wallet_funding_session_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funding_continuations",
                schema: "credit_payments");
        }
    }
}
