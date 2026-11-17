using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Refahi.Modules.Bus.Domain.Discovery;

namespace Refahi.Modules.Bus.Infrastructure.Persistence;

public sealed class BusDbContext(DbContextOptions<BusDbContext> options) : DbContext(options)
{
    public DbSet<BusCityMapping> Cities => Set<BusCityMapping>();
    public DbSet<BusOffer> Offers => Set<BusOffer>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("bus");
        var cities = model.Entity<BusCityMapping>();
        cities.ToTable("hiholiday_city_mappings");
        cities.HasKey(x => x.CityId);
        cities.Property(x => x.CityId).ValueGeneratedNever();
        cities.HasIndex(x => x.ProviderRegionId).IsUnique();

        var offers = model.Entity<BusOffer>();
        offers.ToTable("hiholiday_offers");
        offers.HasKey(x => x.Id);
        offers.HasIndex(x => x.ExpiresAt);
        offers.OwnsOne(x => x.Service);
        offers.Navigation(x => x.Service).IsRequired();
    }
}

public sealed class BusDbContextFactory : IDesignTimeDbContextFactory<BusDbContext>
{
    public BusDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<BusDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("REFAHI_DB_CONNECTION") ??
            "Host=localhost;Database=refahi;Username=postgres",
            options => options.MigrationsHistoryTable("__EFMigrationsHistory", "bus")).Options);
}

internal sealed class BusDiscoveryRepository(BusDbContext db) : IBusDiscoveryRepository
{
    public async Task<IReadOnlyList<BusCityMapping>> CitiesAsync(CancellationToken ct) =>
        await db.Cities.AsNoTracking().ToListAsync(ct);

    public async Task StoreOffersAsync(IReadOnlyList<BusOffer> offers, DateTimeOffset now, CancellationToken ct)
    {
        await db.Offers.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        db.Offers.AddRange(offers);
        await db.SaveChangesAsync(ct);
    }

    public Task<BusOffer?> GetOfferAsync(Guid id, CancellationToken ct) =>
        db.Offers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
}
