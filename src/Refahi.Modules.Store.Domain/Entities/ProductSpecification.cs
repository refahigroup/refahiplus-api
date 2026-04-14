namespace Refahi.Modules.Store.Domain.Entities;

public sealed class ProductSpecification
{
    private ProductSpecification() { }

    public int Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Key { get; private set; } = string.Empty;     // "کد محصول", "جیب", "قد لباس"
    public string Value { get; private set; } = string.Empty;   // "10000002504610", "دارد", "۷۰ سانتی متر"
    public int SortOrder { get; private set; }

    internal static ProductSpecification Create(Guid productId, string key, string value, int sortOrder)
        => new() { ProductId = productId, Key = key, Value = value, SortOrder = sortOrder };
}
