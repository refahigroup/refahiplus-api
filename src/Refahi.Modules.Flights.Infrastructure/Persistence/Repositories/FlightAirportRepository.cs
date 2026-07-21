using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Repositories;

public sealed class FlightAirportRepository : IFlightAirportRepository
{
    private readonly FlightsDbContext _dbContext;

    public FlightAirportRepository(FlightsDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<IReadOnlyList<FlightAirport>> SearchAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        var airports = _dbContext.FlightAirports.AsNoTracking().Where(airport => airport.IsActive);
        var normalized = FlightAirportSearchNormalizer.Normalize(query);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return await airports
                .Where(airport => airport.IsPopular)
                .OrderBy(airport => airport.CityNameFa)
                .ThenBy(airport => airport.IataCode)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        var escapedQuery = EscapeLikePattern(normalized);
        var pattern = $"%{escapedQuery}%";
        var prefixPattern = $"{escapedQuery}%";
        var normalizedCode = normalized.ToUpperInvariant();
        return await airports
            .Where(airport =>
                airport.IataCode == normalizedCode
                || airport.IataCode.StartsWith(normalizedCode)
                || EF.Functions.ILike(airport.SearchText, pattern, "\\"))
            .OrderByDescending(airport => airport.IataCode == normalizedCode)
            .ThenByDescending(airport => airport.IataCode.StartsWith(normalizedCode))
            .ThenByDescending(airport => EF.Functions.ILike(airport.AirportNameFa, prefixPattern, "\\"))
            .ThenByDescending(airport => EF.Functions.ILike(airport.CityNameFa, prefixPattern, "\\"))
            .ThenByDescending(airport => EF.Functions.ILike(airport.CountryNameFa, prefixPattern, "\\"))
            .ThenByDescending(airport => EF.Functions.ILike(airport.AirportNameEn, prefixPattern, "\\"))
            .ThenByDescending(airport => EF.Functions.ILike(airport.CityNameEn, prefixPattern, "\\"))
            .ThenByDescending(airport => EF.Functions.ILike(airport.CountryNameEn, prefixPattern, "\\"))
            .ThenByDescending(airport => airport.IsPopular)
            .ThenBy(airport => airport.CityNameFa)
            .ThenBy(airport => airport.IataCode)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FlightAirport>> GetByIataCodesAsync(
        IReadOnlyCollection<string> iataCodes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = iataCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return await _dbContext.FlightAirports
            .AsNoTracking()
            .Where(airport => airport.IsActive && normalizedCodes.Contains(airport.IataCode))
            .ToListAsync(cancellationToken);
    }

    private static string EscapeLikePattern(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
