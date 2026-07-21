using System.IO.Compression;
using System.Text.Json;
using Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg;
using Refahi.Modules.Flights.Infrastructure.Persistence;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class FlightAirportReferenceDataTests
{
    [Fact]
    public void EmbeddedSnapshot_HasUniqueIataAndCompleteLocalizedCaptions()
    {
        var assembly = typeof(FlightAirportDataSeeder).Assembly;
        var resourceName = Assert.Single(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith("Data.airports-20260714.json.gz", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var document = JsonDocument.Parse(gzip);

        var records = document.RootElement.EnumerateArray().ToArray();
        Assert.True(records.Length > 9_000);
        Assert.Equal(records.Length, records.Select(record => Required(record, "iataCode")).Distinct().Count());
        Assert.All(records, record =>
        {
            Assert.Equal(3, Required(record, "iataCode").Length);
            Assert.False(string.IsNullOrWhiteSpace(Required(record, "airportNameFa")));
            Assert.False(string.IsNullOrWhiteSpace(Required(record, "cityNameFa")));
            Assert.False(string.IsNullOrWhiteSpace(Required(record, "countryNameFa")));
            Assert.False(string.IsNullOrWhiteSpace(Required(record, "airportNameEn")));
            Assert.False(string.IsNullOrWhiteSpace(Required(record, "cityNameEn")));
            Assert.False(string.IsNullOrWhiteSpace(Required(record, "countryNameEn")));
        });
    }

    [Fact]
    public void RefreshFromSource_ReactivatesPreviouslyRemovedAirport()
    {
        var importedAt = DateTime.UtcNow;
        var airport = FlightAirport.Create(
            "THR", "OIII", "THR", "مهرآباد", "Mehrabad", "تهران", "Tehran",
            "IR", "ایران", "Iran", 35.68m, 51.31m, true, "v1", "verified", "thr مهرآباد", importedAt);
        airport.Deactivate();

        airport.RefreshFromSource(
            "OIII", "THR", "فرودگاه مهرآباد", "Mehrabad International Airport",
            "تهران", "Tehran", "IR", "ایران", "Iran", 35.68m, 51.31m, true,
            "v2", "verified", "thr فرودگاه مهرآباد", importedAt.AddDays(1));

        Assert.True(airport.IsActive);
        Assert.Equal("v2", airport.SourceVersion);
        Assert.Equal("فرودگاه مهرآباد", airport.AirportNameFa);
    }

    private static string Required(JsonElement record, string name)
        => record.GetProperty(name).GetString() ?? string.Empty;
}
