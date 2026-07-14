using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class VariantAttributeUpdateTests
{
    [Fact]
    public void Attribute_name_and_sort_order_are_updated_and_trimmed()
    {
        var product = CreateProduct();
        var attribute = product.AddVariantAttribute("رنگ", 1);

        product.UpdateVariantAttribute(attribute.Id, "  رنگ اصلی  ", 4);

        Assert.Equal("رنگ اصلی", attribute.Name);
        Assert.Equal(4, attribute.SortOrder);
    }

    [Fact]
    public void Renaming_attribute_to_an_existing_name_is_rejected_case_insensitively()
    {
        var product = CreateProduct();
        product.AddVariantAttribute("Color", 1);
        var attribute = product.AddVariantAttribute("Size", 2);

        var exception = Assert.Throws<StoreDomainException>(() =>
            product.UpdateVariantAttribute(attribute.Id, " color ", 3));

        Assert.Equal("VARIANT_ATTRIBUTE_ALREADY_EXISTS", exception.ErrorCode);
    }

    [Fact]
    public void Updating_unknown_attribute_is_rejected()
    {
        var product = CreateProduct();

        var exception = Assert.Throws<StoreDomainException>(() =>
            product.UpdateVariantAttribute(Guid.NewGuid(), "رنگ", 0));

        Assert.Equal("VARIANT_ATTRIBUTE_NOT_FOUND", exception.ErrorCode);
    }

    [Fact]
    public void Attribute_value_and_sort_order_are_updated_and_trimmed()
    {
        var product = CreateProduct();
        var attribute = product.AddVariantAttribute("رنگ", 1);
        var value = product.AddVariantAttributeValue(attribute.Id, "قرمز", 1);

        product.UpdateVariantAttributeValue(attribute.Id, value.Id, "  آبی  ", 5);

        Assert.Equal("آبی", value.Value);
        Assert.Equal(5, value.SortOrder);
    }

    [Fact]
    public void Updating_value_of_unknown_attribute_is_rejected()
    {
        var product = CreateProduct();

        var exception = Assert.Throws<StoreDomainException>(() =>
            product.UpdateVariantAttributeValue(Guid.NewGuid(), Guid.NewGuid(), "آبی", 0));

        Assert.Equal("VARIANT_ATTRIBUTE_NOT_FOUND", exception.ErrorCode);
    }

    [Fact]
    public void Updating_unknown_value_is_rejected()
    {
        var product = CreateProduct();
        var attribute = product.AddVariantAttribute("رنگ", 1);

        var exception = Assert.Throws<StoreDomainException>(() =>
            product.UpdateVariantAttributeValue(attribute.Id, Guid.NewGuid(), "آبی", 0));

        Assert.Equal("VARIANT_ATTRIBUTE_VALUE_NOT_FOUND", exception.ErrorCode);
    }

    private static Product CreateProduct()
        => Product.Create(Guid.NewGuid(), "محصول", $"product-{Guid.NewGuid():N}");
}
