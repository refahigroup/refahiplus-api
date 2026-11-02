using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Refahi.Modules.Catalogue.Infrastructure.Migrations;
[DbContext(typeof(CatalogueDbContext))]
[Migration("20261022000100_CatalogueInitial")]
public sealed class CatalogueInitial:Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)=>migrationBuilder.EnsureSchema("catalogue");
 protected override void Down(MigrationBuilder migrationBuilder) { }
 protected override void BuildTargetModel(ModelBuilder modelBuilder)=>modelBuilder.HasDefaultSchema("catalogue");
}
