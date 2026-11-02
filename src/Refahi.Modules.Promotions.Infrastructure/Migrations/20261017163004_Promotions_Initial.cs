using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Promotions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Promotions_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "promotions");

            migrationBuilder.CreateTable(
                name: "audit",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FixedAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    MaximumDiscountMinor = table.Column<long>(type: "bigint", nullable: false),
                    MinimumEligibleMinor = table.Column<long>(type: "bigint", nullable: false),
                    BudgetMinor = table.Column<long>(type: "bigint", nullable: false),
                    MaximumUses = table.Column<int>(type: "integer", nullable: false),
                    MaximumUsesPerUser = table.Column<int>(type: "integer", nullable: false),
                    SourceModules = table.Column<string[]>(type: "text[]", nullable: false),
                    CategoryPrefixes = table.Column<string[]>(type: "text[]", nullable: false),
                    OrganizationIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    UserIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "codes",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Ciphertext = table.Column<string>(type: "text", nullable: false),
                    Mask = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MaximumUses = table.Column<int>(type: "integer", nullable: false),
                    BatchKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_codes_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "promotions",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "redemptions",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscountMinor = table.Column<long>(type: "bigint", nullable: false),
                    RuleVersion = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_redemptions_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "promotions",
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_redemptions_codes_CodeId",
                        column: x => x.CodeId,
                        principalSchema: "promotions",
                        principalTable: "codes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "redemption_items",
                schema: "promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RedemptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_redemption_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_redemption_items_redemptions_RedemptionId",
                        column: x => x.RedemptionId,
                        principalSchema: "promotions",
                        principalTable: "redemptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_SubjectId",
                schema: "promotions",
                table: "audit",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_codes_CampaignId_BatchKey",
                schema: "promotions",
                table: "codes",
                columns: new[] { "CampaignId", "BatchKey" });

            migrationBuilder.CreateIndex(
                name: "IX_codes_Hash",
                schema: "promotions",
                table: "codes",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_redemption_items_RedemptionId_OrderItemId",
                schema: "promotions",
                table: "redemption_items",
                columns: new[] { "RedemptionId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_CampaignId_Status_UserId",
                schema: "promotions",
                table: "redemptions",
                columns: new[] { "CampaignId", "Status", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_CodeId",
                schema: "promotions",
                table: "redemptions",
                column: "CodeId");

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_OrderId",
                schema: "promotions",
                table: "redemptions",
                column: "OrderId",
                unique: true,
                filter: "\"Status\" IN (0,1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_redemptions_OrderId_IdempotencyKey",
                schema: "promotions",
                table: "redemptions",
                columns: new[] { "OrderId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "redemption_items",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "redemptions",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "codes",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "campaigns",
                schema: "promotions");
        }
    }
}
