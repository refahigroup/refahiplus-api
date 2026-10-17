using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;

public sealed class PoolsDbContext(DbContextOptions<PoolsDbContext> options) : DbContext(options)
{
    public const string Schema = "pools";
    public DbSet<PoolBooking> Bookings => Set<PoolBooking>();
    public DbSet<PoolCatalogSnapshot> Catalog => Set<PoolCatalogSnapshot>();
    public DbSet<PoolPurchase> Purchases => Set<PoolPurchase>();
    public DbSet<PoolRequest> Requests => Set<PoolRequest>();
    public DbSet<PoolCatalogEntry> Venues => Set<PoolCatalogEntry>();
    public DbSet<PoolSessionEntry> Sessions => Set<PoolSessionEntry>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        var b = model.Entity<PoolBooking>(); b.ToTable("bookings"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ProviderKey).HasMaxLength(32); b.Property(x => x.IdempotencyKey).HasMaxLength(128);
        b.Property(x => x.Version).IsConcurrencyToken(); b.Ignore(x => x.HasTicket);
        b.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => x.OrderId).IsUnique().HasFilter("\"OrderId\" IS NOT NULL");
        b.HasIndex(x => new { x.ProviderKey, x.ProviderReference }).IsUnique().HasFilter("\"ProviderReference\" IS NOT NULL");
        b.HasIndex(x => new { x.CreatedAt, x.Id });
        b.HasMany(x => x.Attempts).WithOne().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
        var a = model.Entity<PoolProviderAttempt>(); a.ToTable("provider_attempts"); a.HasKey(x => x.Id);
        a.Property(x => x.Id).ValueGeneratedNever();
        var c = model.Entity<PoolCatalogSnapshot>(); c.ToTable("catalog_snapshots"); c.HasKey(x => new { x.ProviderKey, x.ExternalId });
        c.Property(x => x.ProviderKey).HasMaxLength(32); c.Property(x => x.ExternalId).HasMaxLength(128);
        var purchase = model.Entity<PoolPurchase>(); purchase.ToTable("purchases"); purchase.HasKey(x => x.Id);
        purchase.Property(x => x.Id).ValueGeneratedNever(); purchase.Property(x => x.Version).IsConcurrencyToken();
        purchase.Property(x => x.IdempotencyKey).HasMaxLength(128);
        purchase.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        purchase.HasIndex(x => x.OrderId).IsUnique().HasFilter("\"OrderId\" IS NOT NULL");
        purchase.HasIndex(x => new { x.CreatedAt, x.Id });
        purchase.HasMany(x => x.Requests).WithOne().HasForeignKey(x => x.PurchaseId);
        purchase.Navigation(x => x.Requests).UsePropertyAccessMode(PropertyAccessMode.Field);
        var request = model.Entity<PoolRequest>(); request.ToTable("requests"); request.HasKey(x => x.Id);
        request.Property(x => x.Id).ValueGeneratedNever();
        request.HasIndex(x => x.CodeHash).IsUnique().HasFilter("\"CodeHash\" IS NOT NULL");
        request.HasIndex(x => new { x.RedeemedBy, x.RedemptionKey }).IsUnique().HasFilter("\"RedemptionKey\" IS NOT NULL");
        var venue = model.Entity<PoolCatalogEntry>(); venue.ToTable("venues"); venue.HasKey(x => x.Id);
        venue.Property(x => x.Id).ValueGeneratedNever(); venue.HasIndex(x => new { x.ProviderKey, x.ExternalId }).IsUnique();
        venue.HasIndex(x => new { x.IsActive, x.State, x.City, x.Name });
        var session = model.Entity<PoolSessionEntry>(); session.ToTable("sessions"); session.HasKey(x => x.Id);
        session.Property(x => x.Id).ValueGeneratedNever();
        session.HasIndex(x => new { x.PoolId, x.ExternalGroupId, x.ExternalRowId }).IsUnique();
        session.HasIndex(x => new { x.IsActive, x.StartsAtUtc, x.Id });
        session.HasOne<PoolCatalogEntry>().WithMany().HasForeignKey(x => x.PoolId);

    }
}
public sealed class PoolCatalogSnapshot
{
    private PoolCatalogSnapshot() { }
    public string ProviderKey { get; private set; } = "";
    public string ExternalId { get; private set; } = "";
    public string Json { get; private set; } = "";
    public DateTimeOffset FetchedAt { get; private set; }
}
