using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel.Contracts;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;

/// <summary>Short-lived discovery cache. Quote, reservation and fulfillment deliberately bypass it.</summary>
public sealed class TouristPanelReadCache(IMemoryCache cache, TouristPanelClient client, IOptions<TouristPanelOptions> options)
{
    private string Prefix => "touristpanel:" + options.Value.AccountKey + ":";
    public Task<TpProgramDto> GetDetailAsync(string product, CancellationToken ct) =>
        Get(Prefix + "detail:" + product, TimeSpan.FromMinutes(2), x => client.GetDetailAsync(product, null, x), ct);
    public Task<List<TpTicketTypeDto>> GetProgramTicketsAsync(string product, string seller, CancellationToken ct) =>
        Get(Prefix + $"program-tickets:{product}:{seller}", TimeSpan.FromSeconds(30), x => client.GetProgramTicketsAsync(product, seller, x), ct);
    public Task<List<TpEventSansDto>> GetEventsAsync(string product, string seller, DateOnly? start, DateOnly? end,
        string[]? types, bool includeTypes, CancellationToken ct) =>
        Get(Prefix + $"events:{product}:{seller}:{start:yyyyMMdd}:{end:yyyyMMdd}:{includeTypes}:{string.Join(',', types ?? [])}",
            TimeSpan.FromSeconds(30), x => client.GetEventsAsync(product, seller, start, end, types, includeTypes, x), ct);
    public Task<List<TpTicketTypeDto>> GetEventTicketsAsync(string eventId, string seller, string[]? types, CancellationToken ct) =>
        Get(Prefix + $"event-tickets:{eventId}:{seller}:{string.Join(',', types ?? [])}", TimeSpan.FromSeconds(30),
            x => client.GetEventTicketsAsync(eventId, seller, types, x), ct);

    private async Task<T> Get<T>(string key, TimeSpan duration, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        if (cache.TryGetValue<T>(key, out var existing) && existing is not null) return existing;
        var value = await factory(ct);
        cache.Set(key, value, duration);
        return value;
    }
}
