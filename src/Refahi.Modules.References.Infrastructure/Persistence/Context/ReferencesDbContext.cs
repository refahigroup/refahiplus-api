using Microsoft.EntityFrameworkCore;
using Refahi.Modules.References.Domain.Entities;
using Refahi.Modules.References.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.References.Infrastructure.Persistence.Context;

public class ReferencesDbContext : DbContext
{
    public ReferencesDbContext(DbContextOptions<ReferencesDbContext> options) : base(options) { }

    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<City> Cities => Set<City>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("references");

        modelBuilder.ApplyConfiguration(new ProvinceConfiguration());
        modelBuilder.ApplyConfiguration(new CityConfiguration());
    }
}
