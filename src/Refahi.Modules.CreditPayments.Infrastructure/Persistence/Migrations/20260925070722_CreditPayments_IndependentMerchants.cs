using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditPayments_IndependentMerchants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM credit_payments.merchants) THEN RAISE EXCEPTION 'Reset development CreditPayments data before applying IndependentMerchants'; END IF; END $$;");
            migrationBuilder.DropIndex(
                name: "IX_merchants_supplier_id",
                schema: "credit_payments",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "supplier_id",
                schema: "credit_payments",
                table: "merchants");

            migrationBuilder.AddColumn<short>(
                name: "entity_type",
                schema: "credit_payments",
                table: "merchants",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "legal_name",
                schema: "credit_payments",
                table: "merchants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "national_id",
                schema: "credit_payments",
                table: "merchants",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "settlement_wallet_id",
                schema: "credit_payments",
                table: "merchants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_merchants_entity_type_national_id",
                schema: "credit_payments",
                table: "merchants",
                columns: new[] { "entity_type", "national_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_merchants_entity_type",
                schema: "credit_payments",
                table: "merchants",
                sql: "entity_type IN (1,2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_merchants_entity_type_national_id",
                schema: "credit_payments",
                table: "merchants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_merchants_entity_type",
                schema: "credit_payments",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "entity_type",
                schema: "credit_payments",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "legal_name",
                schema: "credit_payments",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "national_id",
                schema: "credit_payments",
                table: "merchants");

            migrationBuilder.DropColumn(
                name: "settlement_wallet_id",
                schema: "credit_payments",
                table: "merchants");

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_id",
                schema: "credit_payments",
                table: "merchants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_merchants_supplier_id",
                schema: "credit_payments",
                table: "merchants",
                column: "supplier_id",
                unique: true);
        }
    }
}
