using System.Text.Json;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Abstraction;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;
using Xunit;

namespace Refahi.Modules.Commerce.Tests;

public sealed class AabsarCommerceProviderTests
{
    [Fact]
    public async Task Showtime_maps_to_one_offer_with_adult_and_child_options()
    {
        var api = new FakeAabsarApi();
        var provider = new AabsarCommerceProvider(api);

        var page = await provider.GetProductsAsync(new(null, null, null, 1, 20), default);

        var product = Assert.Single(page.Items);
        Assert.Equal("event-opaque", product.ProductKey);
        var offer = Assert.Single(product.Offers);
        Assert.Equal("showtime-opaque", offer.OfferKey);
        Assert.Collection(offer.PurchaseOptions,
            adult => { Assert.Equal("adult", adult.Key); Assert.Equal(120_000, adult.PriceMinor); },
            child => { Assert.Equal("child", child.Key); Assert.Equal(70_000, child.PriceMinor); });
        Assert.Contains("بانوان", offer.Title);
        Assert.Equal("entertainment.waterpark", offer.CategoryCode);
    }

    [Fact]
    public async Task Quote_revalidates_capacity_and_uses_current_price()
    {
        var provider = new AabsarCommerceProvider(new FakeAabsarApi());
        var quote = await provider.QuoteAsync(new("event-opaque", "showtime-opaque", "child", 2), default);
        Assert.Equal(75_000, quote.UnitPriceMinor);
        Assert.Equal(5, quote.Capacity);
        Assert.Equal("مجموعه آبی آبسار", quote.SellerTitle);
        Assert.False(string.IsNullOrWhiteSpace(quote.ProductImageUrl));
        Assert.Equal(75_000, quote.OriginalUnitPriceMinor);
        Assert.Contains("showtime_id", quote.ProviderPayloadJson);
    }

    [Fact]
    public async Task Seller_lookup_maps_address_and_location_and_rejects_another_seller()
    {
        var provider = new AabsarCommerceProvider(new FakeAabsarApi());

        var seller = await provider.GetSellerAsync("AABSAR", default);

        Assert.NotNull(seller);
        Assert.Equal("اصفهان", seller.Address.City);
        Assert.InRange(seller.Address.Location!.Lat, -90, 90);
        Assert.InRange(seller.Address.Location.Lng, -180, 180);
        Assert.Null(await provider.GetSellerAsync("another-seller", default));
    }

    [Fact]
    public async Task Product_catalog_is_scoped_to_requested_seller()
    {
        var provider = new AabsarCommerceProvider(new FakeAabsarApi());

        var page = await provider.GetProductsAsync(new(null, null, "another-seller", 1, 20), default);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
    }

    private sealed class FakeAabsarApi : IAabsarApiClient
    {
        public Task<AabsarHealthResponse> HealthCheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AabsarHealthResponse());
        public Task<AabsarApiResponse<IReadOnlyList<AabsarShowtimeDto>>> GetShowtimesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AabsarApiResponse<IReadOnlyList<AabsarShowtimeDto>> { Data = [new()
            {
                Id = "showtime-opaque", EventId = "event-opaque", EventTitle = "پارک آبی", Title = "سانس عصر",
                VendorName = "آبسار", Gender = "female", Time = 1_800_000_000_000, Capacity = 10,
                AdultPrice = 120_000, ChildPrice = 70_000, AdultOldPrice = 130_000
            }] });
        public Task<AabsarApiResponse<AabsarShowtimeCapacityDto>> CheckShowtimeAsync(AabsarCheckShowtimeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AabsarApiResponse<AabsarShowtimeCapacityDto> { Data = new()
            { Id = request.ShowtimeId, Status = "active", Capacity = 5, AdultPrice = 125_000, ChildPrice = 75_000 } });
        public Task<AabsarApiResponse<AabsarCreateOrderResultDto>> CreateOrderAsync(AabsarCreateOrderRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AabsarApiResponse<JsonElement?>> CancelTicketsAsync(AabsarCancelTicketsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
