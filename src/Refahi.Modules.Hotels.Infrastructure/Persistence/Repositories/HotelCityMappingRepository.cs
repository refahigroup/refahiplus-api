using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;

namespace Refahi.Modules.Hotels.Infrastructure.Persistence.Repositories;

public sealed class HotelCityMappingRepository(IServiceScopeFactory scopes) : IHotelCityMappingRepository
{
    public async Task<IReadOnlyList<string>> SnappTripCityIdsAsync(int referenceCityId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotelsDbContext>();
        return await db.Set<SnappTripCityMapping>().AsNoTracking().Where(x => x.CityId == referenceCityId)
            .OrderBy(x => x.ProviderCityId).Select(x => x.ProviderCityId).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<string>> LamasoCityIdsAsync(int referenceCityId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotelsDbContext>();
        return await db.Set<LamasoCityMapping>().AsNoTracking().Where(x => x.CityId == referenceCityId && x.IsEnabled)
            .OrderBy(x => x.ProviderCityId).Select(x => x.ProviderCityId).ToListAsync(ct);
    }
}
