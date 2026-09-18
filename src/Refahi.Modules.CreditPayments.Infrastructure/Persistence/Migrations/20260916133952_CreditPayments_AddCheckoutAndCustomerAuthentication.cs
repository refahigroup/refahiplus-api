using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddCheckoutAndCustomerAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "checkout_sessions",
                schema: "credit_payments",
                columns: table => new
                {
                    checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    token_nonce = table.Column<byte[]>(type: "bytea", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    token_key_version = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authenticated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    authorization_secret_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    authorization_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkout_sessions", x => x.checkout_session_id);
                    table.CheckConstraint("ck_checkout_sessions_auth_consistency", "(status = 1 AND customer_id IS NULL AND authenticated_at IS NULL AND authorization_secret_hash IS NULL AND authorization_expires_at IS NULL) OR (status <> 1 AND ((status = 2 AND customer_id IS NOT NULL AND authenticated_at IS NOT NULL AND octet_length(authorization_secret_hash) = 32 AND authorization_expires_at IS NOT NULL AND authorization_expires_at <= expires_at) OR status IN (3,4,5,6,7)))");
                    table.CheckConstraint("ck_checkout_sessions_expiry", "expires_at > created_at");
                    table.CheckConstraint("ck_checkout_sessions_hash", "octet_length(token_hash) = 32");
                    table.CheckConstraint("ck_checkout_sessions_key_version", "token_key_version > 0");
                    table.CheckConstraint("ck_checkout_sessions_nonce", "octet_length(token_nonce) = 32");
                    table.CheckConstraint("ck_checkout_sessions_status", "status IN (1,2,3,4,5,6,7)");
                    table.CheckConstraint("ck_checkout_sessions_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_checkout_sessions_gateway_payments_gateway_payment_id",
                        column: x => x.gateway_payment_id,
                        principalSchema: "credit_payments",
                        principalTable: "gateway_payments",
                        principalColumn: "gateway_payment_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "checkout_otp_challenges",
                schema: "credit_payments",
                columns: table => new
                {
                    checkout_otp_challenge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    protected_reference_code = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    otp_type = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verify_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    send_sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkout_otp_challenges", x => x.checkout_otp_challenge_id);
                    table.CheckConstraint("ck_checkout_otp_attempts", "verify_attempt_count >= 0");
                    table.CheckConstraint("ck_checkout_otp_expiry", "expires_at > sent_at");
                    table.CheckConstraint("ck_checkout_otp_reference_consistency", "(status = 2 AND resolved_customer_id IS NOT NULL AND protected_reference_code IS NOT NULL) OR (status = 3 AND resolved_customer_id IS NULL AND protected_reference_code IS NULL) OR status IN (1,4,5,6,7)");
                    table.CheckConstraint("ck_checkout_otp_sequence", "send_sequence > 0");
                    table.CheckConstraint("ck_checkout_otp_status", "status IN (1,2,3,4,5,6,7)");
                    table.ForeignKey(
                        name: "FK_checkout_otp_challenges_checkout_sessions_checkout_session_~",
                        column: x => x.checkout_session_id,
                        principalSchema: "credit_payments",
                        principalTable: "checkout_sessions",
                        principalColumn: "checkout_session_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_checkout_otp_active_session",
                schema: "credit_payments",
                table: "checkout_otp_challenges",
                column: "checkout_session_id",
                unique: true,
                filter: "\"status\" IN (2,3)");

            migrationBuilder.CreateIndex(
                name: "ux_checkout_otp_session_sequence",
                schema: "credit_payments",
                table: "checkout_otp_challenges",
                columns: new[] { "checkout_session_id", "send_sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_checkout_sessions_active_payment",
                schema: "credit_payments",
                table: "checkout_sessions",
                column: "gateway_payment_id",
                unique: true,
                filter: "\"status\" IN (1,2,3,4)");

            migrationBuilder.CreateIndex(
                name: "ux_checkout_sessions_token_hash",
                schema: "credit_payments",
                table: "checkout_sessions",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkout_otp_challenges",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "checkout_sessions",
                schema: "credit_payments");

            migrationBuilder.DropColumn(
                name: "customer_id",
                schema: "credit_payments",
                table: "gateway_payments");
        }
    }
}
