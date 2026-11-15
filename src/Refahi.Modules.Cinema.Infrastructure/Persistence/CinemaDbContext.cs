using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Cinema.Domain;
namespace Refahi.Modules.Cinema.Infrastructure.Persistence;
public sealed class CinemaDbContext(DbContextOptions<CinemaDbContext> options) : DbContext(options)
{
    public const string Schema = "cinema";
    public DbSet<CinemaOrder> Orders => Set<CinemaOrder>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema(Schema);
        var order=model.Entity<CinemaOrder>();order.ToTable("orders");order.HasKey(x=>x.Id);
        order.Property(x=>x.IdempotencyKey).HasMaxLength(128);order.Property(x=>x.ProviderKey).HasMaxLength(32);
        order.Property(x=>x.ProviderOrderId).HasMaxLength(128);
        order.Property(x=>x.Version).IsConcurrencyToken();
        order.HasIndex(x=>new{x.UserId,x.IdempotencyKey}).IsUnique();
        order.HasIndex(x=>new{x.ProviderKey,x.ProviderOrderId}).IsUnique().HasFilter("\"ProviderOrderId\" IS NOT NULL");
        order.HasIndex(x=>x.OrderId).IsUnique().HasFilter("\"OrderId\" IS NOT NULL");
        order.HasIndex(x=>new{x.IssuanceStatus,x.UpdatedAt});
        order.Ignore(x=>x.HasTicket);
        order.HasMany(x=>x.Attempts).WithOne().HasForeignKey(x=>x.CinemaOrderId).OnDelete(DeleteBehavior.Cascade);
        order.Navigation(x=>x.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
        var attempt=model.Entity<CinemaProviderAttempt>();attempt.ToTable("provider_attempts");attempt.HasKey(x=>x.Id);
        // Attempt IDs are assigned by the domain, including attempts added to existing orders.
        attempt.Property(x=>x.Id).ValueGeneratedNever();
        model.Entity<CinemaFilm>().ToTable("films").HasMany(x => x.Credits)
            .WithOne().HasForeignKey(x => x.FilmId);
        model.Entity<CinemaPerson>().ToTable("people");
        model.Entity<CinemaCredit>().ToTable("film_credits").HasKey(x => new { x.FilmId, x.PersonId });
        model.Entity<CinemaVenue>().ToTable("venues").HasIndex(x => x.CityId);
        model.Entity<ITicketMapping>().ToTable("iticket_mappings").HasKey(x => new { x.Kind, x.ExternalId });
        model.Entity<ITicketGeography>().ToTable("iticket_geographies").HasKey(x => x.ExternalCityId);
        model.Entity<ITicketGeography>().Property(x => x.ExternalCityId).ValueGeneratedNever();
        var screenings = model.Entity<ITicketScreening>().ToTable("iticket_screenings");
        screenings.HasKey(x => x.ExternalId);
        screenings.HasIndex(x => x.Id).IsUnique();
        screenings.HasIndex(x => new { x.Published, x.StartsAt });
        screenings.HasIndex(x => new { x.FilmId, x.VenueId, x.StartsAt });
        screenings.HasOne<CinemaFilm>().WithMany().HasForeignKey(x => x.FilmId);
        screenings.HasOne<CinemaVenue>().WithMany().HasForeignKey(x => x.VenueId);
        model.Entity<ITicketDiscoveryState>().ToTable("iticket_discovery_state").Property(x => x.Id).ValueGeneratedNever();
    }
}
public sealed class CinemaOrderRepository(CinemaDbContext db) : ICinemaOrderRepository
{
    public async Task<CinemaOrder?> GetAsync(Guid id,CancellationToken ct)
    {
        var tracked=db.ChangeTracker.Entries<CinemaOrder>().SingleOrDefault(x=>x.Entity.Id==id);
        if(tracked!=null){await tracked.ReloadAsync(ct);await tracked.Collection(x=>x.Attempts).LoadAsync(ct);return tracked.Entity;}
        return await db.Orders.Include(x=>x.Attempts).SingleOrDefaultAsync(x=>x.Id==id,ct);
    }
    public Task<CinemaOrder?> FindAsync(Guid user,string key,CancellationToken ct)=>db.Orders.Include(x=>x.Attempts).SingleOrDefaultAsync(x=>x.UserId==user&&x.IdempotencyKey==key,ct);
    public async Task<IReadOnlyList<Guid>> CandidatesAsync(CancellationToken ct,int skip=0)=>await db.Orders.AsNoTracking()
        .Where(x=>x.PaymentStatus!="Refunded" && (x.IssuanceStatus!="Issued" || x.CancellationStatus!="None")
            && (x.ProviderOrderId!=null || x.IssuanceStatus=="Reserving") && (x.CancellationStatus!="Cancelled" || x.OrderId!=null))
        .OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id).Select(x=>x.Id).Skip(skip).Take(50).ToArrayAsync(ct);
    public async Task AddAsync(CinemaOrder order,CancellationToken ct){db.Orders.Add(order);await db.SaveChangesAsync(ct);}
    public async Task SaveAsync(CancellationToken ct)=>await db.SaveChangesAsync(ct);
}
