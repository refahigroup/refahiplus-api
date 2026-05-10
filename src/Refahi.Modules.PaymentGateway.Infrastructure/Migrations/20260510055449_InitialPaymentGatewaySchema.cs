using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.PaymentGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPaymentGatewaySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payment_gateway");

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "payment_gateway",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    provider = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    return_base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    succeeded_callback_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failed_callback_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    provider_token = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    provider_ref_num = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_trace_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_secure_pan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    provider_raw_callback = table.Column<string>(type: "jsonb", nullable: true),
                    provider_result_code = table.Column<int>(type: "integer", nullable: true),
                    provider_result_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    initiated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    topup_ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.session_id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_pg_sessions_initiated_at",
                schema: "payment_gateway",
                table: "sessions",
                column: "initiated_at");

            migrationBuilder.CreateIndex(
                name: "idx_pg_sessions_status",
                schema: "payment_gateway",
                table: "sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_pg_sessions_user",
                schema: "payment_gateway",
                table: "sessions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sessions",
                schema: "payment_gateway");
        }
    }
}
