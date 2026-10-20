using MediatR;
using Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest;

namespace Refahi.Modules.Hotels.Application.Contracts.Catalogue;

public sealed record HotelParty(int Adults, IReadOnlyList<int> ChildrenAges);
public sealed record HotelTravel(DateOnly CheckIn, DateOnly CheckOut, IReadOnlyList<HotelParty> Rooms);
public sealed record HotelCityDto(Guid CityKey, string Name, int? LegacyId);
public sealed record ProviderCity(string Id, string Name, string? Code, string Country, int? LegacyId = null);
public sealed record ProviderHotel(string Id, string CityId, string Name, string Address, int Stars,
    IReadOnlyList<string> Images, string Description = "", IReadOnlyList<string>? Facilities = null, string? Policies = null,
    string AccommodationType = "hotel", string? CheckInTime = null, string? CheckOutTime = null);
public sealed record ProviderHotelOffer(ProviderHotel Hotel, string RoomId, string RoomName, string RatePlanId,
    string BoardType, int Capacity, long DisplayPriceMinor, long SupplyPriceMinor, long FeeMinor,
    string? AgencyProviderId = null, string? RawSnapshot = null, int? AdultCapacity = null);
public sealed record HotelOfferDto(Guid HotelKey, string OfferToken, string HotelName, string Address, int Stars,
    IReadOnlyList<string> Images, string RoomName, string BoardType, int Capacity, long PriceMinor,
    DateTime ExpiresAtUtc, bool CanBook);
public sealed record HotelSearchResponse(IReadOnlyList<HotelOfferDto> Offers, bool IsPartial, string? Message);
public sealed record HotelDetailsResponse(Guid HotelKey, string Name, string Address, string Description, int Stars,
    IReadOnlyList<string> Images, IReadOnlyList<string> Facilities, string? Policies, HotelSearchResponse Availability,
    string? CheckInTime = null, string? CheckOutTime = null);
public sealed record HotelGuest(string FirstName, string LastName, string? NationalCode, string Country,
    string Type, int Age, int RoomIndex);
public sealed record HotelProviderCapabilities(bool Search, bool Purchase, bool Inquiry, bool Cancellation, bool Voucher);
public sealed record HotelSubmitRequest(string ReferenceId, ProviderHotelOffer Offer, HotelTravel Travel,
    IReadOnlyList<HotelGuest> Guests, string Phone, string? Email);
public sealed record HotelSubmitResult(string ReferenceId, long SupplyPriceMinor, long FeeMinor, DateTime LockedUntilUtc,
    string Snapshot);
public sealed record HotelConfirmResult(string ReservationId, string Status);
public sealed class HotelExternalOutcomeUnknownException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class HotelCatalogueConflictException(string message) : Exception(message);

public interface IHotelCatalogueProvider
{
    string Name { get; }
    HotelProviderCapabilities Capabilities { get; }
    Task<IReadOnlyList<ProviderCity>> CitiesAsync(CancellationToken ct);
    Task<IReadOnlyList<ProviderHotelOffer>> SearchAsync(string cityId, HotelTravel travel, CancellationToken ct);
    Task<IReadOnlyList<ProviderHotelOffer>> SearchFreshAsync(string cityId, HotelTravel travel, CancellationToken ct) => SearchAsync(cityId, travel, ct);
    Task<ProviderHotelOffer> ValidateOfferAsync(ProviderHotelOffer offer, HotelTravel travel, CancellationToken ct) => Task.FromResult(offer);
    Task<ProviderHotel> DetailsAsync(string hotelId, string cityId, CancellationToken ct);
    Task<HotelSubmitResult> SubmitAsync(HotelSubmitRequest request, CancellationToken ct);
    Task<HotelConfirmResult> ConfirmAsync(string referenceId, CancellationToken ct);
}
public interface IHotelCatalogueProviderRegistry
{
    IReadOnlyList<IHotelCatalogueProvider> Active { get; }
    IHotelCatalogueProvider Get(string name);
}
public interface IHotelSalePriceCalculator
{
    int Version { get; }
    long CalculateFinalPrice(ProviderHotelOffer offer);
}
public sealed record GetHotelCitiesQuery(string? Name = null) : IRequest<IReadOnlyList<HotelCityDto>>;
public sealed record SearchHotelCatalogueQuery(Guid? CityKey, int? LegacyCityId, HotelTravel Travel,
    long? MinPrice = null, long? MaxPrice = null, int[]? Stars = null, string Sort = "price",
    string[]? Accommodations = null) : IRequest<HotelSearchResponse>;
public sealed record GetHotelCatalogueDetailsQuery(Guid HotelKey, HotelTravel Travel) : IRequest<HotelDetailsResponse>;
public sealed record GetHotelOfferQuery(string OfferToken) : IRequest<HotelOfferDto>;
public sealed record QuoteHotelOfferQuery(string OfferToken, HotelTravel Travel) : IRequest<HotelOfferDto>;
public sealed record CreateHotelCatalogueRequestCommand(Guid UserId, string OfferToken, HotelTravel Travel,
    IReadOnlyList<HotelGuest> Guests, string Phone, string? Email, long ExpectedTotalPriceMinor,
    string IdempotencyKey) : IRequest<CreateHotelRequestResponse>;
public sealed record FinalizeHotelCatalogueCommand(Guid OrderId, Guid UserId) : IRequest;
public sealed record RecoverHotelCatalogueCommand : IRequest;

public sealed class HotelCatalogueUnavailableException(string message) : Exception(message);
