using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Refahi.Modules.Catalogue.Infrastructure;
public sealed class CatalogueDbContext(DbContextOptions<CatalogueDbContext> options):DbContext(options)
{protected override void OnModelCreating(ModelBuilder model)=>model.HasDefaultSchema("catalogue");}
public sealed class CatalogueDbContextFactory:IDesignTimeDbContextFactory<CatalogueDbContext>
{public CatalogueDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<CatalogueDbContext>().UseNpgsql("").Options);}
