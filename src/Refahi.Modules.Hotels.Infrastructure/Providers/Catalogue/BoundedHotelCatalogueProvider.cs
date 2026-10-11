using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue;

// Cache only discovery. Transactional quotes always call the provider again.
internal sealed class BoundedHotelCatalogueProvider(IHotelCatalogueProvider inner, IMemoryCache cache, int timeoutSeconds, bool purchaseEnabled = true)
    : IHotelCatalogueProvider
{
    public string Name => inner.Name;
    public HotelProviderCapabilities Capabilities => inner.Capabilities with { Purchase = inner.Capabilities.Purchase && purchaseEnabled };
    private async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 60)));
        try { return await action(deadline.Token).WaitAsync(deadline.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new HotelCatalogueUnavailableException("مهلت پاسخ تأمین‌کننده هتل به پایان رسید."); }
    }
    public async Task<IReadOnlyList<ProviderCity>> CitiesAsync(CancellationToken ct)
    {
        var key = $"hotels:{Name}:cities";
        if (cache.TryGetValue<IReadOnlyList<ProviderCity>>(key, out var cities) && cities is not null) return cities;
        cities = await ReadAsync(inner.CitiesAsync, ct);
        cache.Set(key, cities, TimeSpan.FromDays(1));
        return cities;
    }
    public async Task<IReadOnlyList<ProviderHotelOffer>> SearchAsync(string cityId, HotelTravel travel, CancellationToken ct)
    {
        var key = $"hotels:{Name}:availability:{cityId}:{JsonSerializer.Serialize(travel)}";
        if (cache.TryGetValue<IReadOnlyList<ProviderHotelOffer>>(key, out var offers) && offers is not null) return offers;
        offers = await SearchFreshAsync(cityId, travel, ct);
        cache.Set(key, offers, TimeSpan.FromSeconds(60));
        return offers;
    }
    public Task<IReadOnlyList<ProviderHotelOffer>> SearchFreshAsync(string cityId, HotelTravel travel, CancellationToken ct) =>
        ReadAsync(token => inner.SearchFreshAsync(cityId, travel, token), ct);
    public Task<ProviderHotel> DetailsAsync(string id, string cityId, CancellationToken ct) =>
        ReadAsync(token => inner.DetailsAsync(id, cityId, token), ct);
    public Task<ProviderHotelOffer> ValidateOfferAsync(ProviderHotelOffer offer, HotelTravel travel, CancellationToken ct) =>
        ReadAsync(token => inner.ValidateOfferAsync(offer, travel, token), ct);
    public Task<HotelSubmitResult> SubmitAsync(HotelSubmitRequest request, CancellationToken ct) => inner.SubmitAsync(request, ct);
    public Task<HotelConfirmResult> ConfirmAsync(string referenceId, CancellationToken ct) => inner.ConfirmAsync(referenceId, ct);
}
