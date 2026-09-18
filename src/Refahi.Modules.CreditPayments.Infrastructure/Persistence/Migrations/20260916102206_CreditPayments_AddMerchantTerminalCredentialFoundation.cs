using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddMerchantTerminalCredentialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "credit_payments");

            migrationBuilder.CreateTable(
                name: "audit_records",
                schema: "credit_payments",
                columns: table => new
                {
                    audit_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.audit_record_id);
                });

            migrationBuilder.CreateTable(
                name: "merchants",
                schema: "credit_payments",
                columns: table => new
                {
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merchants", x => x.merchant_id);
                    table.CheckConstraint("ck_merchants_status", "status IN (1,2,3,4)");
                });

            migrationBuilder.CreateTable(
                name: "terminals",
                schema: "credit_payments",
                columns: table => new
                {
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    approved_category_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    min_payment_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    max_payment_amount_minor = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminals", x => x.terminal_id);
                    table.CheckConstraint("ck_terminals_amounts", "min_payment_amount_minor > 0 AND max_payment_amount_minor > 0 AND min_payment_amount_minor <= max_payment_amount_minor");
                    table.CheckConstraint("ck_terminals_status", "status IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_terminals_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalSchema: "credit_payments",
                        principalTable: "merchants",
                        principalColumn: "merchant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "terminal_credentials",
                schema: "credit_payments",
                columns: table => new
                {
                    credential_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    secret_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    secret_hash_algorithm = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    secret_hash_version = table.Column<int>(type: "integer", nullable: false),
                    scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_credentials", x => x.credential_id);
                    table.CheckConstraint("ck_credentials_expiration", "expires_at IS NULL OR expires_at > created_at");
                    table.CheckConstraint("ck_credentials_hash_version", "secret_hash_version > 0");
                    table.CheckConstraint("ck_credentials_status", "status IN (1,2)");
                    table.ForeignKey(
                        name: "FK_terminal_credentials_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalSchema: "credit_payments",
                        principalTable: "terminals",
                        principalColumn: "terminal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "terminal_return_urls",
                schema: "credit_payments",
                columns: table => new
                {
                    return_url_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canonical_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_return_urls", x => x.return_url_id);
                    table.ForeignKey(
                        name: "FK_terminal_return_urls_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalSchema: "credit_payments",
                        principalTable: "terminals",
                        principalColumn: "terminal_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_endpoints",
                schema: "credit_payments",
                columns: table => new
                {
                    webhook_endpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    signing_secret_ciphertext = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    signing_secret_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_endpoints", x => x.webhook_endpoint_id);
                    table.CheckConstraint("ck_webhooks_https", "url LIKE 'https://%'");
                    table.CheckConstraint("ck_webhooks_secret_version", "signing_secret_version > 0");
                    table.CheckConstraint("ck_webhooks_status", "status IN (1,2)");
                    table.ForeignKey(
                        name: "FK_webhook_endpoints_terminals_terminal_id",
                        column: x => x.terminal_id,
                        principalSchema: "credit_payments",
                        principalTable: "terminals",
                        principalColumn: "terminal_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_entity_type_entity_id_occurred_at",
                schema: "credit_payments",
                table: "audit_records",
                columns: new[] { "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_merchants_supplier_id",
                schema: "credit_payments",
                table: "merchants",
                column: "supplier_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terminal_credentials_client_id",
                schema: "credit_payments",
                table: "terminal_credentials",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terminal_credentials_terminal_id",
                schema: "credit_payments",
                table: "terminal_credentials",
                column: "terminal_id");

            migrationBuilder.CreateIndex(
                name: "IX_terminal_return_urls_terminal_id_canonical_url",
                schema: "credit_payments",
                table: "terminal_return_urls",
                columns: new[] { "terminal_id", "canonical_url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_terminals_merchant_id_name",
                schema: "credit_payments",
                table: "terminals",
                columns: new[] { "merchant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_endpoints_terminal_id",
                schema: "credit_payments",
                table: "webhook_endpoints",
                column: "terminal_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "terminal_credentials",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "terminal_return_urls",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "webhook_endpoints",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "terminals",
                schema: "credit_payments");

            migrationBuilder.DropTable(
                name: "merchants",
                schema: "credit_payments");
        }
    }
}
