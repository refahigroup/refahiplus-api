using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Refahi.Modules.Catalogue.Infrastructure.Migrations;
[DbContext(typeof(CatalogueDbContext))]
public sealed class CatalogueDbContextModelSnapshot:ModelSnapshot
{protected override void BuildModel(ModelBuilder modelBuilder)=>modelBuilder.HasDefaultSchema("catalogue");}
