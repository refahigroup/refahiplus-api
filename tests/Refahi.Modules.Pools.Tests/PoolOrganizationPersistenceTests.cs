using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Media.Application.Contracts.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Persistence;
using Refahi.Modules.Pools.Infrastructure.Providers;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
using Xunit;
namespace Refahi.Modules.Pools.Tests;
[Collection("PoolsPostgres")]
public sealed class PoolOrganizationPersistenceTests
{
    private static string Connection => Environment.GetEnvironmentVariable("REFAHI_POOLS_TEST_CONNECTION")!;
    private static PoolsDbContext Db()=>new(new DbContextOptionsBuilder<PoolsDbContext>().UseNpgsql(Connection,p=>p.MigrationsHistoryTable("__EFMigrationsHistory","pools")).Options);
    private static PoolTicketFulfillment Service(PoolsDbContext db,Provider provider)=>new(db,new Factory(provider),new Protector(),
        Options.Create(new PoolTicketOptions{UserMobile="09001769450",PriceUnit="TOMAN"}),TimeProvider.System,NullLogger<PoolTicketFulfillment>.Instance);
    private static async Task<Guid> Seed()
    {
        Assert.Contains("test",new Npgsql.NpgsqlConnectionStringBuilder(Connection).Database!);
        await using var db=Db();await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var request=PoolRequest.Create(Guid.NewGuid(),Guid.NewGuid(),"poolticket","1","9","{}","Pool","2026-11-05","10:00",1,0,1000,now.AddDays(1),now.AddDays(1).AddHours(2));
        request.RequireExternalReservation();var p=PoolPurchase.Create(Guid.NewGuid(),Guid.NewGuid().ToString(),"hash",[request],now,now.AddMinutes(10));p.AttachOrder(Guid.NewGuid(),"R-1");p.RecordExternalPayment();
        db.Add(p);await db.SaveChangesAsync();return request.Id;
    }
    private static readonly PoolSelection Selection=new("poolticket","1","9",1,0);
    [PostgresFact]
    public async Task Unknown_response_remains_unknown_after_restart_without_second_reserve()
    {
        var id=await Seed();var provider=new Provider{Fail=true};
        await using(var db=Db())Assert.Equal(PoolFulfillmentOutcome.Unknown,(await Service(db,provider).ReserveAsync(id,Selection,default,1000)).Outcome);
        provider.Fail=false;await using(var db=Db())Assert.Equal(PoolFulfillmentOutcome.Unknown,(await Service(db,provider).ReserveAsync(id,Selection,default,1000)).Outcome);
        Assert.Equal(1,provider.Calls);
    }
    [PostgresFact]
    public async Task Committed_sending_intent_after_crash_is_never_replayed()
    {
        var id=await Seed();await using(var db=Db()){db.Add(PoolTicketReservation.Begin(id,"09001769450",DateTimeOffset.UtcNow));await db.SaveChangesAsync();}
        var provider=new Provider();await using(var db=Db())Assert.Equal(PoolFulfillmentOutcome.Unknown,(await Service(db,provider).ReserveAsync(id,Selection,default,1000)).Outcome);Assert.Equal(0,provider.Calls);
    }
    [PostgresFact]
    public async Task Successful_reserve_is_replayed_locally_with_encrypted_voucher_and_same_reference()
    {
        var id=await Seed();var provider=new Provider();string? reference;
        await using(var db=Db()){var r=await Service(db,provider).ReserveAsync(id,Selection,default,1000);reference=r.ReservationId;Assert.Equal("voucher",r.Voucher);}
        await using(var db=Db()){var r=await Service(db,provider).ReserveAsync(id,Selection,default,1000);Assert.Equal(reference,r.ReservationId);var op=await db.PoolTicketReservations.SingleAsync(x=>x.RequestId==id);Assert.Equal("encrypted:voucher",op.VoucherCiphertext);}
        Assert.Equal(1,provider.Calls);
    }
    [PostgresFact]
    public async Task Mismatched_amount_keeps_reservation_evidence_and_requires_review_without_retry()
    {
        var id=await Seed();var provider=new Provider();
        await using(var db=Db()){Assert.Equal(PoolFulfillmentOutcome.Unknown,(await Service(db,provider).ReserveAsync(id,Selection,default,2000)).Outcome);var operation=await db.PoolTicketReservations.SingleAsync(x=>x.RequestId==id);Assert.NotNull(operation.ReservationId);Assert.NotNull(operation.VoucherCiphertext);}
        await using(var db=Db())Assert.Equal(PoolFulfillmentOutcome.Unknown,(await Service(db,provider).ReserveAsync(id,Selection,default,2000)).Outcome);Assert.Equal(1,provider.Calls);
    }
    [PostgresFact]
    public async Task Inconsistent_reserve_response_is_resolved_only_by_same_known_id_voucher_and_exact_info_without_second_reserve()
    {
        var id=await Seed();var provider=new Provider{InconsistentReserveAmount=true};
        await using(var db=Db())Assert.Equal(PoolFulfillmentOutcome.Unknown,(await Service(db,provider).ReserveAsync(id,Selection,default,1000)).Outcome);
        await using(var db=Db())Assert.Equal(PoolFulfillmentOutcome.Reserved,(await Service(db,provider).ReserveAsync(id,Selection,default,1000)).Outcome);
        Assert.Equal(1,provider.Calls);Assert.Equal(1,provider.InfoCalls);
    }
    [PostgresFact]
    public async Task Known_id_verification_never_accepts_different_identity_voucher_counts_or_date()
    {
        foreach(var changed in new[]{"id","voucher","counts","date"})
        {
            var id=await Seed();var provider=new Provider{InconsistentReserveAmount=true,BadInfo=changed};
            await using(var db=Db())Assert.Equal(PoolFulfillmentOutcome.Unknown,(await Service(db,provider).ReserveAsync(id,Selection,default,1000)).Outcome);
            await using(var db=Db())Assert.Equal(PoolFulfillmentOutcome.Unknown,(await Service(db,provider).ReserveAsync(id,Selection,default,1000)).Outcome);
            Assert.Equal(1,provider.Calls);
        }
    }
    [PostgresFact]
    public async Task Two_workers_use_source_lock_and_only_one_provider_reserve()
    {
        var id=await Seed();var provider=new Provider();var gate=new PostgresPoolMutationLock(Connection);
        async Task Run(){await using var lease=await gate.AcquireAsync(id,default);await using var db=Db();await Service(db,provider).ReserveAsync(id,Selection,default,1000);}
        await Task.WhenAll(Run(),Run());Assert.Equal(1,provider.Calls);
    }
    [PostgresFact]
    public async Task Catalogue_filters_canonical_geography_and_returns_ordered_prefix_without_upstream()
    {
        await Seed();await using var db=Db();var marker=Guid.NewGuid().ToString("N");var cityId=Random.Shared.Next(100000,999999);var now=DateTimeOffset.UtcNow;
        var cityKey=Guid.NewGuid().ToString("N");var geo=PoolTicketGeography.Create(cityKey);geo.Map("native-state","Province","City",811,cityId,now);db.Add(geo);
        foreach(var name in new[]{"پارک آبی ب ","پارک آبی ا "})
        {
            var venue=PoolCatalogEntry.Create("poolticket",Guid.NewGuid().ToString("N"));venue.Refresh(name+marker,"Province","City",null,null,2,null,1000,true,now);db.Add(venue);
            var content=PoolTicketContent.Create(venue.Id);content.Refresh("native-state",cityKey,"","address",null,35,51,now);db.Add(content);
        }
        var sync=await db.PoolTicketSyncStates.FindAsync("venues");if(sync==null){sync=PoolTicketSyncState.Create();db.Add(sync);}sync.Complete(now);await db.SaveChangesAsync();
        var source=new PoolsCatalogueSource(db,new Media(),new Monitor(),TimeProvider.System);
        var query=new CatalogueQuery(Q:marker,CityId:cityId,ProvinceId:811);
        Assert.Equal(2,(await source.SearchAsync(query with{Q="پارك آبي"},10,default)).TotalCount);
        var all=await source.SearchAsync(query,10,default);var prefix=await source.SearchAsync(query,1,default);
        Assert.Equal(2,all.TotalCount);Assert.Equal(all.Items[0].Id,prefix.Items.Single().Id);Assert.All(all.Items,x=>Assert.Equal(cityId,x.CityId));
        Assert.All(all.Items,x=>Assert.Equal(811,x.Locations!.Single().ProvinceId));
        var empty=await source.SearchAsync(query with{CityId=cityId+1},10,default);Assert.Empty(empty.Items);Assert.Equal(0,empty.TotalCount);
        var nearby=await source.SearchAsync(query with{Location="nearby",CityId=null,ProvinceId=null,Latitude=35,Longitude=51},10,default);
        Assert.All(nearby.Items,x=>Assert.Equal(0d,x.DistanceMeters));
    }
    private sealed class Monitor:IOptionsMonitor<PoolsOptions>
    {
        public PoolsOptions CurrentValue{get;}=new(){CatalogEnabled=true};public PoolsOptions Get(string? name)=>CurrentValue;
        public IDisposable? OnChange(Action<PoolsOptions,string?> listener)=>null;
    }
    private sealed class Media:IExternalMediaService
    {
        public Task<string?> GetPublicUrlAsync(string url,ExternalMediaOrigin? origin=null,CancellationToken ct=default)=>Task.FromResult<string?>(url);
        public Task<IReadOnlyList<string>> GetPublicUrlsAsync(IEnumerable<string> urls,ExternalMediaOrigin? origin=null,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<string>>(urls.ToArray());
    }
    private sealed class Protector:IPoolCodeProtector{public string Protect(string code)=>"encrypted:"+code;public string Unprotect(string value)=>value[10..];}
    private sealed class Factory(IPoolProvider provider):IPoolProviderFactory{public IReadOnlyList<IPoolProvider> Enabled()=>[provider];public IPoolProvider Get(string key)=>provider;}
    private sealed class Provider:IPoolOrganizationProvider
    {
        public string? BadInfo;public int Calls,InfoCalls;public bool Fail,InconsistentReserveAmount;public string Key=>"poolticket";public string Name=>"test";public PoolCapabilities Capabilities=>new(true,true,true,false,true,false,false);
        public async Task<PoolExternalReservation> ReserveAsync(PoolSelection selection,CancellationToken ct){Interlocked.Increment(ref Calls);await Task.Delay(15,ct);if(Fail)throw new PoolException("unknown",502);return new(Guid.NewGuid().ToString(),"voucher","Pool","Session",1,0,InconsistentReserveAmount?0:100,"2026-11-05","10:00",0);}
        public Task RegisterUserAsync(CancellationToken ct)=>throw new NotImplementedException();
        public Task<IReadOnlyList<PoolArea>> AreasAsync(string id,CancellationToken ct)=>throw new NotImplementedException();
        public Task<PoolProviderDetails> DetailsAsync(string id,CancellationToken ct)=>throw new NotImplementedException();
        public Task<PoolSessionAvailability> SessionInfoAsync(string pool,string row,CancellationToken ct)=>throw new NotImplementedException();
        public Task<PoolExternalReservation> ReservationInfoAsync(string id,CancellationToken ct){InfoCalls++;return Task.FromResult(new PoolExternalReservation(BadInfo=="id"?"different":id,BadInfo=="voucher"?"different":"voucher","Pool","Session",BadInfo=="counts"?2:1,0,100,BadInfo=="date"?"different":"2026-11-05","10:00",0));}
        public Task<PoolReservationHistory> ReservationHistoryAsync(int page,int limit,CancellationToken ct)=>throw new NotImplementedException();
        public Task<IReadOnlyList<PoolState>> StatesAsync(bool active,CancellationToken ct)=>throw new NotImplementedException();
        public Task<IReadOnlyList<PoolCity>> CitiesAsync(string? state,bool active,CancellationToken ct)=>throw new NotImplementedException();
        public Task<PoolCatalog> SearchAsync(PoolSearchQuery query,CancellationToken ct)=>throw new NotImplementedException();
        public Task<IReadOnlyList<PoolSession>> SessionsAsync(string pool,CancellationToken ct)=>throw new NotImplementedException();
        public Task<string> TermsAsync(string pool,CancellationToken ct)=>throw new NotImplementedException();
        public Task<PoolQuote> QuoteAsync(PoolSelection selection,CancellationToken ct)=>throw new NotImplementedException();
    }
}
