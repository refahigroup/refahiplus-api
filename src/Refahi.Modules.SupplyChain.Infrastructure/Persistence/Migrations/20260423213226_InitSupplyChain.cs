using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitSupplyChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "supplychain");

            migrationBuilder.CreateTable(
                name: "suppliers",
                schema: "supplychain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BrandName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NationalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EconomicCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ProvinceId = table.Column<int>(type: "integer", nullable: true),
                    CityId = table.Column<int>(type: "integer", nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RepresentativeName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RepresentativePhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_attachments",
                schema: "supplychain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_attachments_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "supplychain",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_links",
                schema: "supplychain",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_supplier_links_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "supplychain",
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_attachments_SupplierId",
                schema: "supplychain",
                table: "supplier_attachments",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_links_SupplierId",
                schema: "supplychain",
                table: "supplier_links",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_NationalId",
                schema: "supplychain",
                table: "suppliers",
                column: "NationalId",
                unique: true,
                filter: "\"NationalId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_ProvinceId_CityId",
                schema: "supplychain",
                table: "suppliers",
                columns: new[] { "ProvinceId", "CityId" });

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_Status_IsDeleted",
                schema: "supplychain",
                table: "suppliers",
                columns: new[] { "Status", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_attachments",
                schema: "supplychain");

            migrationBuilder.DropTable(
                name: "supplier_links",
                schema: "supplychain");

            migrationBuilder.DropTable(
                name: "suppliers",
                schema: "supplychain");
        }
    }
}
