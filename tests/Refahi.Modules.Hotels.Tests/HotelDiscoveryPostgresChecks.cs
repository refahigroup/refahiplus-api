using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;
using Refahi.Modules.References.Application.Contracts.Dtos;
using Refahi.Modules.References.Application.Contracts.Queries;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

// Invoked inside the existing guarded, disposable upgrade fixture, before its downgrade checks.
internal static class HotelDiscoveryPostgresChecks
{
    internal static async Task Run(HotelsDbContext db, SnappTripCityMapping mapping)
    {
        var now=DateTimeOffset.UtcNow;
        var key=Guid.NewGuid();
        for(var i=0;i<1200;i++)
        {
            var hotel=SnappTripDiscoveryHotel.Create(i==1199?key:Guid.NewGuid(),"discovery-"+i,mapping.ProviderCityId);
            hotel.Publish(mapping.CityId,$"Hotel {i:D4}","ارکیده","پیروزی",4,"[]","[]",null,"هتل ارکیده پیروزی استان snapptrip 4 ستاره",$"hotel {i:D4}",now, i==0?null:(1200-i)*0.001, i==0?null:0);
            db.Add(hotel);
        }
        var state=SnappTripDiscoverySyncState.Create(mapping.ProviderCityId);state.Succeed(now,24);db.Add(state);await db.SaveChangesAsync();
        var geography=new Geography(mapping.CityId);var registry=new Registry();
        var reader=new HotelDiscoveryReader(db,geography,registry,Options.Create(new HotelDiscoveryOptions()));
        var watch=Stopwatch.StartNew();
        var page=await reader.SearchAsync(new HotelDiscoverySearch("ارکیده پیروزی",Latitude:0,Longitude:0),20,default);
        Assert.Equal(key,page.Items[0].HotelKey);Assert.Equal(1200,page.TotalCount);Assert.False(page.IsPartial);
        Assert.Equal(20,page.Items.Count);
        var prefix=await reader.SearchAsync(new HotelDiscoverySearch(Latitude:0,Longitude:0),1000,default);
        Assert.Equal(1000,prefix.Items.Count);Assert.DoesNotContain(prefix.Items,x=>x.Latitude is null);
        var filtered=await reader.SearchAsync(new HotelDiscoverySearch(ProvinceId:999),20,default);Assert.Empty(filtered.Items);
        Assert.NotNull(await reader.ProfileAsync(key,default));
        Console.WriteLine($"Synthetic PostgreSQL discovery: 1200 rows, nearby prefixes 20/1000 + profile, {watch.ElapsedMilliseconds} ms total.");
        // Freshness is independent of missing GIS. Stale rows remain visible; expired rows do not.
        await db.Database.ExecuteSqlRawAsync("UPDATE hotels.snapptrip_discovery_hotels SET \"ObservedAtUtc\"=CURRENT_TIMESTAMP - interval '2 days'");
        Assert.All((await reader.SearchAsync(new HotelDiscoverySearch(CityId:mapping.CityId),20,default)).Items,x=>Assert.True(x.IsStale));
        await db.Database.ExecuteSqlRawAsync("UPDATE hotels.snapptrip_discovery_hotels SET \"ObservedAtUtc\"=CURRENT_TIMESTAMP - interval '8 days'");
        Assert.Empty((await reader.SearchAsync(new HotelDiscoverySearch(),20,default)).Items);
        await Assert.ThrowsAsync<Refahi.Modules.Catalogue.Application.Contracts.CatalogueUnavailableException>(()=>reader.ProfileAsync(key,default));
        geography.Active=false;
        Assert.Empty(await reader.CitiesAsync(default));Assert.Null(await reader.ProfileAsync(key,default));
    }
    private sealed class Geography(int city) : ISender
    {
        public bool Active=true;
        public Task<T> Send<T>(IRequest<T> request,CancellationToken ct=default) => Task.FromResult((T)(object)(request switch {
            GetCitiesQuery => new GetCitiesResponse(Active?[new(city,"شهر","City","city",1,"استان",0,true)]:[]),
            GetProvincesQuery => new GetProvincesResponse([new(1,"استان","Province","province",0,true)]),
            _ => throw new InvalidOperationException("Unexpected call; discovery must not invoke availability.")
        }));
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    private sealed class Registry : IHotelCatalogueProviderRegistry
    {
        public IReadOnlyList<IHotelCatalogueProvider> Active=>[new Provider()];
        public IHotelCatalogueProvider Get(string name)=>throw new InvalidOperationException();
    }
    private sealed class Provider : IHotelCatalogueProvider
    {
        public string Name=>"SnappTrip";
        public HotelProviderCapabilities Capabilities=>new(true,false,false,false,false);
        public Task<IReadOnlyList<string>> ResolveCityIdsAsync(int id,CancellationToken ct)=>throw new InvalidOperationException();
        public Task<IReadOnlyList<ProviderHotelOffer>> SearchAsync(string city,HotelTravel travel,CancellationToken ct)=>throw new InvalidOperationException();
        public Task<ProviderHotel> DetailsAsync(string id,string city,CancellationToken ct)=>throw new InvalidOperationException();
        public Task<HotelSubmitResult> SubmitAsync(HotelSubmitRequest request,CancellationToken ct)=>throw new InvalidOperationException();
        public Task<HotelConfirmResult> ConfirmAsync(string reference,CancellationToken ct)=>throw new InvalidOperationException();
    }
}
