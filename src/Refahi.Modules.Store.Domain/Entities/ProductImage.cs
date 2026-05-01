namespace Refahi.Modules.Store.Domain.Entities;

public sealed class ProductImage
{
    private ProductImage() { }

    public int Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public bool IsMain { get; private set; }
    public int SortOrder { get; private set; }

    internal static ProductImage Create(Guid productId, string imageUrl, bool isMain, int sortOrder)
        => new() { ProductId = productId, ImageUrl = imageUrl, IsMain = isMain, SortOrder = sortOrder };

    internal void SetMain(bool isMain) => IsMain = isMain;

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
