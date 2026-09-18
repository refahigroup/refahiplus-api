using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_AddOrderBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_checkout_sessions_auth_consistency",
                schema: "credit_payments",
                table: "checkout_sessions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "order_bound_at",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "order_id",
                schema: "credit_payments",
                table: "gateway_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_gateway_payments_order_id",
                schema: "credit_payments",
                table: "gateway_payments",
                column: "order_id",
                unique: true,
                filter: "\"order_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_gateway_payments_order_binding",
                schema: "credit_payments",
                table: "gateway_payments",
                sql: "(order_id IS NULL AND order_bound_at IS NULL) OR (order_id IS NOT NULL AND order_bound_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkout_sessions_auth_consistency",
                schema: "credit_payments",
                table: "checkout_sessions",
                sql: "(status = 1 AND customer_id IS NULL AND authenticated_at IS NULL AND authorization_secret_hash IS NULL AND authorization_expires_at IS NULL) OR (status IN (2,3,4) AND customer_id IS NOT NULL AND authenticated_at IS NOT NULL AND octet_length(authorization_secret_hash) = 32 AND authorization_expires_at IS NOT NULL AND authorization_expires_at <= expires_at) OR status IN (5,6,7)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_gateway_payments_order_id",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_gateway_payments_order_binding",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checkout_sessions_auth_consistency",
                schema: "credit_payments",
                table: "checkout_sessions");

            migrationBuilder.DropColumn(
                name: "order_bound_at",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.DropColumn(
                name: "order_id",
                schema: "credit_payments",
                table: "gateway_payments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_checkout_sessions_auth_consistency",
                schema: "credit_payments",
                table: "checkout_sessions",
                sql: "(status = 1 AND customer_id IS NULL AND authenticated_at IS NULL AND authorization_secret_hash IS NULL AND authorization_expires_at IS NULL) OR (status <> 1 AND ((status = 2 AND customer_id IS NOT NULL AND authenticated_at IS NOT NULL AND octet_length(authorization_secret_hash) = 32 AND authorization_expires_at IS NOT NULL AND authorization_expires_at <= expires_at) OR status IN (3,4,5,6,7)))");
        }
    }
}
