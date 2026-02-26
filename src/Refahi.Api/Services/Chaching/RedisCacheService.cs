using Refahi.Shared.Services.Cache;
using StackExchange.Redis;
using System.Text.Json;


namespace Refahi.Api.Services.Chaching;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;

    public RedisCacheService(IConnectionMultiplexer multiplexer)
    {
        _db = multiplexer.GetDatabase();
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, ttl);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var json = await _db.StringGetAsync(key);
        if (json.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(json.ToString());
    }

    public Task RemoveAsync(string key)
        => _db.KeyDeleteAsync(key);

    public Task<bool> ExistsAsync(string key)
        => _db.KeyExistsAsync(key);
}
