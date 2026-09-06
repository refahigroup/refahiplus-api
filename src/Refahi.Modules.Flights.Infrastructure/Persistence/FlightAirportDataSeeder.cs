using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Refahi.Modules.Flights.Infrastructure.Persistence;

public sealed class FlightAirportDataSeeder(FlightsDbContext db, ILogger<FlightAirportDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Exact manual import: version check, advisory lock, transaction and before-images.
        // Never reapply the legacy snapshot over the curated catalogue.
        var assembly = typeof(FlightAirportDataSeeder).Assembly;
        await using var stream = assembly.GetManifestResourceStream("Flights.SearchCatalog.Import.sql")
            ?? throw new InvalidOperationException("فایل کاتالوگ جستجوی پرواز یافت نشد.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally { await db.Database.CloseConnectionAsync(); }
        logger.LogInformation("Flight search catalogue checked. Version={Version}", "search-catalog-20260906");
    }
}
