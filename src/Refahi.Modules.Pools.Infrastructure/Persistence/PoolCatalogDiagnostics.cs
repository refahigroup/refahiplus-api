using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
namespace Refahi.Modules.Pools.Infrastructure.Persistence;
public sealed class PoolCatalogDiagnostics(PoolsDbContext db,IOptionsMonitor<PoolsOptions> options,IOptions<PoolTicketOptions> provider,TimeProvider clock):IPoolCatalogDiagnostics
{
    public async Task<PoolCatalogDiagnosticsDto> ReadAsync(CancellationToken ct)
    {
        var o=options.CurrentValue;
        if(!o.CatalogEnabled)return new("Disabled",0,0,0,0,null,null);
        if(string.IsNullOrWhiteSpace(provider.Value.ClientId))return new("NotConfigured",0,0,0,0,null,null);
        var now=clock.GetUtcNow();var venueCutoff=now.AddSeconds(-o.VenueSyncSeconds*2d);var sessionCutoff=now.AddSeconds(-o.SessionSyncSeconds*2d);
        var venueCount=await db.Venues.CountAsync(x=>x.IsActive,ct);
        var sessionCount=await db.Sessions.CountAsync(x=>x.IsActive,ct);
        var staleVenues=await db.Venues.CountAsync(x=>x.IsActive&&x.FetchedAt<venueCutoff,ct);
        var staleSessions=await db.Venues.CountAsync(x=>x.IsActive&&(x.SessionsFetchedAt==null||x.SessionsFetchedAt<sessionCutoff),ct);
        var lastVenue=await db.Venues.Select(x=>(DateTimeOffset?)x.FetchedAt).MaxAsync(ct);
        var lastSession=await db.Venues.Select(x=>x.SessionsFetchedAt).MaxAsync(ct);
        return new(lastVenue==null?"NeverSynced":staleVenues>0||staleSessions>0?"Degraded":"Ready",venueCount,sessionCount,staleVenues,staleSessions,lastVenue,lastSession);
    }
}
