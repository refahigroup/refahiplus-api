using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Persistence;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;

public sealed class HotelInventoryStore(IServiceScopeFactory scopes)
{
    // Independent context: providers can run concurrently without sharing the request's DbContext.
    public async Task<string> GetAsync(string provider, string cityId, Func<Task<string>> fetch, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotelsDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IHotelCatalogueRepository>();
        await using var held = await repository.LockAsync($"inventory:{provider}:{cityId}", ct);
        var row = await db.Set<HotelProviderInventory>().SingleOrDefaultAsync(x => x.Provider == provider && x.CityId == cityId, ct);
        if (row is not null && row.RefreshedAtUtc > DateTime.UtcNow.AddDays(-1)) return row.PayloadJson;
        var payload = await fetch();
        if (row is null) db.Add(HotelProviderInventory.Create(provider, cityId, payload));
        else row.Refresh(payload);
        await db.SaveChangesAsync(ct);
        return payload;
    }
}
