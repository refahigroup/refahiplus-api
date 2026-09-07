using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Infrastructure.Persistence.Repositories;

public sealed class FlightLocationRepository(FlightsDbContext db) : IFlightLocationRepository
{
    public async Task<IReadOnlyDictionary<string, FlightAirportPresentation>> GetAirportPresentationsAsync(
        IEnumerable<string?> airportCodes,
        CancellationToken ct
    )
    {
        var codes = airportCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (codes.Length == 0)
            return new Dictionary<string, FlightAirportPresentation>(StringComparer.OrdinalIgnoreCase);

        var airports = await db.FlightAirports
            .AsNoTracking()
            .Where(airport => airport.IsActive && codes.Contains(airport.IataCode))
            .Select(airport => new FlightAirportPresentation(
                airport.IataCode,
                airport.CityNameFa,
                airport.AirportNameFa
            ))
            .ToListAsync(ct);

        return airports.ToDictionary(
            airport => airport.AirportCode,
            StringComparer.OrdinalIgnoreCase
        );
    }

    // This is a deliberately bounded reference catalogue (40 cities per mode).
    // Materialize memberships before grouping so a text match/limit never changes airport counts.
    private async Task<List<Row>> ReadAsync(bool domestic, CancellationToken ct) =>
        await (from m in db.FlightSearchMemberships.AsNoTracking()
               join c in db.FlightSearchCities.AsNoTracking() on m.CityCode equals c.CityCode
               join a in db.FlightAirports.AsNoTracking() on m.AirportCode equals a.IataCode
               where m.IsDomestic == domestic && a.IsActive
               select new Row(c.CityCode, c.CityNameFa, c.CityNameEn, c.CountryCode,
                   c.CountryNameFa, c.CountryNameEn, a.IataCode, a.AirportNameFa,
                   a.AirportNameEn, m.CityRank, m.AirportRank)).ToListAsync(ct);

    public async Task<IReadOnlyList<FlightLocation>> SearchAsync(bool isDomestic, string? query, int limit, CancellationToken ct)
    {
        var rows = await ReadAsync(isDomestic, ct);
        var q = FlightAirportSearchNormalizer.Normalize(query);
        var groups = isDomestic
            ? rows.GroupBy(x => x.AirportCode)
            : rows.GroupBy(x => x.CityCode);
        return groups.Select(g => new { Rows = g.ToList(), Score = g.Max(x => Score(x, q)) })
            .Where(x => q.Length == 0 || x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Rows.Max(r => r.CityRank))
            .ThenByDescending(x => x.Rows.Max(r => r.AirportRank))
            .ThenBy(x => x.Rows[0].CityCode, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(x => Map(x.Rows[0], x.Rows.Count, !isDomestic && x.Rows.Count > 1))
            .ToList();
    }

    public async Task<FlightLocation?> ResolveAsync(bool isDomestic, string code, string type, CancellationToken ct)
    {
        var rows = await ReadAsync(isDomestic, ct);
        code = code.Trim().ToUpperInvariant();
        if (string.Equals(type, "Airport", StringComparison.OrdinalIgnoreCase))
        {
            // Retain explicit airport requests for legacy URLs, including city members.
            var airport = rows.SingleOrDefault(x => x.AirportCode == code);
            return airport is null ? null : Map(airport, 1, false);
        }
        if (!isDomestic && string.Equals(type, "City", StringComparison.OrdinalIgnoreCase))
        {
            var city = rows.Where(x => x.CityCode == code).ToList();
            return city.Count > 1 ? Map(city[0], city.Count, true) : null;
        }
        return null;
    }

    private static FlightLocation Map(Row r, int count, bool city) => new(
        city ? r.CityCode : r.AirportCode, city ? "City" : "Airport", r.CityCode,
        r.CityNameFa, r.CityNameEn, r.CountryCode, r.CountryNameFa, r.CountryNameEn,
        count, city ? null : r.AirportNameFa, city ? null : r.AirportNameEn);

    private static int Score(Row r, string q)
    {
        if (q.Length == 0) return 0;
        var values = new[] { r.CityCode, r.AirportCode, r.CityNameFa, r.CityNameEn,
            r.CountryCode, r.CountryNameFa, r.CountryNameEn, r.AirportNameFa, r.AirportNameEn }
            .Select(FlightAirportSearchNormalizer.Normalize).ToArray();
        if (values.Any(v => v == q)) return 3;
        if (values.Any(v => v.StartsWith(q, StringComparison.Ordinal))) return 2;
        return values.Any(v => v.Contains(q, StringComparison.Ordinal)) ? 1 : 0;
    }

    private sealed record Row(string CityCode, string CityNameFa, string CityNameEn,
        string CountryCode, string CountryNameFa, string CountryNameEn, string AirportCode,
        string AirportNameFa, string AirportNameEn, int CityRank, int AirportRank);
}
