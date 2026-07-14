using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Domain.Entities;

public sealed class VariantAttribute
{
    private VariantAttribute() { _values = new List<VariantAttributeValue>(); }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;       // "سایز", "رنگ", "جنس"
    public int SortOrder { get; private set; }

    private readonly List<VariantAttributeValue> _values;
    public IReadOnlyList<VariantAttributeValue> Values => _values.AsReadOnly();

    internal static VariantAttribute Create(Guid productId, string name, int sortOrder)
        => new()
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Name = name.Trim(),
            SortOrder = sortOrder
        };

    internal VariantAttributeValue AddValue(string value, int sortOrder)
    {
        var v = VariantAttributeValue.Create(Id, value, sortOrder);
        _values.Add(v);
        return v;
    }

    internal void Update(string name, int sortOrder)
    {
        Name = name.Trim();
        SortOrder = sortOrder;
    }

    internal void UpdateValue(Guid valueId, string value, int sortOrder)
    {
        var attributeValue = _values.FirstOrDefault(v => v.Id == valueId)
            ?? throw new StoreDomainException("مقدار ویژگی تنوع یافت نشد", "VARIANT_ATTRIBUTE_VALUE_NOT_FOUND");

        attributeValue.Update(value, sortOrder);
    }

    internal void RemoveValue(Guid valueId)
    {
        var value = _values.FirstOrDefault(v => v.Id == valueId)
            ?? throw new StoreDomainException("مقدار ویژگی تنوع یافت نشد", "VARIANT_ATTRIBUTE_VALUE_NOT_FOUND");

        _values.Remove(value);
    }
}
