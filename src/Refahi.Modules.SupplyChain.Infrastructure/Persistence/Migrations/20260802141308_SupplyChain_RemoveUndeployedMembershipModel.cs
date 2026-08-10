using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplyChain_RemoveUndeployedMembershipModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS supplychain.supplier_memberships CASCADE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This model was never deployed. Rolling back must not recreate it.
            return;
            migrationBuilder.CreateTable(
                name: "supplier_memberships",
                schema: "supplychain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Role = table.Column<short>(type: "smallint", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_memberships_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "supplychain",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_memberships_SupplierId_UserId",
                schema: "supplychain",
                table: "supplier_memberships",
                columns: new[] { "SupplierId", "UserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_memberships_UserId_IsActive",
                schema: "supplychain",
                table: "supplier_memberships",
                columns: new[] { "UserId", "IsActive" }
            );
        }
    }
}
