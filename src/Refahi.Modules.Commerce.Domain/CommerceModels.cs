namespace Refahi.Modules.Commerce.Domain;

public enum CommerceOrderStatus : short
{
    PendingPayment = 1,
    FulfillmentPending = 2,
    Fulfilling = 3,
    Completed = 4,
    CompensationPending = 5,
    CancellationPending = 6,
    ManualReview = 7,
    Cancelled = 8,
    Refunded = 9
}

public enum ProviderFulfillmentStatus : short
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    CancellationPending = 4,
    Cancelled = 5,
    Failed = 6,
    ManualReview = 7
}

public enum ProviderOperationOutcome : short
{
    Started = 1,
    Succeeded = 2,
    Failed = 3,
    Ambiguous = 4
}

public sealed class CommerceCart
{
    private readonly List<CommerceCartItem> _items = [];
    private CommerceCart() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<CommerceCartItem> Items => _items.AsReadOnly();

    public static CommerceCart Create(Guid userId)
    {
        if (userId == Guid.Empty) throw new CommerceDomainException("شناسه کاربر معتبر نیست", "INVALID_USER");
        var now = DateTimeOffset.UtcNow;
        return new CommerceCart { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now, UpdatedAt = now };
    }

    public void AddOrReplace(CommerceCartItemSnapshot item)
    {
        if (item.Quantity is <= 0 or > 100 || item.ExpectedUnitPriceMinor <= 0)
            throw new CommerceDomainException("تعداد یا قیمت آیتم معتبر نیست", "INVALID_CART_ITEM");
        var existing = _items.FirstOrDefault(x => x.IdentityEquals(item));
        if (existing is null) _items.Add(CommerceCartItem.Create(Id, item));
        else existing.Replace(item.Quantity, item.ExpectedUnitPriceMinor, item.Title, item.OfferTitle);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateQuantity(Guid itemId, int quantity)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new CommerceDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        item.ChangeQuantity(quantity);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Remove(Guid itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new CommerceDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        _items.Remove(item);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Clear() { _items.Clear(); UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class CommerceCartItem
{
    private CommerceCartItem() { }
    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string SellerKey { get; private set; } = string.Empty;
    public string ProductKey { get; private set; } = string.Empty;
    public string OfferKey { get; private set; } = string.Empty;
    public string PurchaseOptionKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string OfferTitle { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public long ExpectedUnitPriceMinor { get; private set; }

    internal static CommerceCartItem Create(Guid cartId, CommerceCartItemSnapshot x) => new()
    {
        Id = Guid.NewGuid(), CartId = cartId, ProviderKey = Normalize(x.ProviderKey),
        SellerKey = Normalize(x.SellerKey), ProductKey = x.ProductKey.Trim(), OfferKey = x.OfferKey.Trim(),
        PurchaseOptionKey = Normalize(x.PurchaseOptionKey), Title = x.Title.Trim(), OfferTitle = x.OfferTitle.Trim(),
        Quantity = x.Quantity, ExpectedUnitPriceMinor = x.ExpectedUnitPriceMinor
    };

    internal bool IdentityEquals(CommerceCartItemSnapshot x) =>
        ProviderKey.Equals(x.ProviderKey, StringComparison.OrdinalIgnoreCase) &&
        SellerKey.Equals(x.SellerKey, StringComparison.OrdinalIgnoreCase) && ProductKey == x.ProductKey &&
        OfferKey == x.OfferKey && PurchaseOptionKey.Equals(x.PurchaseOptionKey, StringComparison.OrdinalIgnoreCase);
    internal void Replace(int quantity, long price, string title, string offerTitle)
    { Quantity = quantity; ExpectedUnitPriceMinor = price; Title = title.Trim(); OfferTitle = offerTitle.Trim(); }
    internal void ChangeQuantity(int quantity)
    {
        if (quantity is <= 0 or > 100) throw new CommerceDomainException("تعداد آیتم معتبر نیست", "INVALID_QUANTITY");
        Quantity = quantity;
    }
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

public sealed record CommerceCartItemSnapshot(string ProviderKey, string SellerKey, string ProductKey,
    string OfferKey, string PurchaseOptionKey, string Title, string OfferTitle, int Quantity,
    long ExpectedUnitPriceMinor);

public sealed class CommerceOrder
{
    private readonly List<CommerceOrderItem> _items = [];
    private readonly List<ProviderFulfillment> _fulfillments = [];
    private CommerceOrder() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public CommerceOrderStatus Status { get; private set; }
    public long TotalAmountMinor { get; private set; }
    public string RecipientNameProtected { get; private set; } = string.Empty;
    public string RecipientMobileProtected { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<CommerceOrderItem> Items => _items.AsReadOnly();
    public IReadOnlyList<ProviderFulfillment> Fulfillments => _fulfillments.AsReadOnly();

    public static CommerceOrder Create(Guid userId, string idempotencyKey, string fingerprint,
        string protectedName, string protectedMobile, IReadOnlyCollection<CommerceOrderItemSnapshot> items)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey) || items.Count == 0)
            throw new CommerceDomainException("اطلاعات سفارش Commerce معتبر نیست", "INVALID_COMMERCE_ORDER");
        var now = DateTimeOffset.UtcNow;
        var value = new CommerceOrder { Id = Guid.NewGuid(), UserId = userId,
            IdempotencyKey = idempotencyKey.Trim(), RequestFingerprint = fingerprint,
            RecipientNameProtected = protectedName, RecipientMobileProtected = protectedMobile,
            Status = CommerceOrderStatus.PendingPayment, CreatedAt = now, UpdatedAt = now };
        foreach (var item in items) value._items.Add(CommerceOrderItem.Create(value.Id, item));
        foreach (var group in value._items.GroupBy(x => x.ProviderKey, StringComparer.OrdinalIgnoreCase))
            value._fulfillments.Add(ProviderFulfillment.Create(value.Id, group.Key));
        value.TotalAmountMinor = value._items.Sum(x => checked(x.UnitPriceMinor * x.Quantity));
        return value;
    }

    public void EnsureFingerprint(string fingerprint)
    { if (RequestFingerprint != fingerprint) throw new CommerceDomainException("کلید یکتایی با اطلاعات متفاوت استفاده شده است", "IDEMPOTENCY_PAYLOAD_MISMATCH"); }
    public void AttachOrder(Guid orderId) { OrderId = orderId; UpdatedAt = DateTimeOffset.UtcNow; }
    public void QueueFulfillment(Guid? paymentId = null) { if (Status == CommerceOrderStatus.PendingPayment) Status = CommerceOrderStatus.FulfillmentPending; PaymentId ??= paymentId; UpdatedAt = DateTimeOffset.UtcNow; }
    public void BeginFulfillment() { if (Status is CommerceOrderStatus.FulfillmentPending or CommerceOrderStatus.Fulfilling) Status = CommerceOrderStatus.Fulfilling; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Complete() { Status = CommerceOrderStatus.Completed; UpdatedAt = DateTimeOffset.UtcNow; }
    public void RequireManualReview() { Status = CommerceOrderStatus.ManualReview; UpdatedAt = DateTimeOffset.UtcNow; }
    public void ResumeFulfillment() { if (Status == CommerceOrderStatus.ManualReview) Status = CommerceOrderStatus.FulfillmentPending; UpdatedAt = DateTimeOffset.UtcNow; }
    public void BeginCompensation() { Status = CommerceOrderStatus.CompensationPending; UpdatedAt = DateTimeOffset.UtcNow; }
    public void BeginCancellation() { Status = CommerceOrderStatus.CancellationPending; UpdatedAt = DateTimeOffset.UtcNow; }
    public void MarkCancelled(bool refunded) { Status = refunded ? CommerceOrderStatus.Refunded : CommerceOrderStatus.Cancelled; UpdatedAt = DateTimeOffset.UtcNow; }
    public void RedactRecipient()
    {
        if (Status is not CommerceOrderStatus.Completed and not CommerceOrderStatus.Cancelled and not CommerceOrderStatus.Refunded)
            throw new CommerceDomainException("اطلاعات دریافت‌کننده در سفارش فعال قابل حذف نیست", "RECIPIENT_RETENTION_NOT_REACHED");
        RecipientNameProtected = string.Empty; RecipientMobileProtected = string.Empty; UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class CommerceOrderItem
{
    private CommerceOrderItem() { }
    public Guid Id { get; private set; }
    public Guid CommerceOrderId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string SellerKey { get; private set; } = string.Empty;
    public string ProductKey { get; private set; } = string.Empty;
    public string OfferKey { get; private set; } = string.Empty;
    public string PurchaseOptionKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string OfferTitle { get; private set; } = string.Empty;
    public string CategoryCode { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public long UnitPriceMinor { get; private set; }
    public string ProviderPayloadJson { get; private set; } = "{}";
    internal static CommerceOrderItem Create(Guid orderId, CommerceOrderItemSnapshot x) => new()
    { Id = Guid.NewGuid(), CommerceOrderId = orderId, ProviderKey = x.ProviderKey, SellerKey = x.SellerKey,
      ProductKey = x.ProductKey, OfferKey = x.OfferKey, PurchaseOptionKey = x.PurchaseOptionKey,
      Title = x.Title, OfferTitle = x.OfferTitle, CategoryCode = x.CategoryCode,
      Quantity = x.Quantity, UnitPriceMinor = x.UnitPriceMinor, ProviderPayloadJson = x.ProviderPayloadJson };
}

public sealed record CommerceOrderItemSnapshot(string ProviderKey, string SellerKey, string ProductKey,
    string OfferKey, string PurchaseOptionKey, string Title, string OfferTitle, string CategoryCode,
    int Quantity, long UnitPriceMinor, string ProviderPayloadJson);

public sealed class ProviderFulfillment
{
    private readonly List<ProviderTicket> _tickets = [];
    private readonly List<ProviderOperationAttempt> _attempts = [];
    private ProviderFulfillment() { }
    public Guid Id { get; private set; }
    public Guid CommerceOrderId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public ProviderFulfillmentStatus Status { get; private set; }
    public string? ProviderOrderCode { get; private set; }
    public string? FailureReason { get; private set; }
    public IReadOnlyList<ProviderTicket> Tickets => _tickets.AsReadOnly();
    public IReadOnlyList<ProviderOperationAttempt> Attempts => _attempts.AsReadOnly();
    internal static ProviderFulfillment Create(Guid orderId, string providerKey) => new()
    { Id = Guid.NewGuid(), CommerceOrderId = orderId, ProviderKey = providerKey, Status = ProviderFulfillmentStatus.Pending };
    public ProviderOperationAttempt BeginAttempt(string operation, string idempotencyKey, string payloadHash)
    { Status = operation == "cancel" ? ProviderFulfillmentStatus.CancellationPending : ProviderFulfillmentStatus.Processing;
      var value = ProviderOperationAttempt.Start(Id, operation, idempotencyKey, payloadHash); _attempts.Add(value); return value; }
    public void Complete(string orderCode, IEnumerable<(string Hash, string Protected, bool IsChild)> tickets)
    { ProviderOrderCode = orderCode; _tickets.AddRange(tickets.Select(x => ProviderTicket.Create(Id, x.Hash, x.Protected, x.IsChild)));
      Status = ProviderFulfillmentStatus.Completed; FailureReason = null; }
    public void Fail(string reason, bool ambiguous)
    { FailureReason = reason; Status = ambiguous ? ProviderFulfillmentStatus.ManualReview : ProviderFulfillmentStatus.Failed; }
    public void MarkCancelled() { Status = ProviderFulfillmentStatus.Cancelled; }
}

public sealed class ProviderTicket
{
    private ProviderTicket() { }
    public Guid Id { get; private set; }
    public Guid ProviderFulfillmentId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public string CodeProtected { get; private set; } = string.Empty;
    public bool IsChild { get; private set; }
    internal static ProviderTicket Create(Guid id, string hash, string protectedCode, bool child) => new()
    { Id = Guid.NewGuid(), ProviderFulfillmentId = id, CodeHash = hash, CodeProtected = protectedCode, IsChild = child };
}

public sealed class ProviderOperationAttempt
{
    private ProviderOperationAttempt() { }
    public Guid Id { get; private set; }
    public Guid ProviderFulfillmentId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public ProviderOperationOutcome Outcome { get; private set; }
    public string? SanitizedError { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    internal static ProviderOperationAttempt Start(Guid id, string operation, string key, string hash) => new()
    { Id = Guid.NewGuid(), ProviderFulfillmentId = id, Operation = operation, IdempotencyKey = key,
      PayloadHash = hash, Outcome = ProviderOperationOutcome.Started, StartedAt = DateTimeOffset.UtcNow };
    public void Complete() { Outcome = ProviderOperationOutcome.Succeeded; CompletedAt = DateTimeOffset.UtcNow; }
    public void Fail(string error, bool ambiguous) { Outcome = ambiguous ? ProviderOperationOutcome.Ambiguous : ProviderOperationOutcome.Failed;
      SanitizedError = error.Length <= 1000 ? error : error[..1000]; CompletedAt = DateTimeOffset.UtcNow; }
}
