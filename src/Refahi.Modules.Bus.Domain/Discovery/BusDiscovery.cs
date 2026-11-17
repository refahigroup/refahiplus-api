namespace Refahi.Modules.Bus.Domain.Discovery;

public sealed record BusCityMapping(int CityId, int ProviderRegionId);

public sealed record BusService(
    string Code, string Company, string BusType, string DepartureTerminal, string ArrivalTerminal,
    TimeOnly DepartureTime, int AvailableSeats, long PriceMinor);

public sealed record BusSeat(int Number, int Row, int Column, bool Available);

public sealed class BusOffer
{
    private BusOffer() { }

    public Guid Id { get; private set; }
    public int OriginCityId { get; private set; }
    public int DestinationCityId { get; private set; }
    public DateOnly DepartureDate { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public BusService Service { get; private set; } = null!;

    public static BusOffer Create(int origin, int destination, DateOnly date, BusService service, DateTimeOffset expiresAt) =>
        new() { Id = Guid.NewGuid(), OriginCityId = origin, DestinationCityId = destination,
            DepartureDate = date, Service = service, ExpiresAt = expiresAt };
}

public interface IBusDiscoveryRepository
{
    Task<IReadOnlyList<BusCityMapping>> CitiesAsync(CancellationToken ct);
    Task StoreOffersAsync(IReadOnlyList<BusOffer> offers, DateTimeOffset now, CancellationToken ct);
    Task<BusOffer?> GetOfferAsync(Guid id, CancellationToken ct);
}
