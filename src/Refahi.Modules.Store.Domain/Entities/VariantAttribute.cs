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
}
