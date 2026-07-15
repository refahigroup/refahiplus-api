using Refahi.Modules.Store.Application.Contracts.Queries.Products.V2;
using Refahi.Modules.Store.Application.Features.Products.GetProductCatalogV2;
using Refahi.Modules.Store.Application.Features.Products.GetSyntheticOffersV2;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class SyntheticOfferQueryValidatorTests
{
    [Fact]
    public void Catalog_rejects_invalid_price_range_and_page_size()
    {
        var validator = new GetProductCatalogV2QueryValidator();
        var result = validator.Validate(new GetProductCatalogV2Query(
            ModuleId: 1,
            MinPriceMinor: 20,
            MaxPriceMinor: 10,
            PageSize: 31));

        Assert.Contains(result.Errors, x => x.PropertyName == string.Empty);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetProductCatalogV2Query.PageSize));
    }

    [Theory]
    [InlineData("StockProduct")]
    [InlineData("StockVariant")]
    [InlineData("ProductSession")]
    [InlineData("SessionVariant")]
    public void Offers_accepts_supported_offer_kinds(string offerKind)
    {
        var validator = new GetSyntheticOffersV2QueryValidator();
        var result = validator.Validate(new GetSyntheticOffersV2Query(1, OfferKind: offerKind));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Offers_rejects_invalid_usage_range_and_offer_kind()
    {
        var validator = new GetSyntheticOffersV2QueryValidator();
        var result = validator.Validate(new GetSyntheticOffersV2Query(
            1,
            OfferKind: "Unknown",
            UsageFrom: new DateOnly(2026, 7, 20),
            UsageTo: new DateOnly(2026, 7, 19)));

        Assert.Contains(result.Errors, x => x.PropertyName == nameof(GetSyntheticOffersV2Query.OfferKind));
        Assert.Contains(result.Errors, x => x.PropertyName == string.Empty);
    }

    [Fact]
    public void Offers_rejects_product_id_and_slug_together()
    {
        var validator = new GetSyntheticOffersV2QueryValidator();
        var result = validator.Validate(new GetSyntheticOffersV2Query(
            1,
            ProductId: Guid.NewGuid(),
            ProductSlug: "product"));

        Assert.Contains(result.Errors, x => x.PropertyName == string.Empty);
    }
}
