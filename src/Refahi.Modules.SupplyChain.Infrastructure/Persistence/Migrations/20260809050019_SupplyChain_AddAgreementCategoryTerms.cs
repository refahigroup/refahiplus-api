using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyChain_AddAgreementCategoryTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agreement_category_terms",
                schema: "supplychain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    AllowedSalesChannels = table.Column<short>(type: "smallint", nullable: false),
                    CommissionPercent = table.Column<decimal>(
                        type: "numeric(5,2)",
                        nullable: false
                    ),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agreement_category_terms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agreement_category_terms_agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalSchema: "supplychain",
                        principalTable: "agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_agreement_category_terms_AgreementId",
                schema: "supplychain",
                table: "agreement_category_terms",
                column: "AgreementId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_agreement_category_terms_CategoryId",
                schema: "supplychain",
                table: "agreement_category_terms",
                column: "CategoryId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_agreement_category_terms_effective_lookup",
                schema: "supplychain",
                table: "agreement_category_terms",
                columns: new[] { "CategoryId", "AllowedSalesChannels", "IsDeleted", "AgreementId" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_agreement_category_terms_IsDeleted",
                schema: "supplychain",
                table: "agreement_category_terms",
                column: "IsDeleted"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "agreement_category_terms", schema: "supplychain");
        }
    }
}
