namespace Refahi.Modules.Flights.Domain.Repositories;

public interface IFlightGeographyMappingRepository
{
    Task<IReadOnlyDictionary<string, int>> ReadAsync(bool commercial, IReadOnlyCollection<string>? codes, CancellationToken ct);
}
