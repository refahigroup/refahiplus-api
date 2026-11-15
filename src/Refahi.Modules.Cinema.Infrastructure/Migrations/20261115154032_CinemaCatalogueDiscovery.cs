using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Refahi.Modules.Cinema.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CinemaCatalogueDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "films",
                schema: "cinema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    Genres = table.Column<string[]>(type: "text[]", nullable: false),
                    SummaryText = table.Column<string>(type: "text", nullable: true),
                    SummaryHtml = table.Column<string>(type: "text", nullable: true),
                    DescriptionHtml = table.Column<string>(type: "text", nullable: true),
                    Poster = table.Column<string>(type: "text", nullable: true),
                    Cover = table.Column<string>(type: "text", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    AgeGroup = table.Column<string>(type: "text", nullable: true),
                    ReleaseYear = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_films", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iticket_discovery_state",
                schema: "cinema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Failed = table.Column<bool>(type: "boolean", nullable: false),
                    UnmappedVenues = table.Column<int>(type: "integer", nullable: false),
                    PendingMappings = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iticket_discovery_state", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iticket_geographies",
                schema: "cinema",
                columns: table => new
                {
                    ExternalCityId = table.Column<int>(type: "integer", nullable: false),
                    ExternalProvinceId = table.Column<int>(type: "integer", nullable: false),
                    CityId = table.Column<int>(type: "integer", nullable: true),
                    ProvinceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iticket_geographies", x => x.ExternalCityId);
                });

            migrationBuilder.CreateTable(
                name: "iticket_mappings",
                schema: "cinema",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    CanonicalId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iticket_mappings", x => new { x.Kind, x.ExternalId });
                });

            migrationBuilder.CreateTable(
                name: "people",
                schema: "cinema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    Portrait = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                schema: "cinema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Image = table.Column<string>(type: "text", nullable: true),
                    CityId = table.Column<int>(type: "integer", nullable: true),
                    CityName = table.Column<string>(type: "text", nullable: true),
                    ProvinceId = table.Column<int>(type: "integer", nullable: true),
                    ProvinceName = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "film_credits",
                schema: "cinema",
                columns: table => new
                {
                    FilmId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_film_credits", x => new { x.FilmId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_film_credits_films_FilmId",
                        column: x => x.FilmId,
                        principalSchema: "cinema",
                        principalTable: "films",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_film_credits_people_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "cinema",
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "iticket_screenings",
                schema: "cinema",
                columns: table => new
                {
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FilmId = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalFilmId = table.Column<string>(type: "text", nullable: false),
                    ExternalVenueId = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PurchaseEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PurchaseStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iticket_screenings", x => x.ExternalId);
                    table.ForeignKey(
                        name: "FK_iticket_screenings_films_FilmId",
                        column: x => x.FilmId,
                        principalSchema: "cinema",
                        principalTable: "films",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_iticket_screenings_venues_VenueId",
                        column: x => x.VenueId,
                        principalSchema: "cinema",
                        principalTable: "venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_film_credits_PersonId",
                schema: "cinema",
                table: "film_credits",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_iticket_screenings_FilmId_VenueId_StartsAt",
                schema: "cinema",
                table: "iticket_screenings",
                columns: new[] { "FilmId", "VenueId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_iticket_screenings_Id",
                schema: "cinema",
                table: "iticket_screenings",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iticket_screenings_Published_StartsAt",
                schema: "cinema",
                table: "iticket_screenings",
                columns: new[] { "Published", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_iticket_screenings_VenueId",
                schema: "cinema",
                table: "iticket_screenings",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_venues_CityId",
                schema: "cinema",
                table: "venues",
                column: "CityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "film_credits",
                schema: "cinema");

            migrationBuilder.DropTable(
                name: "iticket_discovery_state",
                schema: "cinema");

            migrationBuilder.DropTable(
                name: "iticket_geographies",
                schema: "cinema");

            migrationBuilder.DropTable(
                name: "iticket_mappings",
                schema: "cinema");

            migrationBuilder.DropTable(
                name: "iticket_screenings",
                schema: "cinema");

            migrationBuilder.DropTable(
                name: "people",
                schema: "cinema");

            migrationBuilder.DropTable(
                name: "films",
                schema: "cinema");

            migrationBuilder.DropTable(
                name: "venues",
                schema: "cinema");
        }
    }
}
