using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Entertainments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntertainmentPublicContentAndDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SortTitle",
                schema: "entertainments",
                table: "touristpanel_programs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "DeliveryNeedsReview",
                schema: "entertainments",
                table: "touristpanel_issuances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "touristpanel_suppliers",
                schema: "entertainments",
                columns: table => new
                {
                    SupplierKey = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountKey = table.Column<string>(type: "text", nullable: false),
                    NativeId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_touristpanel_suppliers", x => x.SupplierKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_touristpanel_suppliers_AccountKey_NativeId",
                schema: "entertainments",
                table: "touristpanel_suppliers",
                columns: new[] { "AccountKey", "NativeId" },
                unique: true);
            // Additive backfill: identities survive refreshes; no financial or encrypted snapshots are rewritten.
            migrationBuilder.Sql("""
                INSERT INTO entertainments.touristpanel_suppliers ("SupplierKey", "AccountKey", "NativeId")
                SELECT gen_random_uuid(), "AccountKey", "SupplierId"
                FROM entertainments.touristpanel_programs
                GROUP BY "AccountKey", "SupplierId"
                ON CONFLICT ("AccountKey", "NativeId") DO NOTHING;
                UPDATE entertainments.touristpanel_programs
                SET "SortTitle" = btrim(regexp_replace(translate(lower(normalize("Title", NFKC)),
                    'يك‌۰۱۲۳۴۵۶۷۸۹٠١٢٣٤٥٦٧٨٩', 'یک 01234567890123456789'), '\s+', ' ', 'g')),
                    "NextDetailAttemptUtc" = LEAST("NextDetailAttemptUtc", CURRENT_TIMESTAMP);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "touristpanel_suppliers",
                schema: "entertainments");

            migrationBuilder.DropColumn(
                name: "SortTitle",
                schema: "entertainments",
                table: "touristpanel_programs");

            migrationBuilder.DropColumn(
                name: "DeliveryNeedsReview",
                schema: "entertainments",
                table: "touristpanel_issuances");
        }
    }
}
