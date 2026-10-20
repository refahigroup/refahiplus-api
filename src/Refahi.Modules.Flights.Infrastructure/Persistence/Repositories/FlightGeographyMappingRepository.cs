using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Flights.Domain.Aggregates.FlightLocationAgg;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Repositories;

public sealed class FlightGeographyMappingRepository(FlightsDbContext db) : IFlightGeographyMappingRepository
{
    public async Task<IReadOnlyDictionary<string, int>> ReadAsync(bool commercial,
        IReadOnlyCollection<string>? codes, CancellationToken ct)
    {
        if (codes is { Count: 0 }) return new Dictionary<string, int>();
        var normalized = codes?.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (commercial)
        {
            var query = db.Set<SnappTripFlightCityMapping>().AsNoTracking();
            if (normalized is not null) query = query.Where(x => normalized.Contains(x.ProviderCityCode));
            return await query.ToDictionaryAsync(x => x.ProviderCityCode, x => x.CityId, ct);
        }
        var airports = db.Set<AirportGeographyMapping>().AsNoTracking();
        if (normalized is not null) airports = airports.Where(x => normalized.Contains(x.AirportIataCode));
        return await airports.ToDictionaryAsync(x => x.AirportIataCode, x => x.CityId, ct);
    }
}
