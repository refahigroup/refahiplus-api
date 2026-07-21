using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg;

namespace Refahi.Modules.Flights.Infrastructure.Persistence;

public sealed class FlightAirportDataSeeder
{
    private const string ResourceSuffix = "Data.airports-20260714.json.gz";
    private const string SourceVersion = "ourairports-20260714";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FlightsDbContext _dbContext;
    private readonly ILogger<FlightAirportDataSeeder> _logger;

    public FlightAirportDataSeeder(FlightsDbContext dbContext, ILogger<FlightAirportDataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var records = await ReadRecordsAsync(cancellationToken);
        if (records.Count == 0)
            throw new InvalidOperationException("فایل مرجع فرودگاه‌ها خالی است.");

        var importedRecordCount = await _dbContext.FlightAirports.CountAsync(
            airport => airport.SourceVersion == SourceVersion && airport.IsActive,
            cancellationToken);
        if (importedRecordCount == records.Count)
            return;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await _dbContext.FlightAirports.ToDictionaryAsync(
            airport => airport.IataCode,
            StringComparer.OrdinalIgnoreCase,
            cancellationToken);
        var importedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedAtUtc = DateTime.UtcNow;

        foreach (var record in records)
        {
            importedCodes.Add(record.IataCode);
            var searchText = FlightAirportSearchNormalizer.Normalize(string.Join(' ',
                record.IataCode,
                record.IcaoCode,
                record.AirportNameFa,
                record.AirportNameEn,
                record.CityNameFa,
                record.CityNameEn,
                record.CountryNameFa,
                record.CountryNameEn));

            if (existing.TryGetValue(record.IataCode, out var airport))
            {
                airport.RefreshFromSource(
                    record.IcaoCode, record.CityCode, record.AirportNameFa, record.AirportNameEn,
                    record.CityNameFa, record.CityNameEn, record.CountryCode, record.CountryNameFa,
                    record.CountryNameEn, record.Latitude, record.Longitude, record.IsPopular,
                    SourceVersion, record.TranslationSource, searchText, importedAtUtc);
            }
            else
            {
                _dbContext.FlightAirports.Add(FlightAirport.Create(
                    record.IataCode, record.IcaoCode, record.CityCode, record.AirportNameFa,
                    record.AirportNameEn, record.CityNameFa, record.CityNameEn, record.CountryCode,
                    record.CountryNameFa, record.CountryNameEn, record.Latitude, record.Longitude,
                    record.IsPopular, SourceVersion, record.TranslationSource, searchText, importedAtUtc));
            }
        }

        foreach (var airport in existing.Values.Where(airport => !importedCodes.Contains(airport.IataCode)))
            airport.Deactivate();

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Flight airport reference data imported. Version={Version}, AirportCount={AirportCount}",
            SourceVersion,
            records.Count);
    }

    private static async Task<IReadOnlyList<AirportSeedRecord>> ReadRecordsAsync(CancellationToken cancellationToken)
    {
        var assembly = typeof(FlightAirportDataSeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (resourceName is null)
            throw new InvalidOperationException($"Embedded airport data '{ResourceSuffix}' was not found.");

        await using var resource = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded airport data '{resourceName}' could not be opened.");
        await using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        return await JsonSerializer.DeserializeAsync<List<AirportSeedRecord>>(gzip, JsonOptions, cancellationToken) ?? [];
    }

    internal sealed record AirportSeedRecord(
        string IataCode,
        string? IcaoCode,
        string CityCode,
        string AirportNameFa,
        string AirportNameEn,
        string CityNameFa,
        string CityNameEn,
        string CountryCode,
        string CountryNameFa,
        string CountryNameEn,
        decimal? Latitude,
        decimal? Longitude,
        bool IsPopular,
        string TranslationSource);
}
