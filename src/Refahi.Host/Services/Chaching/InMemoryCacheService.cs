using Microsoft.Extensions.Caching.Memory;
using Refahi.Shared.Services.Cache;

namespace Refahi.Host.Services.Chaching;

public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public InMemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        _cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        if (_cache.TryGetValue(key, out var obj))
            return Task.FromResult((T?)obj);

        return Task.FromResult(default(T?));
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(_cache.TryGetValue(key, out _));
    }
}
