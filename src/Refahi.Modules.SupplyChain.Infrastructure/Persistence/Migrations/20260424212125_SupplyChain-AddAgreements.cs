using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyChainAddAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_suppliers_NationalId",
                schema: "supplychain",
                table: "suppliers");

            migrationBuilder.CreateTable(
                name: "agreements",
                schema: "supplychain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AgreementType = table.Column<short>(type: "smallint", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ToDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agreements_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "supplychain",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agreement_products",
                schema: "supplychain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    DiscountedPrice = table.Column<long>(type: "bigint", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CommissionPrice = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreement_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agreement_products_agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalSchema: "supplychain",
                        principalTable: "agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_NationalId",
                schema: "supplychain",
                table: "suppliers",
                column: "NationalId",
                unique: true,
                filter: "\"NationalId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_agreement_products_AgreementId",
                schema: "supplychain",
                table: "agreement_products",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_agreement_products_AgreementId_IsDeleted",
                schema: "supplychain",
                table: "agreement_products",
                columns: new[] { "AgreementId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_agreements_AgreementNo",
                schema: "supplychain",
                table: "agreements",
                column: "AgreementNo",
                unique: true,
                filter: "\"AgreementNo\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_agreements_Status_IsDeleted",
                schema: "supplychain",
                table: "agreements",
                columns: new[] { "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_agreements_SupplierId_Status",
                schema: "supplychain",
                table: "agreements",
                columns: new[] { "SupplierId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agreement_products",
                schema: "supplychain");

            migrationBuilder.DropTable(
                name: "agreements",
                schema: "supplychain");

            migrationBuilder.DropIndex(
                name: "IX_suppliers_NationalId",
                schema: "supplychain",
                table: "suppliers");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_NationalId",
                schema: "supplychain",
                table: "suppliers",
                column: "NationalId",
                unique: true,
                filter: "national_id IS NOT NULL AND is_deleted = false");
        }
    }
}
