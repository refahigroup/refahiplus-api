using Microsoft.Extensions.Caching.Memory;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Services;

internal sealed class ModuleResolver : IModuleResolver
{
    private readonly IStoreModuleRepository _repo;
    private readonly IMemoryCache _cache;

    public ModuleResolver(IStoreModuleRepository repo, IMemoryCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<int?> ResolveIdAsync(string slug, CancellationToken ct = default)
    {
        var cacheKey = $"store_module_slug:{slug.ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out int cachedId))
            return cachedId;

        var module = await _repo.GetBySlugAsync(slug.ToLowerInvariant(), ct);

        if (module is null || !module.IsActive)
            return null;

        _cache.Set(cacheKey, module.Id, TimeSpan.FromMinutes(5));
        return module.Id;
    }
}
