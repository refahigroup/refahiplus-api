using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel.Contracts;

namespace Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;

/// <summary>Short-lived discovery cache. Quote, reservation and fulfillment deliberately bypass it.</summary>
public sealed class TouristPanelReadCache(IMemoryCache cache, TouristPanelClient client, IOptions<TouristPanelOptions> options)
{
    private string Prefix => "touristpanel:" + options.Value.AccountKey + ":";
    public Task<TpProgramDto> GetDetailAsync(string program, CancellationToken ct) =>
        Get(Prefix + "detail:" + program, TimeSpan.FromMinutes(2), x => client.GetDetailAsync(program, null, x), ct);
    public Task<List<TpTicketTypeDto>> GetProgramTicketsAsync(string program, string supplier, CancellationToken ct) =>
        Get(Prefix + $"program-tickets:{program}:{supplier}", TimeSpan.FromSeconds(30), x => client.GetProgramTicketsAsync(program, supplier, x), ct);
    public Task<List<TpEventSansDto>> GetEventsAsync(string program, string supplier, DateOnly? start, DateOnly? end,
        string[]? types, bool includeTypes, CancellationToken ct) =>
        Get(Prefix + $"events:{program}:{supplier}:{start:yyyyMMdd}:{end:yyyyMMdd}:{includeTypes}:{string.Join(',', types ?? [])}",
            TimeSpan.FromSeconds(30), x => client.GetEventsAsync(program, supplier, start, end, types, includeTypes, x), ct);
    public Task<List<TpTicketTypeDto>> GetEventTicketsAsync(string eventId, string supplier, string[]? types, CancellationToken ct) =>
        Get(Prefix + $"event-tickets:{eventId}:{supplier}:{string.Join(',', types ?? [])}", TimeSpan.FromSeconds(30),
            x => client.GetEventTicketsAsync(eventId, supplier, types, x), ct);

    private async Task<T> Get<T>(string key, TimeSpan duration, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        if (cache.TryGetValue<T>(key, out var existing) && existing is not null) return existing;
        var value = await factory(ct);
        cache.Set(key, value, duration);
        return value;
    }
}
