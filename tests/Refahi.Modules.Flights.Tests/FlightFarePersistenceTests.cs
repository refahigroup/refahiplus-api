using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Refahi.Modules.Flights.Domain.Aggregates.FlightOfferSnapshotAgg;
using Refahi.Modules.Flights.Infrastructure.Persistence;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class FlightFarePostgresFactAttribute : FactAttribute
{
    public FlightFarePostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLIGHT_FARE_TEST_CONNECTION")))
            Skip = "Set FLIGHT_FARE_TEST_CONNECTION to a PostgreSQL test server with database creation permission.";
    }
}

public sealed class FlightFarePersistenceTests
{
    [FlightFarePostgresFact]
    public async Task Migration_PreservesExistingOffersAndRoundTripsLongFareCodesThroughBooking()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLIGHT_FARE_TEST_CONNECTION")!;
        var database = "flight_fare_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var settings = new NpgsqlConnectionStringBuilder(connectionString) { Database = database, Pooling = false };
            await using var db = new FlightsDbContext(new DbContextOptionsBuilder<FlightsDbContext>()
                .UseNpgsql(settings.ConnectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "flights"))
                .Options);
            var migrator = db.GetService<IMigrator>();
            await ReferenceGeographyFixture.InstallAsync(db);
            await migrator.MigrateAsync("20260906194439_FlightSearchCatalogue");
            var now = DateTime.UtcNow;
            // Insert the historical schema directly; the current EF model also has later pricing columns.
            Task<int> InsertLegacy(FlightOfferSnapshot offer) => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO flights.flight_search_offer_snapshots(id,offer_token,provider_name,provider_fare_source_code,
                  provider_search_id,provider_trace_id,total_fare_amount,currency,public_offer_snapshot,provider_snapshot,created_at_utc,expires_at_utc)
                VALUES({offer.Id},{offer.OfferToken},{offer.ProviderName},{offer.ProviderFareSourceCode},
                  {offer.ProviderSearchId},{offer.ProviderTraceId},{offer.TotalFareAmount},{offer.Currency},
                  CAST({offer.PublicOfferSnapshotJson} AS jsonb),CAST({offer.ProviderSnapshotJson} AS jsonb),{offer.CreatedAtUtc},{offer.ExpiresAtUtc})
                """);
            var existing = Offer("existing", "short-fare", now);
            await InsertLegacy(existing);

            var fareCode = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6000));
            var longOffer = Offer("long", fareCode, now);
            var error = await Assert.ThrowsAsync<PostgresException>(() => InsertLegacy(longOffer));
            Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, error.SqlState);
            db.ChangeTracker.Clear();

            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal("short-fare", (await db.FlightOfferSnapshots.SingleAsync(x => x.Id == existing.Id)).ProviderFareSourceCode);
            db.FlightOfferSnapshots.Add(longOffer);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var storedOffer = await db.FlightOfferSnapshots.SingleAsync(x => x.Id == longOffer.Id);
            Assert.Equal(fareCode, storedOffer.ProviderFareSourceCode);

            var booking = FlightBookingTestFactory.CreateDraft(now, storedOffer.ProviderFareSourceCode);
            db.FlightBookings.Add(booking);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var storedBooking = await db.FlightBookings.SingleAsync();
            Assert.Equal(fareCode, storedBooking.SelectedFare.ProviderFareId);

            await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20260906194439_FlightSearchCatalogue"));
            db.ChangeTracker.Clear();
            Assert.Equal(fareCode, (await db.FlightBookings.SingleAsync()).SelectedFare.ProviderFareId);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static FlightOfferSnapshot Offer(string token, string fareCode, DateTime now) =>
        FlightOfferSnapshot.Create(token, "SnappTrip", fareCode, "search", "trace", 1_200_000,
            100_000, 1_300_000, "IRR", "{}", "{}", now, now.AddMinutes(20));
}
