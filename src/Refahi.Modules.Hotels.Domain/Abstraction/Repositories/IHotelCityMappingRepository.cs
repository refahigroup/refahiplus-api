namespace Refahi.Modules.Hotels.Domain.Abstraction.Repositories;

public interface IHotelCityMappingRepository
{
    Task<IReadOnlyList<string>> SnappTripCityIdsAsync(int referenceCityId, CancellationToken ct);
    Task<IReadOnlyList<string>> LamasoCityIdsAsync(int referenceCityId, CancellationToken ct);
}
