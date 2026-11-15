using Microsoft.EntityFrameworkCore;

namespace Refahi.Modules.Cinema.Infrastructure.Persistence;

internal sealed class DiscoveryScreening
{
    public Guid Id { get; init; }
    public Guid FilmId { get; init; }
    public Guid VenueId { get; init; }
    public required string ExternalId { get; init; }
    public required string ExternalFilmId { get; init; }
    public required string ExternalVenueId { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset? PurchaseStartsAt { get; init; }
    public DateTimeOffset? PurchaseEndsAt { get; init; }
    public bool Published { get; init; }
}

internal sealed record DiscoveryState(DateTimeOffset? LastSuccessAt, bool Failed, int PendingMappings);

internal interface ICinemaScheduleInventory
{
    string Key { get; }
    IQueryable<DiscoveryScreening> Screenings { get; }
    Task<DiscoveryState?> ReadStateAsync(CancellationToken ct);
}

internal sealed class ITicketScheduleInventory(CinemaDbContext db) : ICinemaScheduleInventory
{
    public string Key => "iticket";
    public IQueryable<DiscoveryScreening> Screenings => db.Set<ITicketScreening>().AsNoTracking().Select(x =>
        new DiscoveryScreening
        {
            Id = x.Id, FilmId = x.FilmId, VenueId = x.VenueId, ExternalId = x.ExternalId,
            ExternalFilmId = x.ExternalFilmId, ExternalVenueId = x.ExternalVenueId, StartsAt = x.StartsAt,
            PurchaseStartsAt = x.PurchaseStartsAt, PurchaseEndsAt = x.PurchaseEndsAt, Published = x.Published
        });

    public Task<DiscoveryState?> ReadStateAsync(CancellationToken ct) =>
        db.Set<ITicketDiscoveryState>().Select(x => new DiscoveryState(x.LastSuccessAt, x.Failed, x.PendingMappings))
            .SingleOrDefaultAsync(ct);
}
