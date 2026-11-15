using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;

public sealed class PoolsDbContext(DbContextOptions<PoolsDbContext> options) : DbContext(options)
{
    public const string Schema = "pools";
    public DbSet<AabsarCart> AabsarCarts => Set<AabsarCart>();
    public DbSet<AabsarFulfillment> AabsarFulfillments => Set<AabsarFulfillment>();
    public DbSet<AabsarVenue> AabsarVenues => Set<AabsarVenue>();
    public DbSet<AabsarProduct> AabsarProducts => Set<AabsarProduct>();
    public DbSet<AabsarShowtime> AabsarShowtimes => Set<AabsarShowtime>();
    public DbSet<PoolBooking> Bookings => Set<PoolBooking>();
    public DbSet<PoolCatalogSnapshot> Catalog => Set<PoolCatalogSnapshot>();
    public DbSet<PoolPurchase> Purchases => Set<PoolPurchase>();
    public DbSet<PoolRequest> Requests => Set<PoolRequest>();
    public DbSet<PoolCatalogEntry> Venues => Set<PoolCatalogEntry>();
    public DbSet<PoolSessionEntry> Sessions => Set<PoolSessionEntry>();
    public DbSet<PoolTicketGeography> PoolTicketGeographies => Set<PoolTicketGeography>();
    public DbSet<PoolTicketContent> PoolTicketContents => Set<PoolTicketContent>();
    public DbSet<PoolTicketSyncState> PoolTicketSyncStates => Set<PoolTicketSyncState>();
    public DbSet<PoolTicketReservation> PoolTicketReservations => Set<PoolTicketReservation>();
    public DbSet<PoolTicketProvince> PoolTicketProvinces => Set<PoolTicketProvince>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        var cart = model.Entity<AabsarCart>();
        cart.ToTable("aabsar_carts");
        cart.Property(x => x.Id).ValueGeneratedNever();
        cart.Property(x => x.Version).IsRowVersion();
        cart.HasIndex(x => x.UserId).IsUnique();
        cart.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.CartId);
        cart.Navigation(x => x.Items).HasField("_items");
        var cartItem = model.Entity<AabsarCartItem>();
        cartItem.ToTable("aabsar_cart_items");
        cartItem.Property(x => x.Id).ValueGeneratedNever();
        cartItem.HasIndex(x => new { x.CartId, x.OfferKey, x.PurchaseOptionKey }).IsUnique();
        var fulfillment = model.Entity<AabsarFulfillment>();
        fulfillment.ToTable("aabsar_fulfillments").HasKey(x => x.PurchaseId);
        fulfillment.HasOne<PoolPurchase>().WithOne().HasForeignKey<AabsarFulfillment>(x => x.PurchaseId);
        model.Entity<AabsarVenue>().ToTable("aabsar_venues").Property(x => x.Id).ValueGeneratedNever();
        var product = model.Entity<AabsarProduct>();
        product.ToTable("aabsar_products");
        product.Property(x => x.Id).ValueGeneratedNever();
        product.Property(x => x.EventId).HasMaxLength(300);
        product.HasIndex(x => x.EventId).IsUnique();
        var showtime = model.Entity<AabsarShowtime>();
        showtime.ToTable("aabsar_showtimes");
        showtime.Property(x => x.Id).ValueGeneratedNever();
        showtime.Property(x => x.ShowtimeId).HasMaxLength(300);
        showtime.HasIndex(x => x.ShowtimeId).IsUnique();
        showtime.HasOne<AabsarProduct>().WithMany().HasForeignKey(x => x.ProductId);
        var reservation = model.Entity<PoolTicketReservation>(); reservation.ToTable("poolticket_reservations"); reservation.HasKey(x => x.RequestId);
        reservation.HasOne<PoolRequest>().WithOne().HasForeignKey<PoolTicketReservation>(x => x.RequestId);
        reservation.HasIndex(x => x.ReservationId).IsUnique().HasFilter("\"ReservationId\" IS NOT NULL");
        var province = model.Entity<PoolTicketProvince>(); province.ToTable("poolticket_provinces"); province.HasKey(x => x.StateId);
        var geo = model.Entity<PoolTicketGeography>(); geo.ToTable("poolticket_geographies"); geo.HasKey(x => x.CityId);
        geo.HasIndex(x => new { x.ReferenceProvinceId, x.ReferenceCityId });
        var content = model.Entity<PoolTicketContent>(); content.ToTable("poolticket_contents"); content.HasKey(x => x.VenueId);
        content.HasOne<PoolCatalogEntry>().WithOne().HasForeignKey<PoolTicketContent>(x => x.VenueId);
        var sync = model.Entity<PoolTicketSyncState>(); sync.ToTable("poolticket_sync_states"); sync.HasKey(x => x.Key);
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
