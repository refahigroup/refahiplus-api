using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Store.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Store_RemoveUndeployedVendorPosModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS store.in_person_sales CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS store.vendor_pos_configurations CASCADE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // These models were never deployed. Rolling back must not recreate them.
            return;
            migrationBuilder.CreateTable(
                name: "in_person_sales",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommissionAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    GrossAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyKey = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    MobileNumber = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false
                    ),
                    NetAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OtpExpiresAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    OtpReference = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_in_person_sales", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "vendor_pos_configurations",
                schema: "store",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ShopId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_pos_configurations", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_in_person_sales_IdempotencyKey",
                schema: "store",
                table: "in_person_sales",
                column: "IdempotencyKey",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_in_person_sales_OrderId",
                schema: "store",
                table: "in_person_sales",
                column: "OrderId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_in_person_sales_SupplierId_CreatedAt",
                schema: "store",
                table: "in_person_sales",
                columns: new[] { "SupplierId", "CreatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_vendor_pos_configurations_ShopId",
                schema: "store",
                table: "vendor_pos_configurations",
                column: "ShopId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_vendor_pos_configurations_SupplierId_IsActive",
                schema: "store",
                table: "vendor_pos_configurations",
                columns: new[] { "SupplierId", "IsActive" }
            );
        }
    }
}
