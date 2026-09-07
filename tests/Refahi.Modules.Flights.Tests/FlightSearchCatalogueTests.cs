using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Refahi.Modules.Flights.Application.Features.Search;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Infrastructure.Persistence;
using Refahi.Modules.Flights.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class PostgresCatalogueFactAttribute : FactAttribute
{
    public PostgresCatalogueFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLIGHT_CATALOG_TEST_CONNECTION")))
            Skip = "Set FLIGHT_CATALOG_TEST_CONNECTION to an isolated PostgreSQL test server.";
    }
}

public sealed class FlightSearchCatalogueTests
{
    [PostgresCatalogueFact]
    public async Task SqlAndSeeder_AreIdempotentAndRollbackPreservesExistingData()
    {
        await using var fixture = await DatabaseFixture.CreateAsync(manualSchema: true);
        var db = fixture.Db;
        // Existing data deliberately differs from JSON and carries metadata JSON does not provide.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO flights.airports(iata_code,city_code,airport_name_fa,airport_name_en,city_name_fa,
              city_name_en,country_code,country_name_fa,country_name_en,is_popular,is_active,source_version,
              translation_source,search_text,imported_at_utc,icao_code,latitude,longitude)
            VALUES ('YXU','YXU','نام قبلی','Old airport','لندن','London','CA','کانادا','Canada',
              false,true,'legacy','manual','old', '2020-01-01T00:00:00Z','CYXU',43.03,-81.15),
             ('IFN','IFN','نام فارسی معتبر','Old IFN','اصفهان','Isfahan','IR','ایران','Iran',
              true,true,'legacy','manual','old','2020-01-01T00:00:00Z',NULL,NULL,NULL),
             ('ZZZ','ZZZ','خارج از فهرست','Unlisted','شهر','City','IR','ایران','Iran',
              false,true,'legacy','manual','old','2020-01-01T00:00:00Z',NULL,NULL,NULL);
            """);
        var before = await Snapshot(db);
        await ExecuteScript(db, "02-data.sql");
        var yxu = await db.FlightAirports.AsNoTracking().SingleAsync(x => x.IataCode == "YXU");
        Assert.Equal("LON", yxu.CityCode);
        Assert.Equal("GB", yxu.CountryCode);
        Assert.Equal("CYXU", yxu.IcaoCode);
        Assert.Equal(43.03m, yxu.Latitude);
        Assert.Equal("نام فارسی معتبر", (await db.FlightAirports.AsNoTracking().SingleAsync(x => x.IataCode == "IFN")).AirportNameFa);
        Assert.Equal(121, await db.FlightSearchMemberships.CountAsync());
        Assert.True(await db.FlightAirports.AnyAsync(x => x.IataCode == "ZZZ" && x.IsActive));
        Assert.False(await db.FlightSearchMemberships.AnyAsync(x => x.AirportCode == "ZZZ"));
        var imported = await Snapshot(db);
        await ExecuteScript(db, "02-data.sql");
        await new FlightAirportDataSeeder(db, NullLogger<FlightAirportDataSeeder>.Instance).SeedAsync();
        Assert.Equal(imported, await Snapshot(db));
        await ExecuteScript(db, "04-rollback-data.sql");
        Assert.Equal(before, await Snapshot(db));
        Assert.Equal(0, await db.FlightSearchMemberships.CountAsync());
        await new FlightAirportDataSeeder(db, NullLogger<FlightAirportDataSeeder>.Instance).SeedAsync();
        Assert.Equal(121, await db.FlightSearchMemberships.CountAsync());
    }

    [PostgresCatalogueFact]
    public async Task Locations_GroupBeforeFilterAndLimit_AndUseCuratedMembership()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await new FlightAirportDataSeeder(fixture.Db, NullLogger<FlightAirportDataSeeder>.Instance).SeedAsync();
        var repository = new FlightLocationRepository(fixture.Db);
        var domestic = await repository.SearchAsync(true, null, 50, default);
        var international = await repository.SearchAsync(false, null, 50, default);
        Assert.Equal(40, domestic.Count);
        Assert.Equal(40, international.Count);
        Assert.Equal("THR", domestic[0].Code);
        Assert.Equal("مهرآباد", domestic[0].AirportNameFa);
        Assert.Equal("IKA", international[0].Code);
        Assert.Equal("Airport", international[0].Type);
        foreach (var text in new[] { "SAW", "صبیحا", "Sabiha", "استانبول", "Istanbul" })
        {
            var city = Assert.Single(await repository.SearchAsync(false, text, 1, default));
            Assert.Equal("IST", city.Code);
            Assert.Equal("City", city.Type);
            Assert.Equal(3, city.AirportCount);
        }
        var doha = Assert.Single(await repository.SearchAsync(false, "DOH", 1, default));
        Assert.Equal("Airport", doha.Type);
        Assert.Equal(1, doha.AirportCount);
        Assert.DoesNotContain(international, x => x.Code == "XOZ");
        Assert.Empty(await repository.SearchAsync(false, "%_", 50, default));
        Assert.Equal("KIH", Assert.Single(await repository.SearchAsync(true, "كيش", 1, default)).Code);
        Assert.Null(await repository.ResolveAsync(true, "IKA", "Airport", default));
        Assert.Null(await repository.ResolveAsync(false, "IKA", "City", default));
        Assert.Null(await repository.ResolveAsync(false, "LON", "Airport", default));
    }

    [PostgresCatalogueFact]
    public async Task Search_ValidatesBeforeProvider_AndSwapsLocationTypesForReturn()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var db = fixture.Db;
        await new FlightAirportDataSeeder(db, NullLogger<FlightAirportDataSeeder>.Instance).SeedAsync();
        var provider = new RecordingProvider();
        var handler = new SearchFlightsQueryHandler(
            provider,
            new FlightOfferSnapshotRepository(db),
            new FlightLocationRepository(db),
            new StubAirlineLogoResolver()
        );
        var request = Query("IKA", "IST", false, "Airport", "City") with { ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)) };
        await handler.Handle(request, default);
        Assert.False(provider.Last!.IsDomestic);
        var legs = provider.Last.OriginDestinationInformations.ToArray();
        Assert.Equal(("IKA", "IST", "Airport", "City"), (legs[0].OriginLocationCode, legs[0].DestinationLocationCode, legs[0].OriginType, legs[0].DestinationType));
        Assert.Equal(("IST", "IKA", "City", "Airport"), (legs[1].OriginLocationCode, legs[1].DestinationLocationCode, legs[1].OriginType, legs[1].DestinationType));
        var calls = provider.Calls;
        foreach (var invalid in new[] {
            Query("THR","IST",false), Query("IKA","MHD",false), Query("IST","SAW",false,"City","Airport"),
            Query("IKA","IST",false,"City","City"), Query("ZZZ","KIH",true), Query("THR","KIH",true,"City","Airport")
        })
            await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(invalid, default));
        Assert.Equal(calls, provider.Calls);
        await handler.Handle(Query("THR", "KIH", null), default);
        Assert.True(provider.Last!.IsDomestic);
        await handler.Handle(Query("IKA", "IST", null), default); // legacy explicit airport
        Assert.False(provider.Last!.IsDomestic);
        await handler.Handle(Query("IST", "LON", false, "City", "City"), default);
        Assert.False(provider.Last!.IsDomestic);
    }

    private static SearchFlightsQuery Query(string origin, string destination, bool? domestic,
        string originType = "Airport", string destinationType = "Airport") =>
        new(origin, destination, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), null,
            1, 0, 0, "Economy", "OneWay", domestic, null, null, null, originType, destinationType);

    private static async Task ExecuteScript(FlightsDbContext db, string name)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = await File.ReadAllTextAsync(Script(name));
            await command.ExecuteNonQueryAsync();
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private static string Script(string name) => Path.Combine(TestPaths.RepositoryRoot, "scripts", "flights", "search-catalog-20260906", name);
    private static Task<string> Snapshot(FlightsDbContext db) => db.Database.SqlQueryRaw<string>(
        """SELECT coalesce(jsonb_agg(to_jsonb(a) ORDER BY iata_code),'[]'::jsonb)::text AS "Value" FROM flights.airports a""").SingleAsync();

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        private readonly string admin;
        private readonly string database;
        public FlightsDbContext Db { get; }
        private DatabaseFixture(string admin, string database, FlightsDbContext db)
        { this.admin = admin; this.database = database; Db = db; }
        public static async Task<DatabaseFixture> CreateAsync(bool manualSchema = false)
        {
            var admin = Environment.GetEnvironmentVariable("FLIGHT_CATALOG_TEST_CONNECTION")!;
            var name = "flight_catalog_" + Guid.NewGuid().ToString("N");
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
            var builder = new NpgsqlConnectionStringBuilder(admin) { Database = name };
            var db = new FlightsDbContext(new DbContextOptionsBuilder<FlightsDbContext>()
                .UseNpgsql(builder.ConnectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", "flights")).Options);
            try
            {
                if (manualSchema)
                {
                    await db.GetService<IMigrator>().MigrateAsync("20260721052246_FlightAirportsReferenceData");
                    await ExecuteScript(db, "01-schema.sql");
                    await ExecuteScript(db, "01-schema.sql");
                    Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                }
                else await db.Database.MigrateAsync();
            }
            catch
            {
                await db.DisposeAsync();
                await using var cleanup = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", connection);
                await cleanup.ExecuteNonQueryAsync();
                throw;
            }
            return new(admin, name, db);
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class RecordingProvider : IFlightProvider, IFlightProviderFactory
    {
        public FlightSearchRequest? Last { get; private set; }
        public int Calls { get; private set; }
        public IFlightProvider GetProvider(FlightProviderType type) => this;
        public IFlightProvider GetDefaultProvider() => this;
        public Task<FlightSearchResponse> SearchAsync(FlightSearchRequest request, CancellationToken cancellationToken = default)
        {
            Last = request; Calls++;
            return Task.FromResult(new FlightSearchResponse(true, "test", "search", []));
        }
        public Task<FlightBookResponse> BookAsync(FlightBookRequest r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FlightIssueResponse> IssueAsync(FlightIssueRequest r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FlightInquiryResponse> InquiryAsync(FlightInquiryRequest r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FlightCancellationQuoteResponse> QuoteCancellationAsync(FlightCancellationQuoteRequest r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FlightCancellationSubmitResponse> SubmitCancellationAsync(FlightCancellationSubmitRequest r, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

