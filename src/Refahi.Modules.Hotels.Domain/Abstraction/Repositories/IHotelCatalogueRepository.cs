using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
namespace Refahi.Modules.Hotels.Domain.Abstraction.Repositories;

public interface IHotelCatalogueRepository
{
    Task<IReadOnlyList<HotelCityReference>> CitiesAsync(CancellationToken ct);
    Task<HotelListing> ListingAsync(Guid cityKey, string provider, string nativeId, string nativeCityId, CancellationToken ct);
    Task<HotelListing?> FindListingAsync(Guid key, CancellationToken ct);
    Task<HotelOfferSnapshot?> OfferAsync(string token, CancellationToken ct);
    Task<HotelExternalReservation?> ReservationAsync(Guid requestId, CancellationToken ct);
    Task<IReadOnlyList<HotelExternalReservation>> RecoveryAsync(CancellationToken ct);
    void AddCity(HotelCityReference city);
    void AddOffer(HotelOfferSnapshot offer);
    void AddReservation(HotelExternalReservation reservation);
    Task SaveAsync(CancellationToken ct);
    Task<IAsyncDisposable> LockAsync(string key, CancellationToken ct);
}
