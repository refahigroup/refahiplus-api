namespace Refahi.Modules.Pools.Domain;

public sealed class AabsarCart
{
    private readonly List<AabsarCartItem> _items = [];
    private AabsarCart() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<AabsarCartItem> Items => _items.AsReadOnly();

    public static AabsarCart Create(Guid userId)
    {
        if (userId == Guid.Empty) throw new AabsarCartException("شناسه کاربر معتبر نیست", "INVALID_USER");
        var now = DateTimeOffset.UtcNow;
        return new AabsarCart { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now, UpdatedAt = now };
    }

    public void AddOrReplace(AabsarCartItemSnapshot item)
    {
        if (item.Quantity is <= 0 or > 100 || item.ExpectedUnitPriceMinor <= 0)
            throw new AabsarCartException("تعداد یا قیمت آیتم معتبر نیست", "INVALID_CART_ITEM");
        var existing = _items.FirstOrDefault(x => x.IdentityEquals(item));
        if (existing is null) _items.Add(AabsarCartItem.Create(Id, item));
        else existing.Replace(item);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateQuantity(Guid itemId, int quantity)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new AabsarCartException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        item.ChangeQuantity(quantity);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Remove(Guid itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new AabsarCartException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        _items.Remove(item);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Clear() { _items.Clear(); UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class AabsarCartItem
{
    private AabsarCartItem() { }
    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string SellerKey { get; private set; } = string.Empty;
    public string ProductKey { get; private set; } = string.Empty;
    public string OfferKey { get; private set; } = string.Empty;
    public string PurchaseOptionKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string SellerTitle { get; private set; } = string.Empty;
    public string ProductTitle { get; private set; } = string.Empty;
    public string? ProductImageUrl { get; private set; }
    public string OptionTitle { get; private set; } = string.Empty;
    public string OfferTitle { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public long ExpectedUnitPriceMinor { get; private set; }
    public long OriginalUnitPriceMinor { get; private set; }
    public bool IsAvailable { get; private set; }

    internal static AabsarCartItem Create(Guid cartId, AabsarCartItemSnapshot x) => new()
    {
        Id = Guid.NewGuid(), CartId = cartId, ProviderKey = Normalize(x.ProviderKey),
        SellerKey = Normalize(x.SellerKey), ProductKey = x.ProductKey.Trim(), OfferKey = x.OfferKey.Trim(),
        PurchaseOptionKey = Normalize(x.PurchaseOptionKey), Title = x.Title.Trim(),
        SellerTitle = x.SellerTitle.Trim(), ProductTitle = x.ProductTitle.Trim(),
        ProductImageUrl = string.IsNullOrWhiteSpace(x.ProductImageUrl) ? null : x.ProductImageUrl.Trim(),
        OptionTitle = x.OptionTitle.Trim(), OfferTitle = x.OfferTitle.Trim(),
        Quantity = x.Quantity, ExpectedUnitPriceMinor = x.ExpectedUnitPriceMinor,
        OriginalUnitPriceMinor = x.OriginalUnitPriceMinor, IsAvailable = x.IsAvailable
    };

    internal bool IdentityEquals(AabsarCartItemSnapshot x) =>
        ProviderKey.Equals(x.ProviderKey, StringComparison.OrdinalIgnoreCase) &&
        SellerKey.Equals(x.SellerKey, StringComparison.OrdinalIgnoreCase) && ProductKey == x.ProductKey &&
        OfferKey == x.OfferKey && PurchaseOptionKey.Equals(x.PurchaseOptionKey, StringComparison.OrdinalIgnoreCase);
    internal void Replace(AabsarCartItemSnapshot item)
    {
        Quantity = item.Quantity;
        ExpectedUnitPriceMinor = item.ExpectedUnitPriceMinor;
        OriginalUnitPriceMinor = item.OriginalUnitPriceMinor;
        IsAvailable = item.IsAvailable;
        Title = item.Title.Trim();
        SellerTitle = item.SellerTitle.Trim();
        ProductTitle = item.ProductTitle.Trim();
        ProductImageUrl = string.IsNullOrWhiteSpace(item.ProductImageUrl) ? null : item.ProductImageUrl.Trim();
        OptionTitle = item.OptionTitle.Trim();
        OfferTitle = item.OfferTitle.Trim();
    }
    internal void ChangeQuantity(int quantity)
    {
        if (quantity is <= 0 or > 100) throw new AabsarCartException("تعداد آیتم معتبر نیست", "INVALID_QUANTITY");
        Quantity = quantity;
    }
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

public sealed record AabsarCartItemSnapshot(string ProviderKey, string SellerKey, string ProductKey,
    string OfferKey, string PurchaseOptionKey, string Title, string SellerTitle, string ProductTitle,
    string? ProductImageUrl, string OptionTitle, string OfferTitle, int Quantity,
    long ExpectedUnitPriceMinor, long OriginalUnitPriceMinor, bool IsAvailable = true);


public sealed class AabsarCartException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}
