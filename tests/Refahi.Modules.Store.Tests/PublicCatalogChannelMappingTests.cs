using Refahi.Modules.Store.Application.Features.Catalog;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class PublicCatalogChannelMappingTests
{
    [Fact]
    public void MapPrice_InPersonOnly_ReturnsManualPriceMode()
    {
        var candidate = Candidate(SalesChannel.InPerson, 8_000_000);

        var result = PublicCatalogMapping.MapPrice([candidate]);

        Assert.Equal("InPerson", result.DisplayMode);
        Assert.Equal(0, result.MinFinalPriceMinor);
        Assert.Equal(candidate.ShopId, result.DefaultShopId);
    }

    [Fact]
    public void MapGroup_WhenOnlineAndInPersonExist_PrefersOnlinePricing()
    {
        var productId = Guid.NewGuid();
        var inPerson = Candidate(SalesChannel.InPerson, 1_000_000, productId);
        var online = Candidate(SalesChannel.Online, 2_000_000, productId);
        var group = new[] { inPerson, online }.GroupBy(x => x.ProductId).Single();

        var result = PublicCatalogMapping.MapGroup(group, new FakePathService());

        Assert.Equal("Fixed", result.Price.DisplayMode);
        Assert.Equal(2_000_000, result.Price.MinFinalPriceMinor);
        Assert.Equal(online.ShopId, result.Price.DefaultShopId);
    }

    private static PublicCatalogOfferCandidate Candidate(
        SalesChannel channel,
        long price,
        Guid? productId = null)
        => new(
            Guid.NewGuid(),
            productId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            4,
            "محصول",
            "product",
            null,
            ProductType.Service,
            SalesModel.Unlimited,
            FulfillmentMethod.Pickup,
            DateTimeOffset.UtcNow,
            null,
            "فروشگاه",
            "shop",
            channel,
            price,
            0,
            price,
            null,
            null,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

    private sealed class FakePathService : IPathService
    {
        public string MakeAbsoluteMediaUrl(string mediaPath) => mediaPath;
    }
}
