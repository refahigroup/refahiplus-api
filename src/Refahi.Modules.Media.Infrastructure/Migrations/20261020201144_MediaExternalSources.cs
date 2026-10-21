using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Media.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MediaExternalSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_sources",
                schema: "media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    FileName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshAfter = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StateChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "external_source_origins",
                schema: "media",
                columns: table => new
                {
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_source_origins", x => new { x.SourceId, x.Module, x.Provider });
                    table.ForeignKey(
                        name: "FK_external_source_origins_external_sources_SourceId",
                        column: x => x.SourceId,
                        principalSchema: "media",
                        principalTable: "external_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_external_source_origins_Module_Provider",
                schema: "media",
                table: "external_source_origins",
                columns: new[] { "Module", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_external_source_origins_Provider",
                schema: "media",
                table: "external_source_origins",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_external_sources_ExpiresAt",
                schema: "media",
                table: "external_sources",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_external_sources_FileName",
                schema: "media",
                table: "external_sources",
                column: "FileName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_source_origins",
                schema: "media");

            migrationBuilder.DropTable(
                name: "external_sources",
                schema: "media");
        }
    }
}
