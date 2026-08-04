using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Application.Features.Products.AddProductVariant;
using Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariants;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class StorePricingRuleTests
{
    [Fact]
    public void Product_variant_accepts_equal_discounted_price()
    {
        var product = Product.Create(Guid.NewGuid(), "محصول", $"product-{Guid.NewGuid():N}");

        var variant = product.AddVariant([], 1, 10_000, 10_000);

        Assert.Equal(10_000, variant.DiscountedPriceMinor);
        Assert.Equal(10_000, variant.EffectivePriceMinor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(10_001L)]
    public void Product_variant_rejects_invalid_discounted_price(long? discountedPriceMinor)
    {
        var product = Product.Create(Guid.NewGuid(), "محصول", $"product-{Guid.NewGuid():N}");

        var exception = Assert.Throws<StoreDomainException>(
            () => product.AddVariant([], 1, 10_000, discountedPriceMinor));

        Assert.Equal("INVALID_DISCOUNTED_PRICE", exception.ErrorCode);
    }

    [Fact]
    public void Shop_product_and_variant_offering_accept_equal_discounted_price()
    {
        var shopProduct = ShopProduct.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10_000,
            10_000);

        var offering = shopProduct.AddVariantOffering(
            Guid.NewGuid(),
            10_000,
            10_000,
            isActive: true);

        Assert.Equal(10_000, shopProduct.DiscountedPrice);
        Assert.Equal(10_000, offering.DiscountedPriceMinor);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(10_001L)]
    public void Shop_product_rejects_invalid_discounted_price(long discountedPrice)
    {
        var exception = Assert.Throws<StoreDomainException>(
            () => ShopProduct.Create(Guid.NewGuid(), Guid.NewGuid(), 10_000, discountedPrice));

        Assert.Equal("INVALID_DISCOUNTED_PRICE", exception.ErrorCode);
    }

    [Fact]
    public void Add_variant_validator_requires_discounted_price_and_accepts_equality()
    {
        var validator = new AddProductVariantCommandValidator();
        var valid = new AddProductVariantCommand(
            Guid.NewGuid(), [], null, 1, 10_000, 10_000, null);
        var missing = valid with { DiscountedPriceMinor = null };
        var greater = valid with { DiscountedPriceMinor = 10_001 };

        Assert.True(validator.Validate(valid).IsValid);
        Assert.Contains(
            validator.Validate(missing).Errors,
            error => error.PropertyName == nameof(valid.DiscountedPriceMinor)
                     && error.ErrorMessage.Contains("الزامی"));
        Assert.Contains(
            validator.Validate(greater).Errors,
            error => error.ErrorMessage.Contains("نباید بیشتر"));
    }

    [Fact]
    public void Shop_variant_validator_requires_discounted_price_and_accepts_equality()
    {
        var validator = new UpsertShopProductVariantCommandValidator();
        var valid = new UpsertShopProductVariantCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10_000, 10_000, true);
        var missing = valid with { DiscountedPriceMinor = null };

        Assert.True(validator.Validate(valid).IsValid);
        Assert.Contains(
            validator.Validate(missing).Errors,
            error => error.PropertyName == nameof(valid.DiscountedPriceMinor)
                     && error.ErrorMessage.Contains("الزامی"));
    }

}
