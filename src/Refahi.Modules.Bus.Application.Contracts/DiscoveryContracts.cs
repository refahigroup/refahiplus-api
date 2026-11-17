using MediatR;

namespace Refahi.Modules.Bus.Application.Contracts;

public sealed record BusLocation(int CityId, string Name, string Province);
public sealed record BusLocationsQuery(string? Query) : IRequest<IReadOnlyList<BusLocation>>;
public sealed record BusSearchQuery(int OriginCityId, int DestinationCityId, DateOnly DepartureDate,
    string? Company, string? BusType, TimeOnly? FromTime, string? Sort, int Page) : IRequest<BusSearchResult>;
public sealed record BusOfferQuery(Guid Token) : IRequest<BusOfferDetails>;

public sealed record BusOfferSummary(Guid Token, int OriginCityId, int DestinationCityId,
    DateOnly DepartureDate, string Company, string BusType, string DepartureTerminal,
    string ArrivalTerminal, TimeOnly DepartureTime, int AvailableSeats, long PriceMinor);
public sealed record BusSeatDto(int Number, int Row, int Column, bool Available, string? Status = null);
public sealed record BusOfferDetails(BusOfferSummary Offer, IReadOnlyList<BusSeatDto> Seats);
public sealed record BusSearchResult(BusLocation Origin, BusLocation Destination, IReadOnlyList<BusOfferSummary> Offers,
    int Page, int TotalPages);

public sealed record BusProviderService(string Code, string Company, string BusType,
    string DepartureTerminal, string ArrivalTerminal, TimeOnly DepartureTime, int AvailableSeats, long PriceMinor);

public sealed record BusProviderRegion(int Id, string Name, string NameEn, string Type, string Code, int? ParentId, string ParentName);
public sealed record BusProviderDetail(BusProviderService Service, IReadOnlyList<BusSeatDto> Seats);

public interface IBusProvider
{
    Task<IReadOnlyList<BusProviderRegion>> RegionsAsync(CancellationToken ct);
    Task<IReadOnlyList<BusProviderService>> SearchAsync(int originRegionId, int destinationRegionId, DateOnly date, CancellationToken ct);
    Task<BusProviderDetail> DetailAsync(string code, CancellationToken ct);
}

public sealed class BusRequestException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
