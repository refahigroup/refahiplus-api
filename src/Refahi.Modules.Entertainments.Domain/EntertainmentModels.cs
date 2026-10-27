namespace Refahi.Modules.Entertainments.Domain;

public enum EntertainmentPurchaseStatus : short
{
    AwaitingPayment = 1,
    IssuancePending = 2,
    Issuing = 3,
    Completed = 4,
    CompensationPending = 5,
    CancellationPending = 6,
    ManualReview = 7,
    Cancelled = 8,
    Refunded = 9,
    AwaitingResult = 10
}

public enum ProviderFulfillmentStatus : short
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    CancellationPending = 4,
    Cancelled = 5,
    Failed = 6,
    ManualReview = 7,
    AwaitingResult = 8
}

public enum ProviderOperationOutcome : short
{
    Started = 1,
    Succeeded = 2,
    Failed = 3,
    Ambiguous = 4
}

public sealed class EntertainmentCart
{
    private readonly List<EntertainmentCartItem> _items = [];
    private EntertainmentCart() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<EntertainmentCartItem> Items => _items.AsReadOnly();

    public static EntertainmentCart Create(Guid userId)
    {
        if (userId == Guid.Empty) throw new EntertainmentDomainException("شناسه کاربر معتبر نیست", "INVALID_USER");
        var now = DateTimeOffset.UtcNow;
        return new EntertainmentCart { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now, UpdatedAt = now };
    }

    public void AddOrReplace(EntertainmentCartItemSnapshot item)
    {
        if (item.Quantity is <= 0 or > 100 || item.ExpectedUnitPriceMinor <= 0)
            throw new EntertainmentDomainException("تعداد یا قیمت آیتم معتبر نیست", "INVALID_CART_ITEM");
        var existing = _items.FirstOrDefault(x => x.IdentityEquals(item));
        if (existing is null) _items.Add(EntertainmentCartItem.Create(Id, item));
        else existing.Replace(item);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateQuantity(Guid itemId, int quantity)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new EntertainmentDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        item.ChangeQuantity(quantity);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Remove(Guid itemId)
    {
        var item = _items.FirstOrDefault(x => x.Id == itemId)
            ?? throw new EntertainmentDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        _items.Remove(item);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Clear() { _items.Clear(); UpdatedAt = DateTimeOffset.UtcNow; }
}

public sealed class EntertainmentCartItem
{
    private EntertainmentCartItem() { }
    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string SupplierKey { get; private set; } = string.Empty;
    public string ProgramKey { get; private set; } = string.Empty;
    public string OfferKey { get; private set; } = string.Empty;
    public string PurchaseOptionKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string SupplierTitle { get; private set; } = string.Empty;
    public string ProgramTitle { get; private set; } = string.Empty;
    public string? ProgramImageUrl { get; private set; }
    public string OptionTitle { get; private set; } = string.Empty;
    public string OfferTitle { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public long ExpectedUnitPriceMinor { get; private set; }
    public long OriginalUnitPriceMinor { get; private set; }
    public bool IsAvailable { get; private set; }

    internal static EntertainmentCartItem Create(Guid cartId, EntertainmentCartItemSnapshot x) => new()
    {
        Id = Guid.NewGuid(), CartId = cartId, ProviderKey = Normalize(x.ProviderKey),
        SupplierKey = Normalize(x.SupplierKey), ProgramKey = x.ProgramKey.Trim(), OfferKey = x.OfferKey.Trim(),
        PurchaseOptionKey = Normalize(x.PurchaseOptionKey), Title = x.Title.Trim(),
        SupplierTitle = x.SupplierTitle.Trim(), ProgramTitle = x.ProgramTitle.Trim(),
        ProgramImageUrl = string.IsNullOrWhiteSpace(x.ProgramImageUrl) ? null : x.ProgramImageUrl.Trim(),
        OptionTitle = x.OptionTitle.Trim(), OfferTitle = x.OfferTitle.Trim(),
        Quantity = x.Quantity, ExpectedUnitPriceMinor = x.ExpectedUnitPriceMinor,
        OriginalUnitPriceMinor = x.OriginalUnitPriceMinor, IsAvailable = x.IsAvailable
    };

    internal bool IdentityEquals(EntertainmentCartItemSnapshot x) =>
        ProviderKey.Equals(x.ProviderKey, StringComparison.OrdinalIgnoreCase) &&
        SupplierKey.Equals(x.SupplierKey, StringComparison.OrdinalIgnoreCase) && ProgramKey == x.ProgramKey &&
        OfferKey == x.OfferKey && PurchaseOptionKey.Equals(x.PurchaseOptionKey, StringComparison.OrdinalIgnoreCase);
    internal void Replace(EntertainmentCartItemSnapshot item)
    {
        Quantity = item.Quantity;
        ExpectedUnitPriceMinor = item.ExpectedUnitPriceMinor;
        OriginalUnitPriceMinor = item.OriginalUnitPriceMinor;
        IsAvailable = item.IsAvailable;
        Title = item.Title.Trim();
        SupplierTitle = item.SupplierTitle.Trim();
        ProgramTitle = item.ProgramTitle.Trim();
        ProgramImageUrl = string.IsNullOrWhiteSpace(item.ProgramImageUrl) ? null : item.ProgramImageUrl.Trim();
        OptionTitle = item.OptionTitle.Trim();
        OfferTitle = item.OfferTitle.Trim();
    }
    internal void ChangeQuantity(int quantity)
    {
        if (quantity is <= 0 or > 100) throw new EntertainmentDomainException("تعداد آیتم معتبر نیست", "INVALID_QUANTITY");
        Quantity = quantity;
    }
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

public sealed record EntertainmentCartItemSnapshot(string ProviderKey, string SupplierKey, string ProgramKey,
    string OfferKey, string PurchaseOptionKey, string Title, string SupplierTitle, string ProgramTitle,
    string? ProgramImageUrl, string OptionTitle, string OfferTitle, int Quantity,
    long ExpectedUnitPriceMinor, long OriginalUnitPriceMinor, bool IsAvailable = true);

public sealed class EntertainmentPurchase
{
    private readonly List<EntertainmentPurchaseItem> _items = [];
    private readonly List<ProviderFulfillment> _fulfillments = [];
    private EntertainmentPurchase() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Guid? CheckoutSessionId { get; private set; }
    public DateTimeOffset? PayableUntil { get; private set; }
    public string? ReservationReference { get; private set; }
    public string? ReservationContextProtected { get; private set; }
    public string? CartSelectionProtected { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public EntertainmentPurchaseStatus Status { get; private set; }
    public long TotalAmountMinor { get; private set; }
    public string RecipientNameProtected { get; private set; } = string.Empty;
    public string RecipientMobileProtected { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }
    public IReadOnlyList<EntertainmentPurchaseItem> Items => _items.AsReadOnly();
    public IReadOnlyList<ProviderFulfillment> Fulfillments => _fulfillments.AsReadOnly();

    public static EntertainmentPurchase Create(Guid userId, string idempotencyKey, string fingerprint,
        string protectedName, string protectedMobile, IReadOnlyCollection<EntertainmentPurchaseItemSnapshot> items)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey) || items.Count == 0)
            throw new EntertainmentDomainException("اطلاعات سفارش Entertainment معتبر نیست", "INVALID_ENTERTAINMENT_ORDER");
        var now = DateTimeOffset.UtcNow;
        var value = new EntertainmentPurchase { Id = Guid.NewGuid(), UserId = userId,
            IdempotencyKey = idempotencyKey.Trim(), RequestFingerprint = fingerprint,
            RecipientNameProtected = protectedName, RecipientMobileProtected = protectedMobile,
            Status = EntertainmentPurchaseStatus.AwaitingPayment, CreatedAt = now, UpdatedAt = now };
        foreach (var item in items) value._items.Add(EntertainmentPurchaseItem.Create(value.Id, item));
        foreach (var group in value._items.GroupBy(x => x.ProviderKey, StringComparer.OrdinalIgnoreCase))
            value._fulfillments.Add(ProviderFulfillment.Create(value.Id, group.Key));
        value.TotalAmountMinor = value._items.Sum(x => checked(x.UnitPriceMinor * x.Quantity));
        return value;
    }

    public void EnsureFingerprint(string fingerprint)
    { if (RequestFingerprint != fingerprint) throw new EntertainmentDomainException("کلید یکتایی با اطلاعات متفاوت استفاده شده است", "IDEMPOTENCY_PAYLOAD_MISMATCH"); }
    public void CheckedPayment() => UpdatedAt = DateTimeOffset.UtcNow;
    public void AttachOrder(Guid orderId) { OrderId = orderId; UpdatedAt = DateTimeOffset.UtcNow; }
    public void AttachSession(Guid sessionId, DateTimeOffset until, string? reference, string? context, string selection)
    { CheckoutSessionId = sessionId; PayableUntil = until; ReservationReference = reference; ReservationContextProtected = context; CartSelectionProtected = selection; }
    public void AwaitResult() { Status = EntertainmentPurchaseStatus.AwaitingResult; UpdatedAt = DateTimeOffset.UtcNow; }
    public void QueueFulfillment(Guid? paymentId = null) { if (Status == EntertainmentPurchaseStatus.AwaitingPayment) Status = EntertainmentPurchaseStatus.IssuancePending; PaymentId ??= paymentId; UpdatedAt = DateTimeOffset.UtcNow; }
    public void BeginFulfillment() { if (Status is EntertainmentPurchaseStatus.IssuancePending or EntertainmentPurchaseStatus.Issuing) Status = EntertainmentPurchaseStatus.Issuing; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Complete() { Status = EntertainmentPurchaseStatus.Completed; UpdatedAt = DateTimeOffset.UtcNow; }
    public void RequireManualReview() { Status = EntertainmentPurchaseStatus.ManualReview; UpdatedAt = DateTimeOffset.UtcNow; }
    public void ResumeFulfillment() { if (Status == EntertainmentPurchaseStatus.ManualReview) Status = EntertainmentPurchaseStatus.IssuancePending; UpdatedAt = DateTimeOffset.UtcNow; }
    public void BeginCompensation() { Status = EntertainmentPurchaseStatus.CompensationPending; UpdatedAt = DateTimeOffset.UtcNow; }
    public void BeginCancellation() { Status = EntertainmentPurchaseStatus.CancellationPending; UpdatedAt = DateTimeOffset.UtcNow; }
    public void MarkCancelled(bool refunded) { Status = refunded ? EntertainmentPurchaseStatus.Refunded : EntertainmentPurchaseStatus.Cancelled; UpdatedAt = DateTimeOffset.UtcNow; }
    public void RedactRecipient()
    {
        if (Status is not EntertainmentPurchaseStatus.Completed and not EntertainmentPurchaseStatus.Cancelled and not EntertainmentPurchaseStatus.Refunded)
            throw new EntertainmentDomainException("اطلاعات دریافت‌کننده در سفارش فعال قابل حذف نیست", "RECIPIENT_RETENTION_NOT_REACHED");
        RecipientNameProtected = string.Empty; RecipientMobileProtected = string.Empty;
        ReservationContextProtected = null; CartSelectionProtected = null; UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class EntertainmentPurchaseItem
{
    private EntertainmentPurchaseItem() { }
    public Guid Id { get; private set; }
    public Guid EntertainmentPurchaseId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public string SupplierKey { get; private set; } = string.Empty;
    public string ProgramKey { get; private set; } = string.Empty;
    public string OfferKey { get; private set; } = string.Empty;
    public string PurchaseOptionKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string OfferTitle { get; private set; } = string.Empty;
    public string CategoryCode { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public long UnitPriceMinor { get; private set; }
    public string ProviderPayloadJson { get; private set; } = "{}";
    internal static EntertainmentPurchaseItem Create(Guid orderId, EntertainmentPurchaseItemSnapshot x) => new()
    { Id = Guid.NewGuid(), EntertainmentPurchaseId = orderId, ProviderKey = x.ProviderKey, SupplierKey = x.SupplierKey,
      ProgramKey = x.ProgramKey, OfferKey = x.OfferKey, PurchaseOptionKey = x.PurchaseOptionKey,
      Title = x.Title, OfferTitle = x.OfferTitle, CategoryCode = x.CategoryCode,
      Quantity = x.Quantity, UnitPriceMinor = x.UnitPriceMinor, ProviderPayloadJson = x.ProviderPayloadJson };
}

public sealed record EntertainmentPurchaseItemSnapshot(string ProviderKey, string SupplierKey, string ProgramKey,
    string OfferKey, string PurchaseOptionKey, string Title, string OfferTitle, string CategoryCode,
    int Quantity, long UnitPriceMinor, string ProviderPayloadJson);

public sealed class ProviderFulfillment
{
    private readonly List<ProviderTicket> _tickets = [];
    private readonly List<ProviderOperationAttempt> _attempts = [];
    private ProviderFulfillment() { }
    public Guid Id { get; private set; }
    public Guid EntertainmentPurchaseId { get; private set; }
    public string ProviderKey { get; private set; } = string.Empty;
    public ProviderFulfillmentStatus Status { get; private set; }
    public string? ProviderOrderCode { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ProviderInvoiceId { get; private set; }
    public string? ProviderPaymentId { get; private set; }
    public string? DeliveryProtected { get; private set; }
    public DateTimeOffset? ReconcileStartedAt { get; private set; }
    public DateTimeOffset? NextCheckAt { get; private set; }
    public int CheckCount { get; private set; }
    public void RestartReconciliation()
    {
        if (Status != ProviderFulfillmentStatus.ManualReview)
            throw new EntertainmentDomainException("استعلام مجدد در این وضعیت مجاز نیست", "INVALID_RECHECK");
        ReconcileStartedAt = DateTimeOffset.UtcNow; NextCheckAt = DateTimeOffset.UtcNow; CheckCount = 0;
        Status = ProviderFulfillmentStatus.AwaitingResult;
    }
    public void AwaitResult(string? invoice, string? payment)
    {
        ProviderInvoiceId = invoice ?? ProviderInvoiceId; ProviderPaymentId = payment ?? ProviderPaymentId;
        ReconcileStartedAt ??= DateTimeOffset.UtcNow; CheckCount++;
        NextCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 5 * Math.Pow(2, Math.Min(CheckCount, 4))));
        Status = ProviderFulfillmentStatus.AwaitingResult;
    }
    public void SetDelivery(string value, string? invoice, string? payment)
    { DeliveryProtected = value; ProviderInvoiceId = invoice; ProviderPaymentId = payment; NextCheckAt = null; }
    public IReadOnlyList<ProviderTicket> Tickets => _tickets.AsReadOnly();
    public IReadOnlyList<ProviderOperationAttempt> Attempts => _attempts.AsReadOnly();
    internal static ProviderFulfillment Create(Guid orderId, string providerKey) => new()
    { Id = Guid.NewGuid(), EntertainmentPurchaseId = orderId, ProviderKey = providerKey, Status = ProviderFulfillmentStatus.Pending };
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
    public Guid? ActorId { get; private set; }
    public string? EvidenceProtected { get; private set; }
    public void RedactEvidence() => EvidenceProtected = null;
    public void RecordEvidence(Guid actor, string protectedEvidence) { ActorId = actor; EvidenceProtected = protectedEvidence; }
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
