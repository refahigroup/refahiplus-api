using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;

public sealed class PoolsDbContext(DbContextOptions<PoolsDbContext> options) : DbContext(options)
{
    public const string Schema = "pools";
    public DbSet<PoolBooking> Bookings => Set<PoolBooking>();
    public DbSet<PoolCatalogSnapshot> Catalog => Set<PoolCatalogSnapshot>();
    public DbSet<MockPoolReservation> MockReservations => Set<MockPoolReservation>();
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
        var m = model.Entity<MockPoolReservation>(); m.ToTable("mock_reservations"); m.HasKey(x => x.Reference);
        m.Property(x => x.Reference).HasMaxLength(128); m.HasIndex(x => new { x.ProviderKey, x.OperationId }).IsUnique();
        m.Property(x => x.Version).IsConcurrencyToken();
    }
}
public sealed class PoolCatalogSnapshot
{
    private PoolCatalogSnapshot() { }
    public string ProviderKey { get; private set; } = "";
    public string ExternalId { get; private set; } = "";
    public string Json { get; private set; } = "";
    public DateTimeOffset FetchedAt { get; private set; }
    public static PoolCatalogSnapshot Create(PoolSummary s) { var c = new PoolCatalogSnapshot { ProviderKey = s.ProviderKey, ExternalId = s.Id }; c.Update(s); return c; }
    public void Update(PoolSummary s) { Json = JsonSerializer.Serialize(s); FetchedAt = DateTimeOffset.UtcNow; }
}
public sealed class MockPoolReservation
{
    private MockPoolReservation() { }
    public string Reference { get; private set; } = "";
    public string ProviderKey { get; private set; } = "";
    public string OperationId { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string Status { get; private set; } = "Reserved";
    public long TotalMinor { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string? TicketCode { get; private set; }
    public long Version { get; private set; } = 1;
    public static MockPoolReservation Create(string provider, string operation, string fingerprint, long total, DateTimeOffset expires)
        => new() { Reference = $"MOCK-{provider}-{operation}", ProviderKey = provider, OperationId = operation,
            Fingerprint = fingerprint, TotalMinor = total, ExpiresAt = expires };
    public void Confirm()
    {
        if (Status == "Confirmed") return;
        if (Status != "Reserved" || ExpiresAt <= DateTimeOffset.UtcNow) throw new PoolException("رزرو آزمایشی منقضی شده است");
        Status = "Confirmed"; TicketCode = "MOCK-TICKET-" + OperationId; Version++;
    }
    public void Cancel() { if (Status != "Cancelled") { Status = "Cancelled"; Version++; } }
    public PoolReservation View() => new(Reference, Status == "Reserved" && ExpiresAt <= DateTimeOffset.UtcNow ? "Expired" : Status, ExpiresAt, TotalMinor, TicketCode, true);
}
