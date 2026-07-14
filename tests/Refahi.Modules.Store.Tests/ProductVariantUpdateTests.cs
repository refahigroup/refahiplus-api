using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Application.Features.Products.UpdateProductVariant;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class ProductVariantUpdateTests
{
    [Fact]
    public void Variant_details_and_combinations_are_updated()
    {
        var product = CreateProduct();
        var attribute = product.AddVariantAttribute("رنگ", 0);
        var red = product.AddVariantAttributeValue(attribute.Id, "قرمز", 0);
        var blue = product.AddVariantAttributeValue(attribute.Id, "آبی", 1);
        var variant = product.AddVariant(
            [(attribute.Id, red.Id)],
            2,
            1_000,
            salesModel: SalesModel.StockBased);

        product.UpdateVariant(
            variant.Id,
            [(attribute.Id, blue.Id)],
            5,
            2_000,
            1_500,
            "/media/blue.png",
            "BLUE",
            salesModel: SalesModel.StockBased);

        Assert.Equal(5, variant.StockCount);
        Assert.Equal(2_000, variant.PriceMinor);
        Assert.Equal(1_500, variant.DiscountedPriceMinor);
        Assert.Equal("BLUE", variant.SKU);
        Assert.Equal("/media/blue.png", variant.ImageUrl);
        Assert.True(variant.IsAvailable);
        var combination = Assert.Single(variant.Combinations);
        Assert.Equal(blue.Id, combination.VariantAttributeValueId);
    }

    [Fact]
    public void Updating_unknown_variant_is_rejected()
    {
        var product = CreateProduct();

        var exception = Assert.Throws<StoreDomainException>(() => product.UpdateVariant(
            Guid.NewGuid(),
            [],
            0,
            1_000));

        Assert.Equal("VARIANT_NOT_FOUND", exception.ErrorCode);
    }

    [Fact]
    public void Update_validator_rejects_empty_combinations_and_equal_discount()
    {
        var validator = new UpdateProductVariantCommandValidator();
        var command = new UpdateProductVariantCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [],
            null,
            0,
            1_000,
            1_000,
            null);

        var result = validator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Combinations));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("کمتر از قیمت اصلی"));
    }

    [Fact]
    public void Update_validator_accepts_valid_variant()
    {
        var validator = new UpdateProductVariantCommandValidator();
        var command = new UpdateProductVariantCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new VariantCombinationInput(Guid.NewGuid(), Guid.NewGuid())],
            null,
            1,
            1_000,
            900,
            "SKU");

        Assert.True(validator.Validate(command).IsValid);
    }

    private static Product CreateProduct()
        => Product.Create(Guid.NewGuid(), "محصول", $"product-{Guid.NewGuid():N}");
}
