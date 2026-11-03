using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Features.Catalog.SyncVenueSessions;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
using Xunit;
namespace Refahi.Modules.Pools.Tests;

public sealed class PoolOrganizationSyncTests
{
    [Fact]
    public async Task V1_refresh_preserves_unique_legacy_session_guid_and_pool_rules_without_v2_terms_dependency()
    {
        var repo = new Repository();
        var old = PoolSessionEntry.Create(repo.Venue.Id, "7", "91"); repo.Rows.Add(old);
        var handler = Handler(repo);
        await handler.Handle(new(repo.Venue.Id), default);
        await handler.Handle(new(repo.Venue.Id), default);
        var row = Assert.Single(repo.Rows);
        Assert.Equal(old.Id, row.Id); Assert.Equal("7", row.ExternalGroupId); Assert.True(row.IsActive);
        Assert.Equal(new DateTimeOffset(2030, 1, 1, 6, 30, 0, TimeSpan.Zero), row.StartsAtUtc);
        Assert.Equal("<p>قوانین مجموعه</p>", repo.Venue.TermsHtml); Assert.Equal(2, repo.Saves);
    }
    [Theory]
    [InlineData("8")]
    [InlineData("")]
    public async Task Ambiguous_legacy_identity_is_not_merged_or_published(string secondGroup)
    {
        var repo = new Repository(); repo.Rows.Add(PoolSessionEntry.Create(repo.Venue.Id,"7","91")); repo.Rows.Add(PoolSessionEntry.Create(repo.Venue.Id,secondGroup,"91"));
        Assert.Equal(502,(await Assert.ThrowsAsync<PoolException>(()=>Handler(repo).Handle(new(repo.Venue.Id),default))).Status);
        Assert.Equal(0,repo.Saves);
    }
    private static SyncPoolVenueSessionsHandler Handler(Repository repo)
    {
        var options=Options.Create(new PoolTicketOptions{ClientId="client",AccessToken="token"});
        var provider=new PoolTicketProvider(new(new HttpClient(new Wire()){BaseAddress=new("https://example.test/")},options,NullLogger<PoolTicketClient>.Instance),options);
        return new(new Factory(provider),repo,new Gate(),new PoolSessionTimeParser(),TimeProvider.System);
    }
    private sealed class Wire:HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("/api/v1/org/saens/list",request.RequestUri!.AbsolutePath);
            var body=await request.Content!.ReadAsStringAsync(ct);
            var json=body.Contains("gender=1") ? "{\"status\":1,\"saens\":[]}" : """{"status":1,"saens":[{"saens_row_id":91,"saens_name":"سانس","status":1,"status_sell":1,"adult_sale_status":1,"child_sale_status":0,"remain_capacity":5,"price_sale_adult":100,"price_sale_child":0,"price_market_adult":120,"price_market_child":0,"start_date":"2030-01-01","time":"10:00","start_datetime":"2030-01-01 10:00:00","finish_datetime":"2030-01-01 12:00:00"}]}""";
            return new(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
        }
    }
    private sealed class Factory(IPoolProvider provider):IPoolProviderFactory
    {
        public IReadOnlyList<IPoolProvider> Enabled()=>[provider];
        public IPoolProvider Get(string key)=>provider;
    }
    private sealed class Gate:IPoolMutationLock
    {
        public Task<IAsyncDisposable> AcquireAsync(Guid id,CancellationToken ct)=>Task.FromResult<IAsyncDisposable>(new Lease());
        private sealed class Lease:IAsyncDisposable { public ValueTask DisposeAsync()=>ValueTask.CompletedTask; }
    }
    private sealed class Repository:IPoolCatalogRepository
    {
        public PoolCatalogEntry Venue {get;}=PoolCatalogEntry.Create("poolticket","72");
        public List<PoolSessionEntry> Rows {get;}=[];
        public int Saves;
        public Repository(){Venue.Refresh("استخر","استان","شهر",null,null,2,null,null,true,DateTimeOffset.UtcNow);Venue.RefreshSessions("<p>قوانین مجموعه</p>",DateTimeOffset.UtcNow);}
        public Task<PoolCatalogEntry?> PoolAsync(Guid id,CancellationToken ct)=>Task.FromResult<PoolCatalogEntry?>(Venue.Id==id?Venue:null);
        public Task<PoolSessionEntry?> SessionAsync(Guid id,CancellationToken ct)=>Task.FromResult(Rows.SingleOrDefault(x=>x.Id==id));
        public Task<IReadOnlyList<PoolCatalogEntry>> ProviderPoolsAsync(string provider,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PoolCatalogEntry>>([Venue]);
        public Task<IReadOnlyList<PoolSessionEntry>> PoolSessionsAsync(Guid poolId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PoolSessionEntry>>(Rows.ToArray());
        public Task AddPoolAsync(PoolCatalogEntry pool,CancellationToken ct)=>throw new NotSupportedException();
        public Task AddSessionAsync(PoolSessionEntry session,CancellationToken ct){Rows.Add(session);return Task.CompletedTask;}
        public Task SaveAsync(CancellationToken ct){Saves++;return Task.CompletedTask;}
    }
}
