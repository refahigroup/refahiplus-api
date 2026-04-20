namespace Refahi.Modules.Store.Domain.Entities;

public sealed class VariantAttributeValue
{
    private VariantAttributeValue() { }

    public Guid Id { get; private set; }
    public Guid VariantAttributeId { get; private set; }
    public string Value { get; private set; } = string.Empty;      // "XL", "مشکی", "نخ"
    public int SortOrder { get; private set; }

    internal static VariantAttributeValue Create(Guid attributeId, string value, int sortOrder)
        => new()
        {
            Id = Guid.NewGuid(),
            VariantAttributeId = attributeId,
            Value = value.Trim(),
            SortOrder = sortOrder
        };
}
