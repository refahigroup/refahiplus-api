namespace Refahi.Modules.Store.Domain.Entities;

/// <summary>
/// Junction between ProductVariant and VariantAttributeValue.
/// Each variant is a unique combination of attribute values, e.g. { سایز:XL, رنگ:مشکی }.
/// </summary>
public sealed class ProductVariantCombination
{
    private ProductVariantCombination() { }

    public Guid Id { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public Guid VariantAttributeId { get; private set; }
    public Guid VariantAttributeValueId { get; private set; }

    internal static ProductVariantCombination Create(
        Guid variantId, Guid attributeId, Guid valueId)
        => new()
        {
            Id = Guid.NewGuid(),
            ProductVariantId = variantId,
            VariantAttributeId = attributeId,
            VariantAttributeValueId = valueId
        };
}
