using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Refahi.Modules.References.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReferencesInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "references");

            migrationBuilder.CreateTable(
                name: "provinces",
                schema: "references",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provinces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                schema: "references",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProvinceId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cities_provinces_ProvinceId",
                        column: x => x.ProvinceId,
                        principalSchema: "references",
                        principalTable: "provinces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cities_ProvinceId",
                schema: "references",
                table: "cities",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_cities_Slug",
                schema: "references",
                table: "cities",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cities_SortOrder",
                schema: "references",
                table: "cities",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_provinces_Slug",
                schema: "references",
                table: "provinces",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provinces_SortOrder",
                schema: "references",
                table: "provinces",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cities",
                schema: "references");

            migrationBuilder.DropTable(
                name: "provinces",
                schema: "references");
        }
    }
}
