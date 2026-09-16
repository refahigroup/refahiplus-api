using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.PaymentGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentGatewayFundingSafetyRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "initiation_idempotency_key",
                schema: "payment_gateway",
                table: "sessions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "initiation_request_hash",
                schema: "payment_gateway",
                table: "sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "initiation_request_hash_version",
                schema: "payment_gateway",
                table: "sessions",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_recovery_at",
                schema: "payment_gateway",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_recovery_at",
                schema: "payment_gateway",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_reversal_at",
                schema: "payment_gateway",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processing_lease_until",
                schema: "payment_gateway",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_attempt_id",
                schema: "payment_gateway",
                table: "sessions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_transaction_id",
                schema: "payment_gateway",
                table: "sessions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recovery_attempt_count",
                schema: "payment_gateway",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "reversal_attempt_count",
                schema: "payment_gateway",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "reversal_description",
                schema: "payment_gateway",
                table: "sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "reversal_status",
                schema: "payment_gateway",
                table: "sessions",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<long>(
                name: "version",
                schema: "payment_gateway",
                table: "sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Deterministic legacy backfill only. Jibit switching URLs are not attempt identity.
            migrationBuilder.Sql(
                """
                UPDATE payment_gateway.sessions s
                SET provider_transaction_id = provider_ref_num
                WHERE provider_ref_num IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM payment_gateway.sessions other
                    WHERE other.session_id <> s.session_id
                      AND other.provider = s.provider
                      AND other.provider_ref_num = s.provider_ref_num);

                UPDATE payment_gateway.sessions s
                SET provider_attempt_id = provider_token
                WHERE provider = 1 AND provider_token IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM payment_gateway.sessions other
                    WHERE other.session_id <> s.session_id
                      AND other.provider = s.provider
                      AND other.provider_token = s.provider_token);
                """);

            migrationBuilder.CreateIndex(
                name: "idx_pg_sessions_recovery",
                schema: "payment_gateway",
                table: "sessions",
                columns: new[] { "status", "next_recovery_at" });

            migrationBuilder.CreateIndex(
                name: "idx_pg_sessions_reversal",
                schema: "payment_gateway",
                table: "sessions",
                columns: new[] { "reversal_status", "next_reversal_at" });

            migrationBuilder.CreateIndex(
                name: "uq_pg_sessions_provider_attempt",
                schema: "payment_gateway",
                table: "sessions",
                columns: new[] { "provider", "provider_attempt_id" },
                unique: true,
                filter: "provider_attempt_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_pg_sessions_provider_transaction",
                schema: "payment_gateway",
                table: "sessions",
                columns: new[] { "provider", "provider_transaction_id" },
                unique: true,
                filter: "provider_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_pg_sessions_topup_ledger",
                schema: "payment_gateway",
                table: "sessions",
                column: "topup_ledger_entry_id",
                unique: true,
                filter: "topup_ledger_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_pg_sessions_user_idempotency",
                schema: "payment_gateway",
                table: "sessions",
                columns: new[] { "user_id", "initiation_idempotency_key" },
                unique: true,
                filter: "initiation_idempotency_key IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pg_sessions_amount_positive",
                schema: "payment_gateway",
                table: "sessions",
                sql: "amount_minor > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pg_sessions_currency",
                schema: "payment_gateway",
                table: "sessions",
                sql: "length(currency) = 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pg_sessions_initiation_hash_pair",
                schema: "payment_gateway",
                table: "sessions",
                sql: "(initiation_request_hash IS NULL) = (initiation_request_hash_version IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pg_sessions_provider",
                schema: "payment_gateway",
                table: "sessions",
                sql: "provider IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pg_sessions_reversal_status",
                schema: "payment_gateway",
                table: "sessions",
                sql: "reversal_status BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pg_sessions_status",
                schema: "payment_gateway",
                table: "sessions",
                sql: "status BETWEEN 1 AND 9");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM payment_gateway.sessions
                        WHERE initiation_request_hash IS NOT NULL
                           OR status IN (8, 9)
                           OR reversal_status <> 0
                    ) THEN
                        RAISE EXCEPTION 'PaymentGateway funding-safety migration is forward-only after new funding data exists.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "idx_pg_sessions_recovery",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "idx_pg_sessions_reversal",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "uq_pg_sessions_provider_attempt",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "uq_pg_sessions_provider_transaction",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "uq_pg_sessions_topup_ledger",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropIndex(
                name: "uq_pg_sessions_user_idempotency",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pg_sessions_amount_positive",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pg_sessions_currency",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pg_sessions_initiation_hash_pair",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pg_sessions_provider",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pg_sessions_reversal_status",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pg_sessions_status",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "initiation_idempotency_key",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "initiation_request_hash",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "initiation_request_hash_version",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "last_recovery_at",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "next_recovery_at",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "next_reversal_at",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "processing_lease_until",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "provider_attempt_id",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "provider_transaction_id",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "recovery_attempt_count",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "reversal_attempt_count",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "reversal_description",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "reversal_status",
                schema: "payment_gateway",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "version",
                schema: "payment_gateway",
                table: "sessions");
        }
    }
}
