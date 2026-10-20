using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Flights.Application.Features.Airports.GetFlightAirports;
using Refahi.Modules.Flights.Application.Features.Locations;
using Refahi.Modules.Flights.Infrastructure.Persistence;
using Refahi.Modules.Flights.Infrastructure.Persistence.Repositories;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.References.Application.Features.Cities.GetCityGeographies;
using Refahi.Modules.References.Infrastructure.Persistence.Context;
using Refahi.Modules.References.Infrastructure.Repositories;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed partial class FlightSearchCatalogueTests
{
    [PostgresCatalogueFact]
    public async Task CanonicalGeography_UsesTwoIndependentRelationships_PreservesCatalogueAndRawData()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var db = fixture.Db;
        await new FlightAirportDataSeeder(db,NullLogger<FlightAirportDataSeeder>.Instance).SeedAsync();
        var before = await Snapshot(db);
        // Model the known YXU/LON discrepancy: physical geography must never replace the provider's LON route code.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "references".countries("Code","Name","NameEn") VALUES('CA','کانادا','Canada');
            INSERT INTO "references".provinces("Id","Name","NameEn","Slug","SortOrder","IsActive","CountryCode","IsSynthetic")
              VALUES(90001,'کانادا','Canada','test-canada',90001,true,'CA',true);
            INSERT INTO "references".cities("Id","Name","NameEn","Slug","ProvinceId","SortOrder","IsActive","LocationKind")
              VALUES(90001,'شهر واقعی کانادا','Actual Canada City','test-london-ca',90001,90001,true,'city'),
                    (90002,'شهر تجاری بریتانیا','Commercial UK City','test-london-gb',
                     (SELECT "Id" FROM "references".provinces WHERE "CountryCode"='GB'),90002,true,'city');
            INSERT INTO flights.airport_geography_mappings VALUES('YXU',90001,'raw','test','{{}}','test','2026-10-20',now());
            INSERT INTO flights.snapptrip_city_mappings VALUES('LON',90002,'raw','test','{{}}','test','2026-10-20',now());
            """);
        await using var references = new ReferencesDbContext(new DbContextOptionsBuilder<ReferencesDbContext>()
            .UseNpgsql(db.Database.GetConnectionString()).Options);
        var reader = new CityGeographyReader(references);
        var mediator = new Mediator(type => type==typeof(IRequestHandler<GetCityGeographiesQuery,IReadOnlyList<CityGeography>>)
            ? new GetCityGeographiesHandler(reader) : Array.Empty<IPipelineBehavior<GetCityGeographiesQuery,IReadOnlyList<CityGeography>>>());
        var geography = new FlightGeographyService(new FlightGeographyMappingRepository(db),mediator,
            NullLogger<FlightGeographyService>.Instance);
        var locations = new FlightLocationRepository(db,geography);
        var international = await locations.SearchAsync(false,null,50,default);
        Assert.Equal(40,international.Count);
        Assert.Equal(40,(await locations.SearchAsync(true,null,50,default)).Count);
        var london = Assert.Single(await locations.SearchAsync(false,"شهر تجاری",1,default));
        Assert.Equal("LON",london.Code); Assert.Equal("City",london.Type);
        Assert.Equal(90002,london.CommercialReferenceCityId);
        Assert.Equal("GB",london.CountryCode);
        var physical = (await locations.GetAirportPresentationsAsync(["YXU"],default))["YXU"];
        Assert.Equal("شهر واقعی کانادا",physical.CityNameFa);
        Assert.Equal("CA",physical.GeographicReferenceCountryCode);
        var airports = new FlightAirportRepository(db,geography);
        var handler = new GetFlightAirportsQueryHandler(airports,geography);
        var airport = Assert.Single((await handler.Handle(new("شهر واقعی کانادا",1),default)).Airports);
        Assert.Equal("YXU",airport.Code); Assert.Equal(90001,airport.GeographicReferenceCityId);
        Assert.Equal("CA",airport.CountryCode);
        Assert.Equal(before,await Snapshot(db));
        await db.Database.ExecuteSqlRawAsync("UPDATE \"references\".cities SET \"IsActive\"=false WHERE \"Id\"=90001");
        var fallback = (await locations.GetAirportPresentationsAsync(["YXU"],default))["YXU"];
        Assert.Null(fallback.GeographicReferenceCityId);
        Assert.Equal("GB",(await locations.ResolveAsync(false,"YXU","Airport",default))!.CountryCode);
        Assert.Null((await locations.ResolveAsync(true,"THR","Airport",default))!.CommercialReferenceCityId);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
