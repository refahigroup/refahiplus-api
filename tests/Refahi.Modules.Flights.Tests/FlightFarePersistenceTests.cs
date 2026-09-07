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
            await migrator.MigrateAsync("20260906194439_FlightSearchCatalogue");
            var now = DateTime.UtcNow;
            var existing = Offer("existing", "short-fare", now);
            db.FlightOfferSnapshots.Add(existing);
            await db.SaveChangesAsync();

            var fareCode = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6000));
            var longOffer = Offer("long", fareCode, now);
            db.FlightOfferSnapshots.Add(longOffer);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
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
