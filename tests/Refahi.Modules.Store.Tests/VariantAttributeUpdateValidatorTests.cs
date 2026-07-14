using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Modules.Store.Application.Features.Products.UpdateVariantAttribute;
using Refahi.Modules.Store.Application.Features.Products.UpdateVariantAttributeValue;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class VariantAttributeUpdateValidatorTests
{
    private readonly UpdateVariantAttributeCommandValidator _attributeValidator = new();
    private readonly UpdateVariantAttributeValueCommandValidator _valueValidator = new();

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Attribute_name_length_is_validated(int length, bool expectedValidity)
    {
        var command = new UpdateVariantAttributeCommand(
            Guid.NewGuid(), Guid.NewGuid(), new string('a', length), 0);

        Assert.Equal(expectedValidity, _attributeValidator.Validate(command).IsValid);
    }

    [Fact]
    public void Empty_attribute_name_and_negative_sort_order_are_rejected()
    {
        var command = new UpdateVariantAttributeCommand(
            Guid.NewGuid(), Guid.NewGuid(), string.Empty, -1);

        var result = _attributeValidator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.SortOrder));
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Attribute_value_length_is_validated(int length, bool expectedValidity)
    {
        var command = new UpdateVariantAttributeValueCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', length), 0);

        Assert.Equal(expectedValidity, _valueValidator.Validate(command).IsValid);
    }

    [Fact]
    public void Empty_attribute_value_and_negative_sort_order_are_rejected()
    {
        var command = new UpdateVariantAttributeValueCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), string.Empty, -1);

        var result = _valueValidator.Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.Value));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(command.SortOrder));
    }
}
