using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Cache;

namespace Refahi.Modules.Store.Application.Services;

internal sealed class ModuleResolver : IModuleResolver
{
    private readonly IStoreModuleRepository _repo;
    private readonly ICacheService _cache;

    public ModuleResolver(IStoreModuleRepository repo, ICacheService cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<int?> ResolveIdAsync(string slug, CancellationToken ct = default)
    {
        var cacheKey = $"store_module_slug:{slug.ToLowerInvariant()}";

        var cachedId = await _cache.GetAsync<int?>(cacheKey);
        if (cachedId.HasValue)
            return cachedId;

        var module = await _repo.GetBySlugAsync(slug.ToLowerInvariant(), ct);

        if (module is null || !module.IsActive)
            return null;

        await _cache.SetAsync(cacheKey, (int?)module.Id, TimeSpan.FromMinutes(5));
        return module.Id;
    }
}
